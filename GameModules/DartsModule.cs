using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Gui;
using Dalamud.Logging;
using Blackjack.GameModules; // For IGameModule, PlayerHand
using Dalamud.Plugin.Services;
using Dalamud.Plugin;

namespace Blackjack.GameModules
{
    public class DartsModule : IGameModule
    {
        public string Name => "Darts 501";

        private readonly IPluginLog _log;
        private readonly IChatGui _chat;

        private readonly Dictionary<string, string> _playerDisplayNames = new();
        private readonly HashSet<string> _knownWorlds = new(StringComparer.OrdinalIgnoreCase)
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

        public Dictionary<string, int> PlayerScores = new();
        public Dictionary<string, List<int>> PlayerRoundScores = new();
        public Dictionary<string, int> RoundScoreBackup = new();
        public IReadOnlyDictionary<string, string> DisplayNames => _playerDisplayNames;

        public bool IsTeamMode = false;
        public List<string> TeamA = new();
        public List<string> TeamB = new();
        public int TeamAScore = 501;
        public int TeamBScore = 501;

        // UPDATED: Store dynamic team names here with defaults
        public string TeamAName { get; private set; } = "Team A";
        public string TeamBName { get; private set; } = "Team B";

        public Dictionary<string, string> PlayerTeams { get; private set; } = new();

        public List<string> PartyPlayers { get; private set; } = new();
        private bool _roundActive = false;
        public bool RoundActive => _roundActive;
        public bool RoundOver => !_roundActive;

        private string? _dealer = null;
        public string? Dealer { get => _dealer; set => _dealer = value; }

        public string CurrentPlayer => "";

        private string? _winner = null;
        public string? Winner { get => _winner; set => _winner = value; }

        public IReadOnlyList<string> Winners => _winner != null ? new List<string> { _winner } : Array.Empty<string>();

        public IReadOnlyList<(string NormalizedName, string DisplayName)> PartyPlayersWithDisplayNames
            => _playerDisplayNames.Select(kvp => (kvp.Key, kvp.Value)).ToList();

        private string? _currentThrowingPlayer = null;
        private List<int> _currentDartThrows = new();

        public string CurrentThrowingPlayer => _currentThrowingPlayer ?? "";
        public List<int> GetCurrentDartThrows() => new List<int>(_currentDartThrows);

        public DartsModule(IPluginLog log, IChatGui chat)
        {
            _log = log;
            _chat = chat;
        }

        public void UpdatePartyPlayers(IEnumerable<string> players)
        {
            var playerList = players.ToList();
            PartyPlayers = playerList;

            if (!_roundActive)
            {
                foreach (var p in PartyPlayers)
                {
                    if (!PlayerScores.ContainsKey(p))
                    {
                        PlayerScores[p] = 501;
                        PlayerRoundScores[p] = new List<int>();
                    }
                }
            }
        }

        public void StartPlayerTurn(string playerName)
        {
            _currentThrowingPlayer = playerName;
            _currentDartThrows.Clear();
        }

        public void ProcessDartThrows(string playerName, List<int> throws)
        {
            _log.Debug($"[Darts] ProcessDartThrows called for player {playerName} with throws: {string.Join(", ", throws)}");

            if (!_roundActive)
            {
                _log.Debug("[Darts] Round not active; ignoring throws.");
                return;
            }

            if (throws.Count != 3)
            {
                _log.Debug("[Darts] Throws count not 3; ignoring.");
                return;
            }

            if (!PlayerScores.ContainsKey(playerName))
            {
                _log.Debug($"[Darts] Player {playerName} not found in PlayerScores.");
                return;
            }

            int total = throws.Sum();

            if (IsTeamMode)
            {
                if (PlayerTeams.TryGetValue(playerName, out var team))
                {
                    if (team == TeamAName)
                    {
                        int newScore = TeamAScore - total;
                        if (newScore >= 0)
                        {
                            TeamAScore = newScore;
                            _log.Debug($"[Darts] {TeamAName} turn total {total} accepted. New total: {newScore}");
                            if (newScore == 0)
                                _winner = TeamAName;
                        }
                        else
                        {
                            _log.Debug($"[Darts] {TeamAName} turn total {total} would drop below 0. Ignored.");
                        }
                    }
                    else if (team == TeamBName)
                    {
                        int newScore = TeamBScore - total;
                        if (newScore >= 0)
                        {
                            TeamBScore = newScore;
                            _log.Debug($"[Darts] {TeamBName} turn total {total} accepted. New total: {newScore}");
                            if (newScore == 0)
                                _winner = TeamBName;
                        }
                        else
                        {
                            _log.Debug($"[Darts] {TeamBName} turn total {total} would drop below 0. Ignored.");
                        }
                    }
                }
            }
            else
            {
                int currentScore = PlayerScores[playerName];
                int newScore = currentScore - total;

                if (newScore < 0)
                {
                    _log.Debug($"[Darts] {playerName} round total {total} would drop below 0. Ignored. Score remains {currentScore}");
                    return;
                }

                PlayerScores[playerName] = newScore;

                if (!PlayerRoundScores.ContainsKey(playerName))
                    PlayerRoundScores[playerName] = new List<int>();

                PlayerRoundScores[playerName].Add(total);

                _log.Debug($"[Darts] {playerName} round score {total} accepted. New total: {newScore}");

                if (newScore == 0)
                {
                    _winner = playerName;
                    _log.Debug($"[Darts] {playerName} has won the game!");
                }
            }

            _log.Debug($"[Darts] {playerName} full turn: {string.Join(", ", throws)} = {total}");

            if (_currentThrowingPlayer == playerName)
            {
                _currentDartThrows.Clear();
                _currentThrowingPlayer = null;
            }
        }

        public void PlayerStands(string playerName) { }

        public bool IsPlayerDone(string playerName) => _winner != null && _winner == playerName;

        public string GetLastWinnerMessage() => _winner != null ? $"{_winner} wins!" : "";

        public IReadOnlyDictionary<string, PlayerHand> GetHands() => new Dictionary<string, PlayerHand>();

        public void ToggleTeamMode()
        {
            IsTeamMode = !IsTeamMode;
            if (IsTeamMode)
            {
                TeamA.Clear();
                TeamB.Clear();
                TeamAScore = 501;
                TeamBScore = 501;
                PlayerTeams.Clear();
                // Reset team names to defaults on toggle
                TeamAName = "Team A";
                TeamBName = "Team B";
            }
        }

        public void AssignPlayerToTeam(string normalizedPlayerName, string teamName)
        {
            PlayerTeams[normalizedPlayerName] = teamName;

            if (teamName == TeamAName)
            {
                if (!TeamA.Contains(normalizedPlayerName))
                {
                    TeamA.Add(normalizedPlayerName);
                    TeamB.Remove(normalizedPlayerName);
                }
            }
            else if (teamName == TeamBName)
            {
                if (!TeamB.Contains(normalizedPlayerName))
                {
                    TeamB.Add(normalizedPlayerName);
                    TeamA.Remove(normalizedPlayerName);
                }
            }
        }

        // NEW: Allow UI or other to update team names dynamically
        public void UpdateTeamName(int teamId, string newName)
        {
            if (teamId == 0 && !string.IsNullOrWhiteSpace(newName))
            {
                string oldName = TeamAName;
                TeamAName = newName.Trim();

                foreach (var kv in PlayerTeams.Where(kv => kv.Value == oldName).ToList())
                    PlayerTeams[kv.Key] = TeamAName;
            }
            else if (teamId == 1 && !string.IsNullOrWhiteSpace(newName))
            {
                string oldName = TeamBName;
                TeamBName = newName.Trim();

                foreach (var kv in PlayerTeams.Where(kv => kv.Value == oldName).ToList())
                    PlayerTeams[kv.Key] = TeamBName;
            }
        }

        public Dictionary<string, string> GetPlayerStatuses()
        {
            var dict = new Dictionary<string, string>();
            foreach (var player in PartyPlayers)
            {
                if (PlayerTeams.TryGetValue(player, out var team))
                    dict[player] = team;
                else
                    dict[player] = IsTeamMode ? TeamAName : "";
            }
            return dict;
        }

        public void StartRound()
        {
            _roundActive = true;
            _winner = null;

            foreach (var rawName in PartyPlayers)
            {
                var (normalized, display) = NormalizeWithDisplayName(rawName);

                if (!PlayerScores.ContainsKey(normalized))
                {
                    PlayerScores[normalized] = 501;
                    _log.Debug($"[Darts] Initialized {normalized} with score 501");
                }

                if (!PlayerRoundScores.ContainsKey(normalized))
                    PlayerRoundScores[normalized] = new List<int>();

                RoundScoreBackup[normalized] = PlayerScores[normalized];
                PlayerRoundScores[normalized].Clear();
                UpdateDisplayNameMapping(normalized, display);
            }
        }

        public void EndRound()
        {
            _roundActive = false;

            if (IsTeamMode)
            {
                int teamARoundTotal = TeamA.Where(PlayerScores.ContainsKey).Sum(p => RoundScoreBackup[p] - PlayerScores[p]);
                int teamBRoundTotal = TeamB.Where(PlayerScores.ContainsKey).Sum(p => RoundScoreBackup[p] - PlayerScores[p]);

                int newTeamAScore = TeamAScore - teamARoundTotal;
                int newTeamBScore = TeamBScore - teamBRoundTotal;

                if (newTeamAScore >= 0)
                    TeamAScore = newTeamAScore;
                if (newTeamBScore >= 0)
                    TeamBScore = newTeamBScore;

                if (TeamAScore == 0)
                    _winner = TeamAName;
                if (TeamBScore == 0)
                    _winner = TeamBName;
            }

            RoundScoreBackup.Clear();
        }

        public Dictionary<string, int> GetScores() => new(PlayerScores);
        public Dictionary<string, List<int>> GetRoundScores() => new(PlayerRoundScores);

        public void ResetGame()
        {
            PlayerScores.Clear();
            PlayerRoundScores.Clear();
            RoundScoreBackup.Clear();
            TeamA.Clear();
            TeamB.Clear();
            PlayerTeams.Clear();
            TeamAScore = 501;
            TeamBScore = 501;
            _winner = null;
            _roundActive = false;

            // Reset team names to default on reset as well
            TeamAName = "Team A";
            TeamBName = "Team B";

            foreach (var rawName in PartyPlayers)
            {
                var (normalized, display) = NormalizeWithDisplayName(rawName);
                PlayerScores[normalized] = 501;
                PlayerRoundScores[normalized] = new List<int>();
                UpdateDisplayNameMapping(normalized, display);
            }
        }

        public (string Normalized, string DisplayName) NormalizeWithDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ("", "");

            var trimmed = name.Trim();

            while (trimmed.Length > 0 && (trimmed[0] < 32 || !char.IsLetterOrDigit(trimmed[0])))
                trimmed = trimmed.Substring(1);

            var split = Regex.Replace(trimmed, "([a-z])([A-Z])", "$1 $2");
            var parts = split.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string combined = string.Join(" ", parts);
            foreach (var world in _knownWorlds)
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

        public string NormalizeName(string name) => NormalizeWithDisplayName(name).Normalized;

        public void UpdateDisplayNameMapping(string normalizedName, string displayName)
        {
            if (string.IsNullOrEmpty(normalizedName) || string.IsNullOrEmpty(displayName))
                return;

            _playerDisplayNames[normalizedName] = displayName;
        }

        public string GetDisplayName(string normalizedName) =>
            _playerDisplayNames.TryGetValue(normalizedName, out var displayName) ? displayName : normalizedName;

        public void ClearCurrentDartThrows()
        {
            _currentDartThrows.Clear();
        }

        public void AddRoll(string playerName, int roll)
        {
            if (_currentThrowingPlayer != playerName)
            {
                _log.Debug($"[Darts] Ignoring roll for {playerName} — it's currently {_currentThrowingPlayer}'s turn.");
                return;
            }

            _currentDartThrows.Add(roll);
            _log.Debug($"[Darts] {playerName} threw dart: {roll}");

            if (_currentDartThrows.Count == 3)
            {
                ProcessDartThrows(playerName, new List<int>(_currentDartThrows));
            }
        }
    }
}
