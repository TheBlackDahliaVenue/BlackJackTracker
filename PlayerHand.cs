namespace Blackjack;
using System;
using System.Collections.Generic;
using System.Linq;

public class PlayerHand
{
    public string NormalizedName { get; }
    public string DisplayName { get; private set; }

    public List<List<int>> Hands { get; } = new();
    public int CurrentHandIndex { get; set; } = 0;

    private HashSet<int> _finishedHands = new();

    public PlayerHand(string normalizedName, string displayName)
    {
        NormalizedName = normalizedName;
        DisplayName = displayName;
        Hands.Add(new List<int>());
    }

    public void SetDisplayName(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            DisplayName = displayName;
    }

    public void AddCard(int value)
    {
        Hands[CurrentHandIndex].Add(value);
    }

    public int GetScore(int handIndex)
    {
        var cards = Hands[handIndex];
        int sum = cards.Sum();
        int aceCount = cards.Count(c => c == 1);

        while (aceCount > 0 && sum + 10 <= 21)
        {
            sum += 10;
            aceCount--;
        }

        return sum;
    }

    public int Score => GetScore(CurrentHandIndex);
    public bool IsBusted => Score > 21;

    public void Stand()
    {
        _finishedHands.Add(CurrentHandIndex);
    }

    public void Busted()
    {
        _finishedHands.Add(CurrentHandIndex);
    }

    public bool IsHandDone(int handIndex)
    {
        return _finishedHands.Contains(handIndex);
    }

    public bool AllHandsFinished()
    {
        for (int i = 0; i < Hands.Count; i++)
        {
            if (!_finishedHands.Contains(i))
                return false;
        }
        return true;
    }

    public bool CanSplit()
    {
        var cards = Hands[CurrentHandIndex];
        return cards.Count == 2 && cards[0] == cards[1];
    }

    public void Split()
    {
        if (!CanSplit())
            throw new InvalidOperationException("Cannot split this hand.");

        var cards = Hands[CurrentHandIndex];
        int cardToMove = cards[1];
        cards.RemoveAt(1);
        Hands.Insert(CurrentHandIndex + 1, new List<int> { cardToMove });
        _finishedHands.Remove(CurrentHandIndex);
    }

    public void StandAndAdvance()
    {
        Stand();
        for (int i = CurrentHandIndex + 1; i < Hands.Count; i++)
        {
            if (!_finishedHands.Contains(i))
            {
                CurrentHandIndex = i;
                return;
            }
        }
    }

    public string GetCardDisplay(int handIndex)
    {
        if (handIndex >= Hands.Count)
            return "";

        var cards = Hands[handIndex];
        List<string> display = new();

        int hardSum = cards.Sum();
        int softSum = hardSum;
        int aceCount = cards.Count(c => c == 1);
        int remainingAces = aceCount;

        while (remainingAces > 0 && softSum + 10 <= 21)
        {
            softSum += 10;
            remainingAces--;
        }

        // Build display string per card
        int tempSoft = 0;
        int usedSoftAces = aceCount;

        foreach (var card in cards)
        {
            if (card == 1)
            {
                // Determine if this Ace can be displayed as 1/11
                if (usedSoftAces > 0 && tempSoft + 11 <= 21)
                {
                    display.Add("1/11");
                    tempSoft += 11;
                    usedSoftAces--;
                }
                else
                {
                    display.Add("1");
                    tempSoft += 1;
                }
            }
            else
            {
                display.Add(card.ToString());
                tempSoft += card;
            }
        }

        string totalDisplay = (softSum != hardSum && softSum <= 21)
            ? $"{hardSum}/{softSum}"
            : $"{Math.Min(softSum, hardSum)}";

        return $"{string.Join(" ", display)} = {totalDisplay}";
    }

    public string GetCurrentHandDisplay()
    {
        return GetCardDisplay(CurrentHandIndex);
    }
	
	public int GetAlternateScore(int handIndex)
{
    if (handIndex < 0 || handIndex >= Hands.Count)
        return 0;

    return Hands[handIndex].Sum();
}

public int GetBestScore(int handIndex)
{
    int score = GetScore(handIndex);
    int alt = GetAlternateScore(handIndex);
    if (score > 21 && alt <= 21)
        return alt;
    if (alt <= 21 && alt > score)
        return alt;
    return score;
}


}
