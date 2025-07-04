namespace Blackjack;
using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerHand
{
    public string NormalizedName { get; }
    public string DisplayName { get; private set; } // New!

    public List<int> Cards { get; } = new();
    public bool IsBusted { get; set; } = false;

    public int Score => Cards.Sum();

    public PlayerHand(string normalizedName, string displayName)
    {
        NormalizedName = normalizedName;
        DisplayName = displayName;
    }

    public void AddCard(int value)
    {
        Cards.Add(value);
    }

    public void SetDisplayName(string displayName)
    {
        // Update in case a new casing (e.g., from chat) is seen
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;
    }
}
