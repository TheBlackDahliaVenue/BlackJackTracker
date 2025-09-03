// === IGameModule.cs ===
using System.Collections.Generic;

namespace Blackjack.GameModules
{
    public interface IGameModule
    {
        string Name { get; }

        void UpdatePartyPlayers(IEnumerable<string> players);
        void StartRound();
        void EndRound();
        void AddRoll(string player, int roll);
        void PlayerStands(string player);

        bool RoundActive { get; }
        bool RoundOver { get; }
        string? CurrentPlayer { get; }
        string? Dealer { get; set; }

        IReadOnlyList<string> Winners { get; }
        IReadOnlyList<(string NormalizedName, string DisplayName)> PartyPlayersWithDisplayNames { get; }

        bool IsPlayerDone(string player);
        string GetLastWinnerMessage();

        IReadOnlyDictionary<string, PlayerHand> GetHands();

    }
}
