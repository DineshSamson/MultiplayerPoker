
using UnityEngine;
using System;

public class HandEvaluatorTest : MonoBehaviour
{
    private int passed;
    private int failed;

    private Card C(CardRank rank, CardSuit suit)
    {
        return new Card(suit, rank);
    }

    private void Check(string name, bool condition)
    {
        if (condition)
        {
            passed++;
            Debug.Log("PASS: " + name);
        }
        else
        {
            failed++;
            Debug.LogError("FAIL: " + name);
        }
    }

    private void Start()
    {
        TestRoyalFlush();
        TestWheelStraight();
        TestFullHouse();
        TestKickers();
        TestTie();
        TestSevenCards();
        TestDuplicateCards();

        Debug.Log($"RESULT: {passed} passed, {failed} failed");
    }

    private void TestRoyalFlush()
    {
        var hand = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Hearts),
            C(CardRank.King, CardSuit.Hearts),
            C(CardRank.Queen, CardSuit.Hearts),
            C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Ten, CardSuit.Hearts)
        });

        Check("Royal Flush", hand.Rank == HandRank.RoyalFlush);
    }

    private void TestWheelStraight()
    {
        var hand = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Spades),
            C(CardRank.Two, CardSuit.Hearts),
            C(CardRank.Three, CardSuit.Clubs),
            C(CardRank.Four, CardSuit.Diamonds),
            C(CardRank.Five, CardSuit.Spades)
        });

        Check("Ace-low straight",
            hand.Rank == HandRank.Straight &&
            hand.Kickers[0] == 5);
    }

    private void TestFullHouse()
    {
        var hand = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.King, CardSuit.Hearts),
            C(CardRank.King, CardSuit.Spades),
            C(CardRank.King, CardSuit.Clubs),
            C(CardRank.Queen, CardSuit.Hearts),
            C(CardRank.Queen, CardSuit.Spades)
        });

        Check("Full House", hand.Rank == HandRank.FullHouse);
    }

    private void TestKickers()
    {
        var first = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Hearts),
            C(CardRank.Ace, CardSuit.Spades),
            C(CardRank.King, CardSuit.Clubs),
            C(CardRank.Nine, CardSuit.Diamonds),
            C(CardRank.Four, CardSuit.Hearts)
        });

        var second = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Clubs),
            C(CardRank.Ace, CardSuit.Diamonds),
            C(CardRank.Queen, CardSuit.Spades),
            C(CardRank.Nine, CardSuit.Hearts),
            C(CardRank.Four, CardSuit.Clubs)
        });

        Check("Pair kicker comparison",
            first.CompareTo(second) > 0);
    }

    private void TestTie()
    {
        var first = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Hearts),
            C(CardRank.King, CardSuit.Hearts),
            C(CardRank.Queen, CardSuit.Hearts),
            C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Ten, CardSuit.Hearts)
        });

        var second = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Ace, CardSuit.Spades),
            C(CardRank.King, CardSuit.Spades),
            C(CardRank.Queen, CardSuit.Spades),
            C(CardRank.Jack, CardSuit.Spades),
            C(CardRank.Ten, CardSuit.Spades)
        });

        Check("Equal hands tie", first.CompareTo(second) == 0);
    }

    private void TestSevenCards()
    {
        var hand = HandEvaluator.Evaluate(new[]
        {
            C(CardRank.Two, CardSuit.Clubs),
            C(CardRank.Three, CardSuit.Diamonds),
            C(CardRank.Ace, CardSuit.Spades),
            C(CardRank.King, CardSuit.Spades),
            C(CardRank.Queen, CardSuit.Spades),
            C(CardRank.Jack, CardSuit.Spades),
            C(CardRank.Ten, CardSuit.Spades)
        });

        Check("Best 5 of 7",
            hand.Rank == HandRank.RoyalFlush &&
            hand.BestCards.Length == 5);
    }

    private void TestDuplicateCards()
    {
        bool rejected = false;

        try
        {
            HandEvaluator.Evaluate(new[]
            {
                C(CardRank.Ace, CardSuit.Spades),
                C(CardRank.Ace, CardSuit.Spades),
                C(CardRank.King, CardSuit.Hearts),
                C(CardRank.Queen, CardSuit.Clubs),
                C(CardRank.Jack, CardSuit.Diamonds)
            });
        }
        catch (ArgumentException)
        {
            rejected = true;
        }

        Check("Duplicate cards rejected", rejected);
    }
}
