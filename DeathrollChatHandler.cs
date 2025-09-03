using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using Blackjack.GameModules;
using System;


namespace Blackjack.GameModules
{
    public class DeathrollChatHandler : IDisposable
    {
        private readonly IChatGui _chat;
        private readonly DeathrollModule _deathrollModule;
        private readonly IPluginLog _log;

        // Matches:
        //  - "(★Asheanei Sher) Random! 589"
        //  - "(★Asheanei Sher) Random! (1-589) 13"
        private static readonly Regex RollRegex = new(@"Random!(?: \(1-(\d+)\))? (\d+)", RegexOptions.Compiled);

        public DeathrollChatHandler(IChatGui chat, DeathrollModule module, IPluginLog log)
        {
            _chat = chat;
            _deathrollModule = module;
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
            if (type != XivChatType.Party || !_deathrollModule.RoundActive)
                return;

            string text = message.TextValue?.Trim() ?? "";
            string playerName = sender.TextValue?.Trim() ?? "";

            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(text))
                return;

            var match = RollRegex.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int roll))
                return;

            // _log.Debug($"[DeathrollChat] {playerName} rolled {roll} ({text})");

            // Directly call AddRoll with displayName and roll; normalization handled inside
            _deathrollModule.AddRoll(playerName, roll);
        }

        public void Dispose()
        {
            _chat.ChatMessage -= OnChatMessage;
        }
    }
}
