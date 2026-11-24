
using System;
using System.Collections.Generic;

public enum HandRank
{
    HighCard = 0,
    OnePair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8,
    RoyalFlush = 9
}

public class HandResult : IComparable<HandResult>
{
    public HandRank Rank { get; private set; }
    public int[] Kickers { get; private set; }
    public Card[] BestCards { get; private set; }

    public HandResult(HandRank rank, int[] kickers, Card[] cards)
    {
        Rank = rank;
        Kickers = kickers;
        BestCards = (Card[])cards.Clone();
    }

    public int CompareTo(HandResult other)
    {
        if (other == null) return 1;

        int rankComparison = Rank.CompareTo(other.Rank);
        if (rankComparison != 0)
            return rankComparison;

        for (int i = 0; i < Kickers.Length; i++)
        {
            int comparison = Kickers[i].CompareTo(other.Kickers[i]);
            if (comparison != 0)
                return comparison;
        }

        return 0;
    }

    public override string ToString()
    {
        return Rank.ToString();
    }
}

public static class HandEvaluator
{
    public static HandResult Evaluate(IReadOnlyList<Card> cards)
    {
        if (cards == null || cards.Count < 5 || cards.Count > 7)
            throw new ArgumentException("Provide 5 to 7 cards.");

        var unique = new HashSet<Card>(cards);
        if (unique.Count != cards.Count)
            throw new ArgumentException("Duplicate cards detected.");

        HandResult best = null;

        for (int a = 0; a < cards.Count - 4; a++)
            for (int b = a + 1; b < cards.Count - 3; b++)
                for (int c = b + 1; c < cards.Count - 2; c++)
                    for (int d = c + 1; d < cards.Count - 1; d++)
                        for (int e = d + 1; e < cards.Count; e++)
                        {
                            Card[] five =
                            {
                cards[a], cards[b], cards[c], cards[d], cards[e]
            };

                            HandResult result = EvaluateFive(five);

                            if (best == null || result.CompareTo(best) > 0)
                                best = result;
                        }

        return best;
    }

    private static HandResult EvaluateFive(Card[] cards)
    {
        int[] counts = new int[15];

        foreach (Card card in cards)
            counts[(int)card.Rank]++;

        bool flush = true;
        for (int i = 1; i < 5; i++)
        {
            if (cards[i].Suit != cards[0].Suit)
            {
                flush = false;
                break;
            }
        }

        int straightHigh = FindStraightHigh(counts);

        if (flush && straightHigh > 0)
        {
            HandRank rank = straightHigh == 14
                ? HandRank.RoyalFlush
                : HandRank.StraightFlush;

            return new HandResult(rank,
                new[] { straightHigh }, cards);
        }

        int four = 0;
        int three = 0;
        int pair1 = 0;
        int pair2 = 0;

        for (int rank = 14; rank >= 2; rank--)
        {
            if (counts[rank] == 4)
                four = rank;
            else if (counts[rank] == 3)
                three = rank;
            else if (counts[rank] == 2)
            {
                if (pair1 == 0)
                    pair1 = rank;
                else
                    pair2 = rank;
            }
        }

        if (four > 0)
        {
            int kicker = HighestExcluding(counts, four);

            return new HandResult(HandRank.FourOfAKind,
                new[] { four, kicker }, cards);
        }

        if (three > 0 && pair1 > 0)
        {
            return new HandResult(HandRank.FullHouse,
                new[] { three, pair1 }, cards);
        }

        if (flush)
        {
            return new HandResult(HandRank.Flush,
                GetDescendingRanks(counts), cards);
        }

        if (straightHigh > 0)
        {
            return new HandResult(HandRank.Straight,
                new[] { straightHigh }, cards);
        }

        if (three > 0)
        {
            var kickers = new List<int> { three };
            AddRemainingRanks(counts, kickers, three);

            return new HandResult(HandRank.ThreeOfAKind,
                kickers.ToArray(), cards);
        }

        if (pair2 > 0)
        {
            int kicker = HighestExcluding(counts, pair1, pair2);

            return new HandResult(HandRank.TwoPair,
                new[] { pair1, pair2, kicker }, cards);
        }

        if (pair1 > 0)
        {
            var kickers = new List<int> { pair1 };
            AddRemainingRanks(counts, kickers, pair1);

            return new HandResult(HandRank.OnePair,
                kickers.ToArray(), cards);
        }

        return new HandResult(HandRank.HighCard,
            GetDescendingRanks(counts), cards);
    }

    private static int FindStraightHigh(int[] counts)
    {
        for (int high = 14; high >= 6; high--)
        {
            bool valid = true;

            for (int rank = high; rank > high - 5; rank--)
            {
                if (counts[rank] == 0)
                {
                    valid = false;
                    break;
                }
            }

            if (valid) return high;
        }

        // Wheel straight: A-2-3-4-5
        if (counts[14] > 0 &&
            counts[2] > 0 &&
            counts[3] > 0 &&
            counts[4] > 0 &&
            counts[5] > 0)
            return 5;

        return 0;
    }

    private static int HighestExcluding(
        int[] counts, int exclude1, int exclude2 = 0)
    {
        for (int rank = 14; rank >= 2; rank--)
        {
            if (rank != exclude1 &&
                rank != exclude2 &&
                counts[rank] > 0)
                return rank;
        }

        return 0;
    }

    private static int[] GetDescendingRanks(int[] counts)
    {
        var ranks = new List<int>(5);

        for (int rank = 14; rank >= 2; rank--)
        {
            for (int i = 0; i < counts[rank]; i++)
                ranks.Add(rank);
        }

        return ranks.ToArray();
    }

    private static void AddRemainingRanks(
        int[] counts, List<int> result, int excluded)
    {
        for (int rank = 14; rank >= 2; rank--)
        {
            if (rank == excluded) continue;

            for (int i = 0; i < counts[rank]; i++)
                result.Add(rank);
        }
    }
}
