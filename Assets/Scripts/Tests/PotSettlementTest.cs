
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PotSettlementTest : MonoBehaviour
{
    private Card C(CardRank rank, CardSuit suit)
    {
        return new Card(suit, rank);
    }

    private HandResult MakeHand(params Card[] cards)
    {
        return HandEvaluator.Evaluate(cards);
    }

    private void Start()
    {
        TestMultipleAllIns();
        TestSplitPot();
    }

    private void TestMultipleAllIns()
    {
        var a = new PokerPlayer(0, 0);
        var b = new PokerPlayer(1, 0);
        var c = new PokerPlayer(2, 0);

        a.TotalContribution = 100;
        b.TotalContribution = 300;
        c.TotalContribution = 500;

        var players = new List<PokerPlayer> { a, b, c };

        var hands = new Dictionary<int, HandResult>
        {
            // A: Three of a kind
            [0] = MakeHand(
                C(CardRank.Ace, CardSuit.Hearts),
                C(CardRank.Ace, CardSuit.Spades),
                C(CardRank.Ace, CardSuit.Clubs),
                C(CardRank.King, CardSuit.Hearts),
                C(CardRank.Queen, CardSuit.Hearts)),

            // B: Two pair
            [1] = MakeHand(
                C(CardRank.King, CardSuit.Spades),
                C(CardRank.King, CardSuit.Clubs),
                C(CardRank.Queen, CardSuit.Spades),
                C(CardRank.Queen, CardSuit.Clubs),
                C(CardRank.Two, CardSuit.Hearts)),

            // C: One pair
            [2] = MakeHand(
                C(CardRank.Jack, CardSuit.Spades),
                C(CardRank.Jack, CardSuit.Clubs),
                C(CardRank.Nine, CardSuit.Hearts),
                C(CardRank.Seven, CardSuit.Clubs),
                C(CardRank.Three, CardSuit.Diamonds))
        };

        var result = SidePotCalculator.Settle(players, hands, 0);

        Debug.Log("=== MULTIPLE ALL-IN TEST ===");

        foreach (var pot in result.Pots)
            Debug.Log($"Pot: {pot.Amount}");

        foreach (var player in players)
            Debug.Log($"Seat {player.SeatIndex}: {player.Chips} chips");

        bool passed =
            a.Chips == 300 &&
            b.Chips == 400 &&
            c.Chips == 200 &&
            players.Sum(p => p.Chips) == 900;

        Debug.Log(passed ? "PASS: Multiple all-ins" :
                           "FAIL: Multiple all-ins");
    }

    private void TestSplitPot()
    {
        var a = new PokerPlayer(0, 0);
        var b = new PokerPlayer(1, 0);
        var c = new PokerPlayer(2, 0);

        a.TotalContribution = 101;
        b.TotalContribution = 101;
        c.TotalContribution = 101;

        var players = new List<PokerPlayer> { a, b, c };

        // A and B tie with the same ranking and kickers.
        var tieHand = MakeHand(
            C(CardRank.Ace, CardSuit.Hearts),
            C(CardRank.King, CardSuit.Hearts),
            C(CardRank.Queen, CardSuit.Hearts),
            C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Ten, CardSuit.Hearts));

        var weaker = MakeHand(
            C(CardRank.Two, CardSuit.Clubs),
            C(CardRank.Three, CardSuit.Hearts),
            C(CardRank.Five, CardSuit.Diamonds),
            C(CardRank.Seven, CardSuit.Spades),
            C(CardRank.Nine, CardSuit.Clubs));

        var hands = new Dictionary<int, HandResult>
        {
            [0] = tieHand,
            [1] = tieHand,
            [2] = weaker
        };

        var result = SidePotCalculator.Settle(players, hands, 0);

        Debug.Log("=== SPLIT POT TEST ===");

        foreach (var player in players)
            Debug.Log($"Seat {player.SeatIndex}: {player.Chips} chips");

        // 303 / 2 = 151 each, remainder 1.
        // Seat 1 is first clockwise from dealer seat 0.
        bool passed =
            a.Chips == 151 &&
            b.Chips == 152 &&
            c.Chips == 0 &&
            players.Sum(p => p.Chips) == 303;

        Debug.Log(passed ? "PASS: Split pot" :
                           "FAIL: Split pot");
    }
}
