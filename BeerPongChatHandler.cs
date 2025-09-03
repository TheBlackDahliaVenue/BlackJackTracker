using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using System.Text.RegularExpressions;
using System;
using Blackjack.GameModules;

namespace Blackjack.GameModules
{
    public class BeerPongChatHandler : IDisposable
    {
        private readonly IChatGui _chat;
        private readonly BeerPongModule _beerPongModule;
        private readonly IPluginLog _log;

        // Matches lines like: "(★Player Name) Random! (1-100) 78"
        private static readonly Regex Dice100Regex = new(@"Random! \(1-100\) (\d+)", RegexOptions.Compiled);

        public BeerPongChatHandler(IChatGui chat, BeerPongModule module, IPluginLog log)
        {
            _chat = chat;
            _beerPongModule = module;
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
            if (type != XivChatType.Party || !_beerPongModule.RoundActive)
                return;

            string text = message.TextValue?.Trim() ?? "";
            string playerName = sender.TextValue?.Trim() ?? "";

            if (string.IsNullOrEmpty(playerName) || string.IsNullOrEmpty(text))
                return;

            var match = Dice100Regex.Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int roll))
                return;

            _log.Debug($"[BeerPongChat] {playerName} (normalized: {BeerPongModule.NormalizePlayerName(playerName)}) rolled {roll}");


            _beerPongModule.AddRoll(playerName, roll);
        }

        public void Dispose()
        {
            _chat.ChatMessage -= OnChatMessage;
        }
    }
}
