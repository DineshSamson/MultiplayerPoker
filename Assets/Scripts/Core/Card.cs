
using System;

public enum CardSuit
{
    Clubs,
    Diamonds,
    Hearts,
    Spades
}

public enum CardRank
{
    Two = 2,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace
}

public readonly struct Card : IEquatable<Card>
{
    public readonly CardSuit Suit;
    public readonly CardRank Rank;

    public Card(CardSuit suit, CardRank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public bool Equals(Card other)
    {
        return Suit == other.Suit && Rank == other.Rank;
    }

    public override bool Equals(object obj)
    {
        return obj is Card other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Suit, Rank);
    }

    public override string ToString()
    {
        return $"{Rank} of {Suit}";
    }
}
