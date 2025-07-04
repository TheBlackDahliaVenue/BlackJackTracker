using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using Dalamud.Game.Gui;   // for IChatGui
using Dalamud.Plugin.Services;

namespace Blackjack
{
    public class BlackjackManager
    {
        private readonly Dictionary<string, PlayerHand> _hands = new();
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;
        private readonly IDalamudPluginInterface _pluginInterface;

        public bool RoundActive { get; private set; } = false;
        public bool RoundOver { get; private set; } = false;

        private readonly List<string> _turnOrder = new();
        private int _currentPlayerIndex = 0;
        private readonly HashSet<string> _playersDone = new();

        private List<string> _winners = new();
        public IReadOnlyList<string> Winners => _winners;

        private List<string> _partyPlayers = new();
        private readonly Dictionary<string, string> _partyPlayerDisplayNames = new(); // normalized -> display

        // Expose normalized + display names for UI
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

                var normalized = NormalizeName(value);
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

        private int _lastBestScore = 0; // store best score
        private string _lastWinnerMessage = ""; // store full winner message

        public BlackjackManager(IPluginLog log, IChatGui chat, IDalamudPluginInterface pluginInterface)
        {
            _log = log;
            _chat = chat;
            _pluginInterface = pluginInterface;
        }

        public string? CurrentPlayer =>
            _turnOrder.Count > 0 && _hands.TryGetValue(_turnOrder[_currentPlayerIndex], out var hand)
                ? hand.DisplayName
                : _turnOrder.Count > 0 ? _turnOrder[_currentPlayerIndex] : null;

        public void UpdatePartyPlayers(IEnumerable<string> players)
        {
            var currentDealer = _dealer;

            _partyPlayers.Clear();
            _partyPlayerDisplayNames.Clear();

            foreach (var player in players.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var normalized = NormalizeName(player);
                if (!_partyPlayers.Contains(normalized))
                {
                    _partyPlayers.Add(normalized);
                    _partyPlayerDisplayNames[normalized] = player;
                }
                else
                {
                    _partyPlayerDisplayNames[normalized] = player;
                }
            }

            if (currentDealer != null && _partyPlayers.Contains(currentDealer))
            {
                _dealer = currentDealer;
            }
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
            _playersDone.Clear();
            _winners.Clear();
            _lastWinnerMessage = "";
            _lastBestScore = 0;
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
            _playersDone.Clear();
            _currentPlayerIndex = 0;

            if (_hands.Count == 0)
            {
                _chat.Print("[Blackjack] Round ended. No players rolled.");
                _lastWinnerMessage = "";
                _lastBestScore = 0;
                return;
            }

            int bestScore = _hands.Values
                .Where(h => !h.IsBusted)
                .Select(h => h.Score)
                .DefaultIfEmpty(0)
                .Max();

            _lastBestScore = bestScore;

            _winners = _hands.Values
                .Where(h => !h.IsBusted && h.Score == bestScore)
                .Select(h => h.DisplayName)
                .ToList();

            if (_winners.Count > 0)
            {
                _lastWinnerMessage = $"[Blackjack] Winner(s): {string.Join(", ", _winners)} with {_lastBestScore}!";

                _log.Debug($"Winners: {string.Join(", ", _winners)} with score {bestScore}");

                AnnounceWinnersToParty(); // prints once using stored message
            }
            else
            {
                _chat.Print("[Blackjack] No winners this round.");
                _lastWinnerMessage = "";
                _lastBestScore = 0;
                _log.Debug("No winners this round.");
            }
        }

        public void AddRoll(string player, int roll)
        {
            if (!RoundActive || RoundOver)
                return;

            var normalized = NormalizeName(player);
            _log.Debug($"{normalized} rolled {roll}");

            if (!_hands.ContainsKey(normalized))
                _hands[normalized] = new PlayerHand(normalized, player);
            else
                _hands[normalized].SetDisplayName(player);

            if (!_turnOrder.Contains(normalized))
            {
                if (_dealer != null && _turnOrder.Contains(_dealer))
                    _turnOrder.Insert(_turnOrder.Count - 1, normalized);
                else
                    _turnOrder.Add(normalized);
            }

            var hand = _hands[normalized];
            if (hand.IsBusted || _playersDone.Contains(normalized))
                return;

            hand.AddCard(roll);
            _log.Debug($"{normalized} hand: {string.Join(", ", hand.Cards)} = {hand.Score}");

            if (hand.Score > 21)
            {
                hand.IsBusted = true;
                _playersDone.Add(normalized);
                _log.Debug($"{normalized} busted!");
                NextTurn();
            }
            else if (hand.Score == 21)
            {
                _playersDone.Add(normalized);
                _log.Debug($"{normalized} hit 21!");
                NextTurn();
            }
        }

        public void PlayerStands(string player)
        {
            if (!RoundActive || RoundOver)
                return;

            var normalized = NormalizeName(player);

            if (!_turnOrder.Contains(normalized) || _playersDone.Contains(normalized))
                return;

            _playersDone.Add(normalized);
            _log.Debug($"{normalized} stands with score {_hands.GetValueOrDefault(normalized)?.Score ?? 0}");

            if (normalized == CurrentPlayer)
                NextTurn();
        }

        private void NextTurn()
        {
            bool allNonDealerDone = _turnOrder
                .Where(p => p != _dealer)
                .All(p => _playersDone.Contains(p));

            if (allNonDealerDone && _dealer != null && !_playersDone.Contains(_dealer))
            {
                _currentPlayerIndex = _turnOrder.IndexOf(_dealer);
                return;
            }

            int safety = 0;
            do
            {
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _turnOrder.Count;
                safety++;
                if (safety > _turnOrder.Count)
                {
                    RoundOver = true;
                    return;
                }
            } while (_playersDone.Contains(CurrentPlayer));

            if (_turnOrder.All(p => _playersDone.Contains(p)))
                RoundOver = true;
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

        public bool IsPlayerDone(string player) => _playersDone.Contains(NormalizeName(player));

        private string NormalizeName(string name) =>
            name.Trim().ToLowerInvariant();

        // Announce winners once using stored message
        public void AnnounceWinnersToParty()
        {
            if (string.IsNullOrEmpty(_lastWinnerMessage))
                return;

            _chat.Print(_lastWinnerMessage);
            _log.Debug($"Announced winners: {_lastWinnerMessage}");
        }

        // Expose stored last winner message for UI copy button
        public string GetLastWinnerMessage() => _lastWinnerMessage;
    }
}

