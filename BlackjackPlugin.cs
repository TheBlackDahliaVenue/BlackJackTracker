using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Party;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackjack;

public sealed class BlackjackPlugin : IDalamudPlugin, IDisposable
{
    public string Name => "Blackjack Plugin";

    private readonly IChatGui _chatGui;
    private readonly IPluginLog _pluginLog;
    private readonly IDalamudPluginInterface _pluginInterface;

    private readonly IClientState _clientState;
    private readonly IPartyList _partyList;
    private readonly IFramework _framework;

    private readonly BlackjackManager _manager;
    private readonly ChatHandler _chatHandler;
    private readonly BlackjackUI _ui;

    private readonly Action _openConfigAction;
    private readonly Action _openMainUiAction;

    private List<string> _lastPartyMembers = new();

    public BlackjackPlugin(
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        IPluginLog pluginLog,
        IClientState clientState,
        IPartyList partyList,
        IFramework framework)
    {
        _pluginInterface = pluginInterface;
        _chatGui = chatGui;
        _pluginLog = pluginLog;

        _clientState = clientState;
        _partyList = partyList;
        _framework = framework;

        // ✅ Pass all 3 required parameters: log, chatGui, pluginInterface
        _manager = new BlackjackManager(_pluginLog, _chatGui, _pluginInterface);
        _chatHandler = new ChatHandler(_manager, _pluginLog);
        _ui = new BlackjackUI(_manager, UpdatePartyPlayersFromPartyList);

        _pluginInterface.UiBuilder.Draw += DrawUI;

        _openConfigAction = () => _ui.ToggleVisible();
        _openMainUiAction = () => _ui.ToggleVisible();

        _pluginInterface.UiBuilder.OpenConfigUi += _openConfigAction;
        _pluginInterface.UiBuilder.OpenMainUi += _openMainUiAction;

        _chatGui.ChatMessage += _chatHandler.OnChatMessage;

        _framework.Update += OnFrameworkUpdate;

        // Initial update deferred via Draw event to ensure main thread
        ScheduleUpdatePartyPlayers();
    }

    private void ScheduleUpdatePartyPlayers()
    {
        void OnDraw()
        {
            UpdatePartyPlayersFromPartyList();
            _pluginInterface.UiBuilder.Draw -= OnDraw;
        }

        _pluginInterface.UiBuilder.Draw += OnDraw;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (_clientState.LocalPlayer == null)
            return;

        var currentMembers = Enumerable.Range(0, _partyList.Length)
            .Select(i => _partyList[i]?.Name.TextValue)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();

        if (!_lastPartyMembers.SequenceEqual(currentMembers))
        {
            _lastPartyMembers = currentMembers;
            UpdatePartyPlayersFromPartyList();
        }
    }

    private void DrawUI()
    {
        _ui.Draw();
    }

    private void UpdatePartyPlayersFromPartyList()
    {
        if (_clientState.LocalPlayer == null)
            return;

        var players = Enumerable.Range(0, _partyList.Length)
            .Select(i => _partyList[i]?.Name.TextValue)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct();

        _manager.UpdatePartyPlayers(players);
    }

    public void Dispose()
    {
        _chatGui.ChatMessage -= _chatHandler.OnChatMessage;
        _pluginInterface.UiBuilder.Draw -= DrawUI;
        _pluginInterface.UiBuilder.OpenConfigUi -= _openConfigAction;
        _pluginInterface.UiBuilder.OpenMainUi -= _openMainUiAction;

        _framework.Update -= OnFrameworkUpdate;
    }
}
