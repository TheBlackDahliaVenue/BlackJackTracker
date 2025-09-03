using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Blackjack.GameModules
{
    // ... (usings and namespace remain unchanged)

public class BeerPongModule : IGameModule
{
    private readonly Dictionary<string, PlayerStatus> _players = new();
    private readonly Dictionary<string, TeamStatus> _teams = new();
    private readonly IPluginLog _log;
    private readonly IChatGui _chat;
    private readonly List<(string player, int roll)> _rollHistory = new();

    public bool IsTeamMode { get; private set; } = false;
    public string? CurrentPlayer => null;

    private List<string> _partyPlayers = new();
    private List<string> _winners = new();

    private readonly int[] _drunkTargets = new[] { 65, 70, 75, 80, 85, 90 };
    private static readonly Random _rng = new();

    public Dictionary<string, string> PlayerTeams { get; } = new();

    public string Name => "Beer Pong";

    public IReadOnlyList<string> PartyPlayers => _partyPlayers;
    public IReadOnlyList<string> Winners => _winners;

    public BeerPongModule(IPluginLog log, IChatGui chat, IDalamudPluginInterface pluginInterface)
    {
        _log = log;
        _chat = chat;
    }

    public string? Dealer { get => null; set { } }

    public IReadOnlyList<(string NormalizedName, string DisplayName)> PartyPlayersWithDisplayNames
        => _players.Values.Select(p => (p.NormalizedName, p.DisplayName)).ToList();

    public void PlayerStands(string player) { }

    public IReadOnlyDictionary<string, PlayerHand> GetHands() => new Dictionary<string, PlayerHand>();

    public string GetLastWinnerMessage() =>
        _winners.Count > 0
            ? $"[Beer Pong] Winner(s): {string.Join(", ", _winners)}!"
            : "[Beer Pong] Game finished.";

    public static string NormalizePlayerName(string name)

{
    if (string.IsNullOrWhiteSpace(name))
        return "";

    string stripped = StripWorldFromName(name).Trim();

    while (stripped.Length > 0 && (stripped[0] < 32 || !char.IsLetterOrDigit(stripped[0])))
        stripped = stripped.Substring(1);

    return stripped.ToLowerInvariant();
}

    public void UpdatePartyPlayers(IEnumerable<string> players)
    {
        var playerList = players.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct().ToList();
        _partyPlayers = playerList.Select(NormalizePlayerName).ToList();

        foreach (var key in _players.Keys.ToList())
{
    if (!_partyPlayers.Contains(key))
    {
        if (_players[key].Team is TeamStatus team)
        {
            team.Players.Remove(_players[key]);
            PlayerTeams.Remove(key);
        }
        _players.Remove(key);
    }
}

        foreach (var p in playerList)
        {
            var norm = NormalizePlayerName(p);
            if (!_players.ContainsKey(norm))
            {
                var newPlayer = new PlayerStatus(p);
                _players[norm] = newPlayer;

                if (IsTeamMode)
                {
                    PlayerTeams[norm] = "";
                    newPlayer.Team = null;
                }
            }
        }
    }

    public void AssignPlayerToTeam(string player, string teamName)
    {
        var norm = NormalizePlayerName(player);
        if (!_players.TryGetValue(norm, out var status))
            return;

if (status.Team is TeamStatus team)
{
    team.Players.Remove(status);
    if (team.Players.Count == 0)
        _teams.Remove(team.TeamName);
}
        if (!_teams.ContainsKey(teamName))
            _teams[teamName] = new TeamStatus(teamName);

        status.Team = _teams[teamName];
        _teams[teamName].Players.Add(status);
        PlayerTeams[norm] = teamName;
    }

    public void ClearTeamAssignments()
    {
        foreach (var team in _teams.Values)
        {
            team.Players.Clear();
            team.IsOut = false;
            team.CupsLeft = 0;
        }
        _teams.Clear();

        foreach (var player in _players.Values)
            player.Team = null;

        PlayerTeams.Clear();
    }

    public void EnableTeamMode(bool enable)
    {
        if (IsTeamMode == enable)
            return;

        IsTeamMode = enable;

        ClearTeamAssignments();

        foreach (var player in _players.Values)
        {
            player.IsOut = false;
            player.DrinksConsumed = 0;

            if (enable)
            {
                player.CupsLeft = 5;
                PlayerTeams[player.NormalizedName] = "";
            }
            else
            {
                player.CupsLeft = 10;
                PlayerTeams.Remove(player.NormalizedName);
            }
        }
    }

    public void StartRound()
    {
        RoundActive = true;
        RoundOver = false;
        _winners.Clear();
        _rollHistory.Clear();

        foreach (var player in _players.Values)
        {
            player.DrinksConsumed = 0;
            player.IsOut = false;
            player.CupsLeft = IsTeamMode ? 5 : 10;
        }

        if (IsTeamMode)
        {
            foreach (var team in _teams.Values)
            {
                team.CupsLeft = team.Players.Count * 5;
                team.IsOut = false;
            }
        }

        _log.Debug("[Beer Pong] Round started.");
        _chat.Print("[Beer Pong] Round started.");
    }

    public void EndRound()
    {
        RoundActive = false;
        RoundOver = true;
        _rollHistory.Clear();

        _winners = IsTeamMode
            ? _teams.Values.Where(t => !t.IsOut).Select(t => t.TeamName).ToList()
            : _players.Values.Where(p => !p.IsOut).Select(p => p.DisplayName).ToList();

        if (_winners.Count > 0)
        {
            var winnerNames = string.Join(", ", _winners);
            _chat.Print($"[Beer Pong] Winner(s): {winnerNames}");
            _log.Debug($"[Beer Pong] Winners: {winnerNames}");
        }
        else
        {
            _chat.Print("[Beer Pong] No winners this round.");
            _log.Debug("[Beer Pong] No winners this round.");
        }
    }

    public void AddRoll(string player, int roll)
    {
        if (!RoundActive || RoundOver)
            return;

        var norm = NormalizePlayerName(player);
        if (!_players.TryGetValue(norm, out var shooter) || shooter.IsOut)
            return;

        if (_rollHistory.Any(r => r.player == norm && r.roll == roll))
            return;

        _rollHistory.Add((norm, roll));

        int targetIndex = Math.Min(shooter.DrinksConsumed, _drunkTargets.Length - 1);
        int target = _drunkTargets[targetIndex];
        bool hit = roll >= target;

        if (hit)
        {
            if (IsTeamMode)
            {
                if (shooter.Team == null)
                {
                    _chat.Print($"[Beer Pong] {shooter.DisplayName} is not assigned to any team!");
                    return;
                }

                var opposingTeams = _teams.Values.Where(t => t != shooter.Team && !t.IsOut).ToList();
                if (!opposingTeams.Any())
                {
                    EndRound();
                    return;
                }

                var targetTeam = opposingTeams.First();
                targetTeam.CupsLeft--;

                // Pick random alive player from target team
                var candidates = targetTeam.Players.Where(p => !p.IsOut && p.CupsLeft > 0).ToList();
                if (candidates.Count > 0)
                {
                    var unlucky = candidates[_rng.Next(candidates.Count)];
                    unlucky.DrinksConsumed++;
                    unlucky.CupsLeft--;

                    _chat.Print($"[Beer Pong] {shooter.DisplayName} hits! Team {targetTeam.TeamName}'s {unlucky.DisplayName} drinks! ({targetTeam.CupsLeft} team cups left, {unlucky.CupsLeft} player cups left)");

                    if (unlucky.CupsLeft <= 0)
                    {
                        unlucky.IsOut = true;
                        _chat.Print($"[Beer Pong] {unlucky.DisplayName} is out!");
                    }
                }
                else
                {
                    _chat.Print($"[Beer Pong] {shooter.DisplayName} hits! Team {targetTeam.TeamName} drinks! ({targetTeam.CupsLeft} cups left)");
                }

                if (targetTeam.CupsLeft <= 0)
                {
                    targetTeam.IsOut = true;
                    _chat.Print($"[Beer Pong] Team {targetTeam.TeamName} is out!");

                    if (_teams.Values.Count(t => !t.IsOut) <= 1)
                        EndRound();
                }
            }
            else
            {
                var opponents = _players.Values.Where(p => p.NormalizedName != norm && !p.IsOut).ToList();
                if (!opponents.Any())
                {
                    EndRound();
                    return;
                }

                var targetOpponent = opponents.First();
                targetOpponent.CupsLeft--;
                targetOpponent.DrinksConsumed++;

                _chat.Print($"[Beer Pong] {shooter.DisplayName} hits! {targetOpponent.DisplayName} drinks! ({targetOpponent.CupsLeft} cups left)");

                if (targetOpponent.CupsLeft <= 0)
                {
                    targetOpponent.IsOut = true;
                    _chat.Print($"[Beer Pong] {targetOpponent.DisplayName} is out!");
                    if (_players.Values.Count(p => !p.IsOut) <= 1)
                        EndRound();
                }
            }
        }
        else
		{
			shooter.DrinksConsumed++;
			shooter.CupsLeft--;

			string message = $"[Beer Pong] {shooter.DisplayName} missed and drinks! ({shooter.CupsLeft} cups left, {shooter.DrinksConsumed} drinks consumed)";

			if (IsTeamMode && shooter.Team != null)
			{
				shooter.Team.CupsLeft--;

				message += $" Team {shooter.Team.TeamName} also loses a cup! ({shooter.Team.CupsLeft} cups left)";

				if (shooter.Team.CupsLeft <= 0)
				{
					shooter.Team.IsOut = true;
					_chat.Print($"[Beer Pong] Team {shooter.Team.TeamName} is out!");

					if (_teams.Values.Count(t => !t.IsOut) <= 1)
					{
						EndRound();
						return;
					}
				}
			}

			_chat.Print(message);

			if (shooter.CupsLeft <= 0)
			{
				shooter.IsOut = true;
				_chat.Print($"[Beer Pong] {shooter.DisplayName} is out!");

				if (!IsTeamMode && _players.Values.Count(p => !p.IsOut) <= 1)
				{
					EndRound();
				}
			}
		}
	}


    public IReadOnlyDictionary<string, PlayerStatus> GetPlayerStatuses() => _players;

    public bool IsPlayerDone(string player)
    {
        var normalized = NormalizePlayerName(player);
        return _players.TryGetValue(normalized, out var status) && status.IsOut;
    }

    public bool RoundActive { get; private set; } = false;
    public bool RoundOver { get; private set; } = false;

    public class PlayerStatus
    {
        public string NormalizedName { get; }
        public string DisplayName { get; }
        public int DrinksConsumed { get; set; }
        public int CupsLeft { get; set; }
        public bool IsOut { get; set; }
        public TeamStatus? Team { get; set; }

        public PlayerStatus(string displayName)
        {
            DisplayName = StripWorldFromName(displayName);
            NormalizedName = NormalizePlayerName(displayName);
            DrinksConsumed = 0;
            CupsLeft = 10;
            IsOut = false;
        }
    }

    public class TeamStatus
    {
        public string TeamName { get; }
        public int CupsLeft { get; set; }
        public bool IsOut { get; set; }
        public List<PlayerStatus> Players { get; } = new();

        public TeamStatus(string teamName)
        {
            TeamName = teamName;
            IsOut = false;
            CupsLeft = 0;
        }
    }
	
	private static string StripWorldFromName(string fullName)
{
    if (string.IsNullOrWhiteSpace(fullName))
        return fullName;

    var parts = fullName.Split(' ');
    if (parts.Length < 2)
        return fullName;

    string first = parts[0];
    string last = parts[1];

    string[] knownWorlds = new[]
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

    foreach (var world in knownWorlds)
    {
        if (last.EndsWith(world, StringComparison.OrdinalIgnoreCase))
        {
            string cleanLast = last.Substring(0, last.Length - world.Length);
            return $"{first} {cleanLast}".Trim();
        }
    }

    return fullName;
}

}

}
