using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Blackjack.GameModules;

namespace Blackjack
{
    public class BeerPongUI
    {
        private bool _visible = true;
        private IGameModule _module;
        private readonly List<IGameModule> _modules;
        private readonly Action<IGameModule> _onModuleChanged;
        private readonly Action _updatePartyPlayers;

        private Dictionary<int, string> _teamNames = new Dictionary<int, string> { { 0, "Team 1" }, { 1, "Team 2" } };

        public BeerPongUI(IGameModule initialModule, List<IGameModule> modules, Action<IGameModule> onModuleChanged, Action updatePartyPlayers)
        {
            _module = initialModule;
            _modules = modules;
            _onModuleChanged = onModuleChanged;
            _updatePartyPlayers = updatePartyPlayers;
        }

        public void SetGameModule(IGameModule module) => _module = module;
        public void ToggleVisible() => _visible = !_visible;

        public void Draw()
        {
            if (!_visible)
                return;

            ImGui.Begin($"{_module.Name} Game");

            ImGui.Text("Select Game Mode:");

            if (ImGui.BeginCombo("##gameModuleCombo", _module.Name))
            {
                foreach (var mod in _modules)
                {
                    bool isSelected = mod == _module;
                    if (ImGui.Selectable(mod.Name, isSelected))
                    {
                        if (!isSelected)
                        {
                            _module = mod;
                            _onModuleChanged?.Invoke(mod);
                            _updatePartyPlayers?.Invoke();
                        }
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.Separator();

            if (_module is not BeerPongModule beerPong)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "This game mode has no UI implementation yet.");
                ImGui.End();
                return;
            }

            if (!beerPong.RoundActive)
            {
                if (ImGui.Button(beerPong.IsTeamMode ? "Switch to 1v1 Mode" : "Switch to Team Mode"))
                {
                    beerPong.EnableTeamMode(!beerPong.IsTeamMode);
                }

                ImGui.SameLine();
                ImGui.Text($"Current Mode: {(beerPong.IsTeamMode ? "Team Beer Pong" : "1v1 Beer Pong")}");
            }

            ImGui.Separator();

            if (beerPong.IsTeamMode && !beerPong.RoundActive)
            {
                ImGui.Text("Team Assignments:");

                foreach (var teamId in _teamNames.Keys.ToList())
{
    string name = _teamNames[teamId];
    byte[] buf = new byte[64];
    Encoding.UTF8.GetBytes(name, 0, name.Length, buf, 0);

    if (ImGui.InputText($"##teamname{teamId}", buf, ImGuiInputTextFlags.None))
    {
        string updated = Encoding.UTF8.GetString(buf).TrimEnd('\0');

        if (updated.Length > 20)
            updated = updated[..20];

        string oldName = _teamNames[teamId];
        if (updated != oldName)
        {
            _teamNames[teamId] = updated;

            foreach (var kv in beerPong.PlayerTeams.Where(kv => kv.Value == oldName).ToList())
            {
                beerPong.AssignPlayerToTeam(kv.Key, updated);
            }
        }
    }
}

                ImGui.Separator();

                var playerStatuses = beerPong.GetPlayerStatuses();
                var teamOptions = _teamNames.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();

                foreach (var player in playerStatuses.Values.OrderBy(p => p.DisplayName))
                {
                    ImGui.Text($"{player.DisplayName}:");
                    ImGui.SameLine();

                    string currentTeam = beerPong.PlayerTeams.TryGetValue(player.NormalizedName, out var teamStr) ? teamStr : teamOptions[0];

                    if (ImGui.BeginCombo($"##teamcombo_{player.NormalizedName}", currentTeam))
                    {
                        foreach (var teamName in teamOptions)
                        {
                            bool isSelected = currentTeam == teamName;
                            if (ImGui.Selectable(teamName, isSelected))
                            {
                                beerPong.PlayerTeams[player.NormalizedName] = teamName;
                                beerPong.AssignPlayerToTeam(player.DisplayName, teamName);
                            }
                            if (isSelected)
                                ImGui.SetItemDefaultFocus();
                        }
                        ImGui.EndCombo();
                    }
                }
            }

            if (!beerPong.RoundActive)
            {
                ImGui.Text("Round is not active.");

                if (ImGui.Button("Start Round"))
                {
                    _updatePartyPlayers?.Invoke();
                    beerPong.StartRound();
                }

                if (beerPong.Winners.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), $"Winner(s): {string.Join(", ", beerPong.Winners)}");

                    ImGui.SameLine();

                    if (ImGui.Button("Copy Winner(s) to Party Chat"))
                    {
                        var message = beerPong.GetLastWinnerMessage();
                        if (!string.IsNullOrEmpty(message))
                        {
                            ImGui.SetClipboardText($"/p {message}");
                        }
                    }
                }
							 // === RULES SECTION ===
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Rules:");
            ImGui.BulletText("Must say actions in Party Chat.");
            ImGui.Bullet();
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "/dice 100");
			ImGui.SameLine(0, 0);
			ImGui.Text(" is the command to generate each throw for each player.");
			ImGui.Bullet();
			ImGui.Text("If a player rolls a ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "0");
			ImGui.SameLine(0, 0);
			ImGui.Text(" have them roll again.");
			ImGui.Bullet();			
			ImGui.Text("Each player gets ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "one throw");
			ImGui.SameLine(0, 0);
			ImGui.Text(" per round.");			
			ImGui.Bullet();	
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "In team mode, 5 drinks ");
			ImGui.SameLine(0, 0);
			ImGui.Text("are added to the team total per player.");
			ImGui.Bullet();
			ImGui.Text("In team mode if a player is");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " knocked out, ");
			ImGui.SameLine(0, 0);
			ImGui.Text("they can't roll for their team.");
			ImGui.Bullet();
			ImGui.Text("If");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " thrower ");
			ImGui.SameLine(0, 0);
			ImGui.Text("lands a hit,");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " other ");
			ImGui.SameLine(0, 0);
			ImGui.Text("player drinks.");
			ImGui.Bullet();
			ImGui.Text("If");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " thrower ");
			ImGui.SameLine(0, 0);
			ImGui.Text("misses a hit,");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " thrower ");
			ImGui.SameLine(0, 0);
			ImGui.Text("player drinks.");
			ImGui.Bullet();
			ImGui.Text("First player or team to reach");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " 0 ");
			ImGui.SameLine(0, 0);
			ImGui.Text("drinks left");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), " loses! ");
			ImGui.BulletText("In team mode, who drinks on what team is selected randomly.");
			ImGui.Bullet();
			ImGui.Text("The more drinks a player consumes, the harder it is to land a throw!");
			ImGui.Bullet();
			ImGui.Text("Rolls needed to land a hit for penalty of each drink consumed below: ");
			ImGui.Indent();
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "1 drink(s) = 65   ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "2 drink(s) = 70   ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "3 drink(s) = 75");
			ImGui.Unindent();
			ImGui.Indent();
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "4 drink(s) = 80   ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "5 drink(s) = 85   ");
			ImGui.SameLine(0, 0);
			ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "6 drink(s) = 90");
			ImGui.Unindent();
			
            }
            else
            {
                var statuses = beerPong.GetPlayerStatuses();

                ImGui.Separator();

                if (beerPong.IsTeamMode)
                {
                    var teams = statuses.Values
                        .Where(p => p.Team != null)
                        .GroupBy(p => p.Team!.TeamName);

                    foreach (var teamGroup in teams)
                    {
                        var team = teamGroup.First().Team!;
                        string teamName = team.TeamName;
                        int teamCups = team.CupsLeft;

                        string displayTeamName = teamName.Length > 20 ? teamName[..17] + "..." : teamName;

                        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
                        ImGui.TextWrapped($"Team ({displayTeamName}): Cups Left: {teamCups}");
                        ImGui.PopTextWrapPos();

                        ImGui.Text("Members:");
                        foreach (var player in teamGroup.OrderBy(p => p.DisplayName))
                        {
                            ImGui.Text($"  {player.DisplayName}: Cups Left: {player.CupsLeft} | Drinks: {player.DrinksConsumed}");
                        }

                        ImGui.Dummy(new Vector2(1f, 1f)); // spacing/layout fix
                        ImGui.Separator();
                    }
                }
                else
                {
                    foreach (var player in statuses.Values.OrderBy(p => p.NormalizedName))
                    {
                        bool isOut = player.IsOut;
                        bool isWinner = beerPong.Winners.Contains(player.NormalizedName);

                        Vector4 color = isWinner ? new Vector4(0f, 1f, 0f, 1f)
                                        : isOut ? new Vector4(0.6f, 0.6f, 0.6f, 1f)
                                        : new Vector4(1f, 1f, 1f, 1f);

                        ImGui.PushStyleColor(ImGuiCol.Text, color);

                        string outText = isOut ? " (Out)" : "";
                        ImGui.Text($"{player.DisplayName}: Cups Left: {player.CupsLeft} | Drinks: {player.DrinksConsumed}{outText}");

                        ImGui.PopStyleColor();
                    }
                }

                ImGui.Separator();

                if (beerPong.RoundOver)
                {
                    ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Round over! Click End Round to finish.");
                }

                if (ImGui.Button("End Round"))
                {
                    beerPong.EndRound();
                }
            }

            ImGui.End();
        }
    }
}
