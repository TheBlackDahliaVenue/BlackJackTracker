using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using Blackjack.GameModules;
using Dalamud.Logging;

namespace Blackjack;

public class DartsUI
{
    private DartsModule darts;
    private Dictionary<string, int> lastValidTeamAScore = new();
    private Dictionary<string, int> lastValidTeamBScore = new();
    private int teamATotalScore = 0;
    private int teamBTotalScore = 0;

    private List<IGameModule> _allModules;
    private Action<IGameModule> _onModuleChanged;
    private Action _updatePartyPlayersFromPartyList;

    public DartsUI(DartsModule dartsModule, List<IGameModule> allModules, Action<IGameModule> onModuleChanged, Action updatePartyPlayers)
    {
        darts = dartsModule;
        _allModules = allModules;
        _onModuleChanged = onModuleChanged;
        _updatePartyPlayersFromPartyList = updatePartyPlayers;
    }

    public void SetGameModule(IGameModule module)
    {
        if (module is DartsModule dm)
            darts = dm;
    }

    public void Draw()
{
    if (darts == null)
        return;

    ImGui.Begin($"{darts.Name} Game");

    // === GAME MODE SELECTOR ===
    ImGui.Text("Select Game Mode:");

    if (ImGui.BeginCombo("##gameModuleCombo", darts.Name))
    {
        foreach (var mod in _allModules)
        {
            bool isSelected = mod == darts;
            if (ImGui.Selectable(mod.Name, isSelected))
            {
                if (!isSelected)
                {
                    _onModuleChanged?.Invoke(mod);
                    _updatePartyPlayersFromPartyList?.Invoke();
                }
            }
            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
    }

    ImGui.Separator();

    // === EXISTING DARTS UI ===
    ImGui.Text("Mode: " + (darts.IsTeamMode ? "Teams" : "1v1"));
    if (ImGui.Button("Toggle Team Mode"))
    {
        darts.ToggleTeamMode();
        lastValidTeamAScore.Clear();
        lastValidTeamBScore.Clear();
        teamATotalScore = 0;
        teamBTotalScore = 0;
    }

    if (!darts.RoundActive)
    {
        if (ImGui.Button("Start Round"))
        {
            darts.StartRound();
            lastValidTeamAScore = darts.TeamA.ToDictionary(p => p, p => darts.PlayerScores.GetValueOrDefault(p, 501));
            lastValidTeamBScore = darts.TeamB.ToDictionary(p => p, p => darts.PlayerScores.GetValueOrDefault(p, 501));
        }

        if (ImGui.Button("Reset Game"))
            darts.ResetGame();
    }
    else
    {
        if (ImGui.Button("End Round"))
        {
            darts.EndRound();
            UpdateTeamTotals();
        }
    }

        ImGui.Separator();

        var currentPlayer = darts.CurrentThrowingPlayer;
        var currentDartThrows = darts.GetCurrentDartThrows();

        if (!string.IsNullOrEmpty(currentPlayer))
        {
            int currentScore = darts.PlayerScores.GetValueOrDefault(currentPlayer, 501);

            ImGui.Text($"Current Player: {darts.GetDisplayName(currentPlayer)}");
            ImGui.Text($"Current Total Score: {currentScore}");

            for (int i = 0; i < currentDartThrows.Count; i++)
            {
                ImGui.Text($"Dart #{i + 1}: {currentDartThrows[i]}");
            }

            if (currentDartThrows.Count == 3)
            {
                int turnSum = currentDartThrows.Sum();
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), $"Full turn: {string.Join(", ", currentDartThrows)} = {turnSum}");

                // Use the new unified method
                darts.ProcessDartThrows(currentPlayer, new List<int>(currentDartThrows));

                darts.ClearCurrentDartThrows();

                UpdateTeamTotals();
            }
        }
        else
        {
            ImGui.Text("No current player turn.");
        }

        ImGui.Separator();

        // === TEAM MODE UI ===
        if (darts.IsTeamMode && !darts.RoundActive)
        {
            ImGui.Text("Team Assignments:");
			ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "(Team name must be at least 5 characters long)");

            // Editable team names using the DartsModule properties and UpdateTeamName method
            for (int teamId = 0; teamId <= 1; teamId++)
            {
                string currentTeamName = teamId == 0 ? darts.TeamAName : darts.TeamBName;

                byte[] buf = new byte[64];
                Encoding.UTF8.GetBytes(currentTeamName, 0, currentTeamName.Length, buf, 0);

                ImGui.PushID($"teamname{teamId}");
                if (ImGui.InputText($"##teamname{teamId}", buf, ImGuiInputTextFlags.None))
                {
                    string updated = Encoding.UTF8.GetString(buf).TrimEnd('\0');
                    if (updated.Length > 20)
                        updated = updated[..20];

                    if (updated != currentTeamName)
                    {
                        darts.UpdateTeamName(teamId, updated);
                        // Clear and recalc team totals on name change
                        lastValidTeamAScore.Clear();
                        lastValidTeamBScore.Clear();
                        teamATotalScore = 0;
                        teamBTotalScore = 0;
                    }
                }
                ImGui.Text($"Team {teamId + 1} Name:");
                ImGui.SameLine();
                ImGui.Text(currentTeamName);
                ImGui.PopID();
            }

            ImGui.Spacing();

            // Dropdown to assign players to teams
            if (darts.PartyPlayers != null)
            {
                foreach (var player in darts.PartyPlayers.OrderBy(p => darts.GetDisplayName(p)))
                {
                    string normalized = darts.NormalizeName(player);
                    string displayName = darts.GetDisplayName(normalized);

                    darts.PlayerTeams.TryGetValue(normalized, out var currentTeam);

                    var teamsOptions = new List<string> { "None", darts.TeamAName, darts.TeamBName };
                    int currentIndex = teamsOptions.IndexOf(currentTeam ?? "None");
                    if (currentIndex == -1) currentIndex = 0;

                    ImGui.PushID($"teamassign_{normalized}");
                    ImGui.Text(displayName);
                    ImGui.SameLine(150);

                    if (ImGui.Combo("##teamcombo", ref currentIndex, teamsOptions.ToArray(), teamsOptions.Count))
                    {
                        string selectedTeam = teamsOptions[currentIndex];

                        if (selectedTeam == "None")
                        {
                            darts.PlayerTeams.Remove(normalized);
                            darts.TeamA.Remove(normalized);
                            darts.TeamB.Remove(normalized);
                        }
                        else
                        {
                            darts.AssignPlayerToTeam(normalized, selectedTeam);
                        }
                    }
                    ImGui.PopID();
                }
            }

            ImGui.Spacing();

            // List players in each team
            foreach (var teamName in new[] { darts.TeamAName, darts.TeamBName })
            {
                var playersInTeam = darts.PlayerTeams
                    .Where(kv => kv.Value == teamName)
                    .Select(kv => darts.GetDisplayName(kv.Key))
                    .OrderBy(name => name)
                    .ToList();

                if (playersInTeam.Count > 0)
                {
                    ImGui.Text($"{teamName} Players:");
                    foreach (var playerName in playersInTeam)
                    {
                        ImGui.BulletText(playerName);
                    }
                    ImGui.Spacing();
                }
            }

            ImGui.Separator();
        }

        // === SCORES UI ===
        ImGui.Separator();
        ImGui.Text("Scores:");

        if (darts.IsTeamMode)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), $"{darts.TeamAName} Total: {darts.TeamAScore}");
            foreach (var player in darts.TeamA)
            {
                int score = darts.PlayerScores.GetValueOrDefault(player, 501);
                ImGui.Text($"{darts.GetDisplayName(player)}: {score}");
                if (darts.PlayerRoundScores.TryGetValue(player, out var rounds) && rounds.Count > 0)
                {
                    string roundsStr = string.Join(", ", rounds);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Rounds: {roundsStr}");
                }
            }

            ImGui.Separator();

            ImGui.TextColored(new Vector4(0.2f, 0.8f, 1f, 1f), $"{darts.TeamBName} Total: {darts.TeamBScore}");
            foreach (var player in darts.TeamB)
            {
                int score = darts.PlayerScores.GetValueOrDefault(player, 501);
                ImGui.Text($"{darts.GetDisplayName(player)}: {score}");
                if (darts.PlayerRoundScores.TryGetValue(player, out var rounds) && rounds.Count > 0)
                {
                    string roundsStr = string.Join(", ", rounds);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Rounds: {roundsStr}");
                }
            }
        }
        else
        {
            foreach (var normalizedName in darts.DisplayNames.Keys.OrderBy(name => darts.DisplayNames[name]))
            {
                string displayName = darts.DisplayNames[normalizedName];
                int score = darts.PlayerScores.GetValueOrDefault(normalizedName, 501);

                ImGui.Text($"{displayName}: {score}");

                if (darts.PlayerRoundScores.TryGetValue(normalizedName, out var rounds) && rounds.Count > 0)
                {
                    string roundsStr = string.Join(", ", rounds);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Rounds: {roundsStr}");
                }
            }
        }

        if (!string.IsNullOrEmpty(darts.Winner))
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), $"Winner: {darts.Winner}!");
        }
		
			// === RULES SECTION ===
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Rules:");

            ImGui.BulletText("Must say actions in Party Chat.");

            ImGui.Bullet();
			ImGui.Text(" To log a throw, players must do ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "/dice 60");
			ImGui.Bullet();
            ImGui.Text(" Each player gets");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " 3 rolls ");
			ImGui.SameLine(0, 0);
			ImGui.Text("per round.");			
			ImGui.Bullet();
			ImGui.Text("The player/team will");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " bust ");
			ImGui.SameLine(0, 0);
			ImGui.Text("if they get more points than their current score.");						
			ImGui.Bullet();
			ImGui.Text("If player/team");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " busts ");
			ImGui.SameLine(0, 0);
			ImGui.Text("that round, their score");			
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " resets ");
			ImGui.SameLine(0, 0);
			ImGui.Text("back to the score they had at the beginning of the round.");				
			// Example section with indentation
			ImGui.BulletText("To win Players must:");
			ImGui.Indent();
			ImGui.Text("Hit 0 before their opponent does.");
			ImGui.Unindent();			
	
	
	
		
        ImGui.End();
    }

    private void UpdateTeamTotals()
    {
        if (!darts.IsTeamMode)
            return;

        bool allValid = true;

        foreach (var player in darts.TeamA)
        {
            if (darts.PlayerScores[player] > lastValidTeamAScore.GetValueOrDefault(player, 501))
            {
                allValid = false;
                break;
            }
        }
        foreach (var player in darts.TeamB)
        {
            if (darts.PlayerScores[player] > lastValidTeamBScore.GetValueOrDefault(player, 501))
            {
                allValid = false;
                break;
            }
        }

        if (allValid)
        {
            teamATotalScore = darts.TeamA.Sum(p => darts.PlayerScores.GetValueOrDefault(p, 501));
            teamBTotalScore = darts.TeamB.Sum(p => darts.PlayerScores.GetValueOrDefault(p, 501));
            lastValidTeamAScore = darts.TeamA.ToDictionary(p => p, p => darts.PlayerScores.GetValueOrDefault(p, 501));
            lastValidTeamBScore = darts.TeamB.ToDictionary(p => p, p => darts.PlayerScores.GetValueOrDefault(p, 501));
        }
        else
        {
            teamATotalScore = lastValidTeamAScore.Values.Sum();
            teamBTotalScore = lastValidTeamBScore.Values.Sum();
        }
    }

	
}
