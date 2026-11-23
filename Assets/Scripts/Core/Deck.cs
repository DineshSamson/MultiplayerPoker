
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

public class Deck
{
    private readonly List<Card> cards = new List<Card>(52);
    private int nextCardIndex;

    public Deck()
    {
        Reset();
    }

    public void Reset()
    {
        cards.Clear();

        foreach (CardSuit suit in Enum.GetValues(typeof(CardSuit)))
        {
            for (int rank = 2; rank <= 14; rank++)
            {
                cards.Add(new Card(suit, (CardRank)rank));
            }
        }

        nextCardIndex = 0;
    }

    public void Shuffle()
    {
        // Fisher-Yates shuffle with secure random values.
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);

            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        nextCardIndex = 0;
    }

    public Card Deal()
    {
        if (nextCardIndex >= cards.Count)
            throw new InvalidOperationException("Deck is empty.");

        return cards[nextCardIndex++];
    }

    public int RemainingCards => cards.Count - nextCardIndex;
}
