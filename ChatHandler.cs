using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using System;


namespace Blackjack;

public class ChatHandler
{
    private readonly BlackjackManager _manager;
    private readonly IPluginLog _log;

    // Matches party roll format: "Random! (1-11) 8"
    private readonly Regex _rollRegex = new(@"Random! \(1-11\) (\d{1,2})", RegexOptions.Compiled);

    public ChatHandler(BlackjackManager manager, IPluginLog log)
    {
        _manager = manager;
        _log = log;
    }

    public void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        if (type != XivChatType.Party)
            return;

        var text = message.TextValue;
        var playerName = sender.TextValue;

        if (_rollRegex.Match(text) is { Success: true } match &&
            int.TryParse(match.Groups[1].Value, out var roll))
        {
            _log.Debug("Captured dice roll: {Player} rolled {Roll}", playerName, roll);
            _manager.AddRoll(playerName, roll);
            return;
        }

        if (text.Trim().Equals("stand", StringComparison.OrdinalIgnoreCase))
        {
            _log.Debug("Captured stand from {Player}", playerName);
            _manager.PlayerStands(playerName);
        }
    }
}
