using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Blackjack.GameModules;

namespace Blackjack
{
    public class DeathrollUI
    {
        private bool _visible = true;
        private IGameModule _module;
        private readonly List<IGameModule> _modules;
        private readonly Action<IGameModule> _onModuleChanged;
        private readonly Action _updatePartyPlayers;

        public DeathrollUI(IGameModule initialModule, List<IGameModule> modules, Action<IGameModule> onModuleChanged, Action updatePartyPlayers)
        {
            _module = initialModule;
            _modules = modules;
            _onModuleChanged = onModuleChanged;
            _updatePartyPlayers = updatePartyPlayers;
        }

        public void SetGameModule(IGameModule module)
        {
            _module = module;
        }

        public void ToggleVisible() => _visible = !_visible;

        public void Draw()
        {
            if (!_visible)
                return;

            ImGui.Begin($"{_module.Name} Game");

            ImGui.Text("Select Game Mode:");
            if (ImGui.BeginCombo("##GameModeCombo", _module.Name))
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

            if (_module is not DeathrollModule deathroll)
            {
                ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "This game mode has no UI implementation yet.");
                ImGui.End();
                return;
            }

            if (!deathroll.RoundActive)
            {
                ImGui.Text("Select Opponent:");
                var players = deathroll.PartyPlayersWithDisplayNames.ToArray(); // Use display names
                int selectedIndex = Array.FindIndex(players, p => p.NormalizedName == deathroll.Opponent);

                if (ImGui.BeginCombo("##opponent", selectedIndex >= 0 ? players[selectedIndex].DisplayName : "None"))
                {
                    if (ImGui.Selectable("None", selectedIndex == -1))
                    {
                        deathroll.Opponent = null;
                    }

                    for (int i = 0; i < players.Length; i++)
                    {
                        bool selected = (i == selectedIndex);
                        if (ImGui.Selectable(players[i].DisplayName, selected))
                        {
                            deathroll.Opponent = players[i].NormalizedName;
                        }

                        if (selected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                if (ImGui.Button("Start Duel") && deathroll.Opponent != null)
                {
                    deathroll.StartRound();
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), $"Current Max: {deathroll.CurrentMax}");

                ImGui.Separator();

                foreach (var (player, roll) in deathroll.GetRollHistory())
                {
                    ImGui.Text($"{player} rolled {roll}");
                }

                if (deathroll.RoundOver)
                {
                    ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), $"{deathroll.Winner} wins!");
                    if (ImGui.Button("End Round"))
                        deathroll.EndRound();
                }
            }
			
			// === RULES SECTION ===
            ImGui.Separator();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Rules:");

            ImGui.BulletText("Must say actions in Party Chat.");

            ImGui.Bullet();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "First roll is /dice by the player going first, each roll after is /dice #");
            ImGui.SameLine(0, 0);
            ImGui.Text(" for each player.");
			ImGui.Bullet();
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Challenger always rolls first!");
            ImGui.BulletText("Each roll lowers the max roll for the next player.");
			// Example section with indentation
			ImGui.BulletText("Example:");
			ImGui.Indent();
			ImGui.Text("Player rolls a 892!");
			ImGui.Text("Next player does /dice 892!");
			ImGui.Unindent();			
            ImGui.BulletText("The first player to roll a 1 loses.");


            ImGui.End();
        }
    }
}
