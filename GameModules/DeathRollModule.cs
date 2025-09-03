using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Blackjack.GameModules
{
    public class DeathrollModule : IGameModule
    {
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;
        private readonly IDalamudPluginInterface _pluginInterface;

        public string? CurrentPlayer => null;

        // Store party player names as (normalized, display)
        private List<(string NormalizedName, string DisplayName)> _partyPlayersWithDisplayNames = new();
        private List<(string player, int roll)> _rollHistory = new();

        public bool RoundActive { get; private set; } = false;
        public bool RoundOver { get; private set; } = false;

        public string Name => "Deathroll";

        public string? Opponent { get; set; } = null;
        public int StartingMax { get; set; } = 1000;
        public int CurrentMax { get; private set; } = 1000;
        public string? Winner { get; private set; } = null;

        public IReadOnlyList<string> PartyPlayers => _partyPlayersWithDisplayNames.Select(p => p.NormalizedName).ToList();
        public IReadOnlyList<(string NormalizedName, string DisplayName)> PartyPlayersWithDisplayNames => _partyPlayersWithDisplayNames;
        public IReadOnlyList<string> Winners => Winner != null ? new List<string> { Winner } : new List<string>();

        public DeathrollModule(IPluginLog log, IChatGui chat, IDalamudPluginInterface pluginInterface)
        {
            _log = log;
            _chat = chat;
            _pluginInterface = pluginInterface;
        }

        public string? Dealer
        {
            get => null;
            set { }
        }

        public void PlayerStands(string player) { }

        public IReadOnlyDictionary<string, PlayerHand> GetHands() => new Dictionary<string, PlayerHand>();
        public IReadOnlyDictionary<string, PlayerStatus> GetPlayerStatuses() => new Dictionary<string, PlayerStatus>();

        public string GetLastWinnerMessage()
        {
            return Winner != null
                ? $"[Deathroll] Winner: {Winner}"
                : "[Deathroll] No winner yet.";
        }

        public void UpdatePartyPlayers(IEnumerable<string> displayNames)
        {
            _partyPlayersWithDisplayNames = displayNames
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => (NormalizePlayerName(p), p.Trim()))
                .Distinct()
                .ToList();
        }

        public void StartRound()
        {
            RoundActive = true;
            RoundOver = false;
            Winner = null;
            _rollHistory.Clear();
            CurrentMax = StartingMax;

            _log.Debug("[Deathroll] Round started.");
            _chat.Print("[Deathroll] Round started.");
        }

        public void EndRound()
        {
            RoundActive = false;
            RoundOver = true;

            if (Winner != null)
            {
                _chat.Print($"[Deathroll] Winner: {Winner}");
                _log.Debug($"[Deathroll] Winner: {Winner}");
            }
            else
            {
                _chat.Print("[Deathroll] Round ended with no winner.");
                _log.Debug("[Deathroll] Round ended with no winner.");
            }
        }

        public void AddRoll(string displayName, int roll)
        {
            if (!RoundActive || RoundOver)
                return;

            var normalized = NormalizePlayerName(displayName);

            var matched = _partyPlayersWithDisplayNames.FirstOrDefault(p => p.NormalizedName == normalized);
            if (string.IsNullOrEmpty(matched.DisplayName))
            {
                _log.Debug($"[Deathroll] Player '{displayName}' not in party (normalized: {normalized}).");
                return;
            }

            // Skip duplicate rolls: same player rolling same value already recorded
            if (_rollHistory.Any(r => r.player == matched.DisplayName && r.roll == roll))
                return;

            if (roll < 1 || roll > CurrentMax)
            {
                _log.Debug($"[Deathroll] Invalid roll {roll} by {displayName}. Must be between 1 and {CurrentMax}.");
                return;
            }

            _rollHistory.Add((matched.DisplayName, roll));
            _log.Debug($"[Deathroll] {matched.DisplayName} rolled {roll} (max was {CurrentMax}).");

            if (roll == 1)
            {
                Winner = matched.DisplayName;
                RoundOver = true;
                RoundActive = false;
                _chat.Print($"[Deathroll] {matched.DisplayName} lost the deathroll!");
                return;
            }

            CurrentMax = roll;
            _chat.Print($"[Deathroll] Next roll max is {CurrentMax}");
        }

        public List<(string player, int roll)> GetRollHistory() => _rollHistory;

        public bool IsPlayerDone(string player)
        {
            return Winner != null && NormalizePlayerName(player) == NormalizePlayerName(Winner);
        }

       public static string NormalizePlayerName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return "";

    var trimmed = name.Trim();

    // Remove any leading special characters
    while (trimmed.Length > 0 && (trimmed[0] < 32 || !char.IsLetterOrDigit(trimmed[0])))
        trimmed = trimmed.Substring(1);

    // Insert a space between lowercase-uppercase transitions
    var split = System.Text.RegularExpressions.Regex.Replace(trimmed, "([a-z])([A-Z])", "$1 $2");

    // Extract up to first two words (assumed to be first and last name)
    var parts = split.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 2)
        trimmed = $"{parts[0]} {parts[1]}";
    else
        trimmed = parts.FirstOrDefault() ?? "";

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
        "Shiva","Twintania","Lich","Odin","Zodiark",
        "Bismarck","Ravana","Sephirot","Sophia","Zurvan"
    };

    // Check if last word is a known world and remove it
    var nameParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    if (nameParts.Count > 2 && knownWorlds.Contains(nameParts[^1]))
        nameParts.RemoveAt(nameParts.Count - 1);

    return string.Join(" ", nameParts).ToLowerInvariant();
}

        public class PlayerStatus
        {
            public string NormalizedName { get; }
            public PlayerStatus(string normalizedName) => NormalizedName = normalizedName;
        }
    }
}
