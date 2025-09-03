using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Game.Gui;
using Blackjack.GameModules;
using Dalamud.Logging;
using Dalamud.Plugin.Services;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Utility;
using Dalamud.Game.Command;

namespace Blackjack.GameModules
{
    public class BlackjackModule : IGameModule
    {
        private readonly Dictionary<string, PlayerHand> _hands = new();
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;
        private readonly IDalamudPluginInterface _pluginInterface;
        private readonly Dictionary<string, string> DisplayNames = new();

        public bool RoundActive { get; private set; } = false;
        public bool RoundOver { get; private set; } = false;

        private readonly List<string> _turnOrder = new();
        private int _currentPlayerIndex = 0;
        public int? WinningScore { get; private set; }

        private List<string> _winners = new();
        public IReadOnlyList<string> Winners =>
            _winners.Select(n => _partyPlayerDisplayNames.TryGetValue(n, out var dn) ? dn : n).ToList();

        private List<string> _partyPlayers = new();
        private readonly Dictionary<string, string> _partyPlayerDisplayNames = new();

        public IReadOnlyList<(string NormalizedName, string DisplayName)> PartyPlayersWithDisplayNames
            => _partyPlayers.Select(n => (n, _partyPlayerDisplayNames.TryGetValue(n, out var dn) ? dn : n)).ToList();

        public IReadOnlyList<string> PartyPlayers => _partyPlayers;

        private string? _dealer = null;
        public string? Dealer
        {
            get => _dealer;
            set
            {
                if (value == null)
                {
                    _dealer = null;
                    RebuildTurnOrder();
                    return;
                }

                var normalized = NormalizeWithDisplayName(value).Normalized;
                if (!_partyPlayers.Contains(normalized))
                {
                    _log.Debug($"Attempt to set invalid dealer '{value}'");
                    return;
                }

                _dealer = normalized;
                _log.Debug($"Dealer set to {_dealer}");
                RebuildTurnOrder();
            }
        }

        private int _lastBestScore = 0;
        private string _lastWinnerMessage = "";

        public string Name => "Blackjack";

        private readonly ICommandManager _commandManager;

        public BlackjackModule(IPluginLog log, IChatGui chat, IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
        {
            _log = log;
            _chat = chat;
            _pluginInterface = pluginInterface;
            _commandManager = commandManager;
        }

        public string? CurrentPlayer =>
            _turnOrder.Count > 0 ? _turnOrder[_currentPlayerIndex] : null;

        private PlayerHand? CurrentPlayerHand =>
            CurrentPlayer != null && _hands.TryGetValue(CurrentPlayer, out var hand) ? hand : null;

        public void UpdatePartyPlayers(IEnumerable<string> players)
        {
            var currentDealer = _dealer;

            _partyPlayers.Clear();
            _partyPlayerDisplayNames.Clear();

            foreach (var player in players.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var (normalized, displayName) = NormalizeWithDisplayName(player);
                if (!_partyPlayers.Contains(normalized))
                    _partyPlayers.Add(normalized);

                _partyPlayerDisplayNames[normalized] = displayName;
            }

            if (currentDealer != null && _partyPlayers.Contains(currentDealer))
                _dealer = currentDealer;
            else
            {
                _dealer = null;
                _log.Debug("Dealer cleared because not in party anymore.");
            }

            RebuildTurnOrder();
        }

        public void StartRound()
        {
            _hands.Clear();
            _winners.Clear();
            _lastWinnerMessage = "";
            _lastBestScore = 0;
            WinningScore = null;
            _currentPlayerIndex = 0;
            RoundActive = true;
            RoundOver = false;

            RebuildTurnOrder();

            _log.Debug("Round started with turn order: " + string.Join(", ", _turnOrder));
        }

        public void EndRound()
        {
            RoundActive = false;
            RoundOver = false;
            _currentPlayerIndex = 0;

            if (_hands.Count == 0)
            {
                _chat.Print("[Blackjack] Round ended. No players rolled.");
                _lastWinnerMessage = "";
                _lastBestScore = 0;
                WinningScore = null;
                return;
            }

            // --- Dealer logic ---
            if (_dealer == null || !_hands.ContainsKey(_dealer))
            {
                _chat.Print("[Blackjack] No dealer set. Cannot determine winners.");
                return;
            }

            var dealerHand = _hands[_dealer];
            int dealerScore = dealerHand.Hands
                .Select(GetBestScoreForHand)
                .Where(score => score <= 21)
                .DefaultIfEmpty(0)
                .Max();
            bool dealerBusted = dealerScore > 21 || dealerScore == 0;

            _log.Debug($"Dealer score: {dealerScore} (busted={dealerBusted})");

            _winners.Clear();

            // Track which hands won for each player
            var winningHandsPerPlayer = new Dictionary<string, List<int>>();

            foreach (var kvp in _hands)
            {
                var playerName = kvp.Key;
                if (playerName == _dealer)
                    continue;

                var playerHand = kvp.Value;

                for (int i = 0; i < playerHand.Hands.Count; i++)
                {
                    var hand = playerHand.Hands[i];
                    int score = GetBestScoreForHand(hand);

                    if (score > 21) continue;

                    if (dealerBusted || score > dealerScore)
                    {
                        if (!_winners.Contains(playerName))
                            _winners.Add(playerName);

                        if (!winningHandsPerPlayer.ContainsKey(playerName))
                            winningHandsPerPlayer[playerName] = new List<int>();
                        winningHandsPerPlayer[playerName].Add(i);

                        _log.Debug($"{playerName} hand #{i + 1} wins with {score} against dealer {dealerScore}");
                    }
                }
            }

            if (_winners.Count > 0)
            {
                WinningScore = _winners
                    .Select(w => _hands[w].Hands.Select(GetBestScoreForHand).Max())
                    .Max();

                var winnerMessages = new List<string>();
                foreach (var winner in _winners)
                {
                    if (winningHandsPerPlayer.TryGetValue(winner, out var hands))
                    {
                        var handScores = hands
                            .Select(idx => GetBestScoreForHand(_hands[winner].Hands[idx]))
                            .ToList();
                        string handsStr = string.Join(", ", handScores.Select((s, idx) => $"Hand {idx + 1}: {s}"));
                        winnerMessages.Add($"{GetDisplayName(winner)} ({handsStr})");
                    }
                    else
                    {
                        winnerMessages.Add(GetDisplayName(winner));
                    }
                }

                _lastWinnerMessage = $"[Blackjack] Winner(s): {string.Join(", ", winnerMessages)} against dealer ({dealerScore})!";
                _log.Debug($"Winners: {string.Join(", ", winnerMessages)} vs dealer {dealerScore}");
                AnnounceWinnersToParty();
            }
            else
            {
                WinningScore = dealerScore;
                _lastWinnerMessage = "[Blackjack] Dealer wins!";
                _chat.Print(_lastWinnerMessage);
                _log.Debug("Dealer wins, no players beat them.");
            }
        }

        public void AddRoll(string player, int roll)
        {
            if (!RoundActive || RoundOver)
                return;

            if (roll < 1 || roll > 10)
            {
                _log.Debug($"[Blackjack] Ignored invalid roll {roll} from {player}");
                return;
            }

            var (normalized, displayName) = NormalizeWithDisplayName(player);
            _log.Debug($"{normalized} rolled {roll}");

            _partyPlayerDisplayNames[normalized] = displayName;

            if (!_hands.ContainsKey(normalized))
                _hands[normalized] = new PlayerHand(normalized, displayName);
            else
                _hands[normalized].SetDisplayName(displayName);

            var hand = _hands[normalized];

            if (hand.AllHandsFinished())
                return;

            hand.AddCard(roll);
            _log.Debug($"{normalized} hand #{hand.CurrentHandIndex + 1}: {string.Join(", ", hand.Hands[hand.CurrentHandIndex])} = {hand.Score}");

            if (hand.Score > 21)
            {
                hand.Stand();
                _log.Debug($"{normalized}'s hand #{hand.CurrentHandIndex + 1} busted!");
            }
            else if (hand.Score == 21)
            {
                hand.Stand();
                _log.Debug($"{normalized}'s hand #{hand.CurrentHandIndex + 1} hit 21!");
            }

            if (hand.IsHandDone(hand.CurrentHandIndex))
            {
                AdvanceToNextHandOrTurn(hand);
            }
        }

        public void TrySplit(string player)
        {
            if (!RoundActive || RoundOver)
                return;

            var (normalized, displayName) = NormalizeWithDisplayName(player);
            _partyPlayerDisplayNames[normalized] = displayName;

            if (_hands.TryGetValue(normalized, out var hand) && hand.CanSplit())
            {
                hand.Split();
                _chat.Print($"[Blackjack] {displayName} split their hand!");
                _log.Debug($"{normalized} split their hand.");
            }
            else
            {
                _chat.Print($"[Blackjack] {displayName} cannot split right now.");
            }
        }

        public void PlayerStands(string player)
        {
            if (!RoundActive || RoundOver)
                return;

            var (normalized, displayName) = NormalizeWithDisplayName(player);
            _partyPlayerDisplayNames[normalized] = displayName;

            if (!_turnOrder.Contains(normalized))
                return;

            if (!_hands.TryGetValue(normalized, out var hand))
                return;

            if (hand.IsHandDone(hand.CurrentHandIndex))
                return;

            hand.Stand();
            _log.Debug($"{normalized} stands on hand #{hand.CurrentHandIndex + 1} with score {hand.Score}");

            AdvanceToNextHandOrTurn(hand);
        }

        private void AdvanceToNextHandOrTurn(PlayerHand hand)
        {
            for (int i = hand.CurrentHandIndex + 1; i < hand.Hands.Count; i++)
            {
                if (!hand.IsHandDone(i))
                {
                    hand.CurrentHandIndex = i;
                    _log.Debug($"{hand.DisplayName} moves to hand #{i + 1}");
                    return;
                }
            }

            _log.Debug($"{hand.DisplayName} finished all hands");
            NextTurn();
        }

        private void NextTurn()
        {
            int safety = 0;
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _turnOrder.Count;
                safety++;
                if (safety > _turnOrder.Count)
                {
                    RoundOver = true;
                    _log.Debug("Round over, all players done.");
                    return;
                }

                var currentPlayer = CurrentPlayer;
                if (currentPlayer == null)
                {
                    RoundOver = true;
                    return;
                }

                if (_hands.TryGetValue(currentPlayer, out var hand) && !hand.AllHandsFinished())
                {
                    _log.Debug($"Next turn: {currentPlayer}");
                    return;
                }

            } while (true);
        }

        private void RebuildTurnOrder()
        {
            var ordered = new List<string>(_partyPlayers);

            if (_dealer != null)
                ordered.Remove(_dealer);

            if (_dealer != null)
                ordered.Add(_dealer);

            _turnOrder.Clear();
            _turnOrder.AddRange(ordered);

            if (_currentPlayerIndex >= _turnOrder.Count)
                _currentPlayerIndex = 0;
        }

        public IReadOnlyDictionary<string, PlayerHand> GetHands() => _hands;

        public bool IsPlayerDone(string player)
        {
            var normalized = NormalizeName(player);
            return !_hands.ContainsKey(normalized) || _hands[normalized].AllHandsFinished();
        }

        private (string Normalized, string DisplayName) NormalizeWithDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ("", "");

            var trimmed = name.Trim();

            while (trimmed.Length > 0 && (trimmed[0] < 32 || !char.IsLetterOrDigit(trimmed[0])))
                trimmed = trimmed.Substring(1);

            var split = System.Text.RegularExpressions.Regex.Replace(trimmed, "([a-z])([A-Z])", "$1 $2");
            var parts = split.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var knownWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Adamantoise","Cactuar","Faerie","Gilgamesh","Jenova","Midgardsormr","Sargatanas","Siren",
                "Behemoth","Excalibur","Exodus","Famfrit","Hyperion","Lamia","Leviathan","Ultros",
                "Balmung","Brynhildr","Coeurl","Diabolos","Goblin","Malboro","Mateus","Zalera",
                "Halicarnassus","Maduin","Marilith","Seraph","Cuchulainn","Golem","Kraken","Rafflesia",
                "Anima","Asura","Chocobo","Hades","Ixion","Masamune","Pandaemonium","Titan",
                "Belias","Mandragora","Ramuh","Shinryu","Unicorn","Valefor","Yojimbo","Zeromus",
                "Alexander","Bahamut","Durandal","Fenrir","Ifrit","Ridill","Tiamat","Ultima",
                "Aegis","Atomos","Carbuncle","Garuda","Gungnir","Kujata","Tonberry","Typhon",
                "Cerberus","Louisoix","Moogle","Omega","Phantom","Ragnarok","Raiden","Spriggan",
                "Shiva","Twintania","Lich","Odin","Zodiark","Bismarck","Ravana","Sephirot","Sophia","Zurvan"
            };

            string combined = string.Join(" ", parts);
            foreach (var world in knownWorlds)
            {
                if (combined.EndsWith(world, StringComparison.OrdinalIgnoreCase))
                {
                    combined = combined.Substring(0, combined.Length - world.Length).TrimEnd();
                    break;
                }
            }

            var nameParts = combined.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string displayName = nameParts.Length >= 2 ? $"{nameParts[0]} {nameParts[1]}" : nameParts.FirstOrDefault() ?? "";
            string normalized = displayName.ToLowerInvariant();

            return (normalized, displayName);
        }

        private string NormalizeName(string name)
        {
            return NormalizeWithDisplayName(name).Normalized;
        }

        public void UpdateDisplayNameMapping(string normalizedName, string displayName)
        {
            if (string.IsNullOrEmpty(normalizedName) || string.IsNullOrEmpty(displayName))
                return;

            _partyPlayerDisplayNames[normalizedName] = displayName;

            if (_hands.TryGetValue(normalizedName, out var hand))
                hand.SetDisplayName(displayName);
        }

        public void AnnounceWinnersToParty()
        {
            if (string.IsNullOrWhiteSpace(_lastWinnerMessage))
                return;

            var command = $"/p {_lastWinnerMessage}";
            _log.Debug($"[BlackJackTracker] Executing: {command}");

            _commandManager.ProcessCommand(command);
        }

        public string GetLastWinnerMessage()
        {
            if (Winners.Count == 0)
                return "";

            var winnersWithDisplay = Winners
                .Select(name =>
                    PartyPlayersWithDisplayNames.FirstOrDefault(p => p.NormalizedName == name).DisplayName ?? name
                );

            return $"[Blackjack] Winner(s): {string.Join(", ", winnersWithDisplay)} with {WinningScore}!";
        }

        private int GetBestScoreForHand(List<int> hand)
        {
            int sum = hand.Sum();
            int aceCount = hand.Count(c => c == 1);
            while (aceCount > 0 && sum + 10 <= 21)
            {
                sum += 10;
                aceCount--;
            }
            return sum;
        }

        public string GetDisplayName(string normalizedName)
        {
            return PartyPlayersWithDisplayNames
                .FirstOrDefault(p => p.NormalizedName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase))
                .DisplayName ?? normalizedName;
        }

        public IReadOnlyDictionary<string, string> GetDisplayNames()
        {
            return DisplayNames;
        }
    }
}
