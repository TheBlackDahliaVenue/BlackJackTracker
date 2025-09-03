using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Blackjack.GameModules;
using System;

namespace Blackjack.GameModules
{
    public class DartsChatHandler : IDisposable
    {
        private readonly IChatGui _chat;
        private readonly DartsModule _dartsModule;
        private readonly IPluginLog _log;

        private static readonly Regex RollRegex = new(@"Random!(?: \(1-(\d+)\))? (\d+)", RegexOptions.Compiled);

        public DartsChatHandler(IChatGui chat, DartsModule module, IPluginLog log)
        {
            _chat = chat;
            _dartsModule = module;
            _log = log;
            _chat.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(
            XivChatType type,
            int timestamp,
            ref SeString sender,
            ref SeString message,
            ref bool isHandled)
        {
            if (!_dartsModule.RoundActive)
                return;

            if (type != XivChatType.Party && type != XivChatType.Say && type != XivChatType.Yell)
                return;

            string text = message.TextValue?.Trim() ?? "";
            string playerNameRaw = sender.TextValue?.Trim() ?? "";

            if (string.IsNullOrEmpty(playerNameRaw) || string.IsNullOrEmpty(text))
                return;

            var match = RollRegex.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int roll))
                return;

            if (roll < 1 || roll > 60)
                return;

            // Normalize player name and also get display name for mapping
            var (normalizedName, displayName) = _dartsModule.NormalizeWithDisplayName(playerNameRaw);

            // Update display name mapping in module for GUI
            _dartsModule.UpdateDisplayNameMapping(normalizedName, displayName);

            // If this is the first dart in the current turn for this player, start their turn
            // We check if current throwing player matches normalizedName; if not, start new turn
            if (_dartsModule.CurrentThrowingPlayer != normalizedName)
            {
                _log.Debug($"[Darts] Starting turn for {normalizedName}");
                _dartsModule.StartPlayerTurn(normalizedName);
            }

            // Add this dart throw to the module (it accumulates and auto-updates on 3 throws)
            _dartsModule.AddRoll(normalizedName, roll);

            _log.Debug($"[Darts] {normalizedName} threw dart: {roll}");
        }

        public void Dispose()
        {
            _chat.ChatMessage -= OnChatMessage;
        }
    }
}
