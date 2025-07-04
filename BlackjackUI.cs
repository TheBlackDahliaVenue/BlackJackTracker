using ImGuiNET;
using System.Numerics;
using System.Linq;
using System;

namespace Blackjack;

public class BlackjackUI
{
    private bool _visible = true;
    private readonly BlackjackManager _manager;
    private readonly Action _updatePartyPlayers;

    public BlackjackUI(BlackjackManager manager, Action updatePartyPlayers)
    {
        _manager = manager;
        _updatePartyPlayers = updatePartyPlayers;
    }

    public void ToggleVisible() => _visible = !_visible;

    public void Draw()
    {
        if (!_visible) return;

        ImGui.Begin("Blackjack Game");

        if (!_manager.RoundActive)
        {
            ImGui.Text("Select Dealer:");

            var partyPlayers = _manager.PartyPlayersWithDisplayNames.ToArray();

            if (partyPlayers.Length > 0)
            {
                int currentDealerIndex = -1;
                if (_manager.Dealer != null)
                    currentDealerIndex = Array.FindIndex(partyPlayers, p => p.NormalizedName == _manager.Dealer);

                if (ImGui.BeginCombo("##dealer", currentDealerIndex >= 0 ? partyPlayers[currentDealerIndex].DisplayName : "None"))
                {
                    if (ImGui.Selectable("None", currentDealerIndex == -1))
                        _manager.Dealer = null;

                    for (int i = 0; i < partyPlayers.Length; i++)
                    {
                        bool isSelected = (i == currentDealerIndex);
                        if (ImGui.Selectable(partyPlayers[i].DisplayName, isSelected))
                            _manager.Dealer = partyPlayers[i].NormalizedName; // assign normalized name internally
                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }
            }
            else
            {
                ImGui.TextDisabled("No players available to assign as dealer");
            }

            ImGui.Separator();

            if (ImGui.Button("Start Round"))
            {
                _updatePartyPlayers?.Invoke();
                _manager.StartRound();
            }

            if (_manager.Winners.Count > 0)
            {
                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.6f, 1f, 0.6f, 1f), $"Winner(s): {string.Join(", ", _manager.Winners)}");

                ImGui.SameLine();

                if (ImGui.Button("Copy Winner(s) to Party Chat"))
                {
                    var winnerMessage = _manager.GetLastWinnerMessage();
                    if (!string.IsNullOrEmpty(winnerMessage))
                    {
                        ImGui.SetClipboardText($"/p {winnerMessage}");
                    }
                }
            }
        }
        else
        {
            // Dealer display name logic
            string dealerDisplayName = "None";
            if (_manager.Dealer != null)
            {
                if (_manager.GetHands().TryGetValue(_manager.Dealer, out var dealerHand))
                    dealerDisplayName = dealerHand.DisplayName;
                else
                {
                    var partyPlayer = _manager.PartyPlayersWithDisplayNames
                        .FirstOrDefault(p => p.NormalizedName == _manager.Dealer);
                    if (partyPlayer != default)
                        dealerDisplayName = partyPlayer.DisplayName;
                    else
                        dealerDisplayName = _manager.Dealer;
                }
            }

            // Current player display name logic
            string currentPlayerDisplayName = "N/A";
            var currentPlayerNormalized = _manager.CurrentPlayer?.ToLowerInvariant();
            if (currentPlayerNormalized != null)
            {
                if (_manager.GetHands().TryGetValue(currentPlayerNormalized, out var currentHand))
                    currentPlayerDisplayName = currentHand.DisplayName;
                else
                {
                    var partyPlayer = _manager.PartyPlayersWithDisplayNames
                        .FirstOrDefault(p => p.NormalizedName == currentPlayerNormalized);
                    if (partyPlayer != default)
                        currentPlayerDisplayName = partyPlayer.DisplayName;
                    else
                        currentPlayerDisplayName = _manager.CurrentPlayer!;
                }
            }

            ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f), $"Dealer: {dealerDisplayName}");
            ImGui.TextColored(new Vector4(0.9f, 0.85f, 0.2f, 1f), $"Current Turn: {currentPlayerDisplayName}");

            ImGui.Separator();

            foreach (var hand in _manager.GetHands().Values)
            {
                if (hand.Cards.Count == 0)
                    continue;

                bool isCurrent = hand.DisplayName == currentPlayerDisplayName;
                bool isDone = _manager.IsPlayerDone(hand.NormalizedName);
                bool isDealer = _manager.Dealer != null && hand.NormalizedName == _manager.Dealer;

                string status = hand.IsBusted ? " (Busted)" :
                                isDone ? " (Stood)" :
                                isCurrent ? " (Your Turn)" : "";

                var color = hand.IsBusted
                    ? new Vector4(1f, 0.3f, 0.3f, 1f)
                    : isCurrent
                        ? new Vector4(0.4f, 1f, 0.4f, 1f)
                        : new Vector4(0.8f, 0.8f, 0.8f, 1f);

                if (isDealer && !hand.IsBusted && !isCurrent)
                {
                    color = new Vector4(1f, 0.85f, 0.2f, 1f);
                    status += " (Dealer)";
                }

                ImGui.TextColored(color, $"{hand.DisplayName}: {string.Join(", ", hand.Cards)} = {hand.Score}{status}");
            }

            ImGui.Separator();

            if (_manager.RoundOver)
                ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Round over! Click End Round to finish.");

            if (ImGui.Button("End Round"))
                _manager.EndRound();
        }

        ImGui.End();
    }
}

