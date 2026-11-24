
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PokerGameEngineTest : MonoBehaviour
{
    private int passed;
    private int failed;

    private void Start()
    {
        passed = 0;
        failed = 0;

        Debug.Log("========== POKER ENGINE TESTS ==========");

        RunTest("Blinds and first turn", TestBlinds);
        RunTest("Call and check", TestCallAndCheck);
        RunTest("Raise betting", TestRaise);
        RunTest("Fold betting", TestFold);
        RunTest("All-in betting", TestAllIn);
        RunTest("Full hand and settlement", TestFullHand);
        RunTest("Multiple side pots", TestSidePots);
        RunTest("Split pot and odd chip", TestSplitPot);
        RunTest("Everyone folds - immediate winner", TestEveryoneFolds);

        // New tests
        RunTest("Heads-up poker", TestHeadsUp);
        RunTest("Short all-in does not reopen raise", TestShortAllIn);

        Debug.Log(
            $"========== RESULTS: {passed} PASSED, " +
            $"{failed} FAILED =========="
        );
    }

    private void RunTest(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Debug.Log($"PASS: {name}");
        }
        catch (Exception ex)
        {
            failed++;
            Debug.LogError($"FAIL: {name} | {ex.Message}");
        }
    }

    private void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private PokerGameEngine CreateGame(
        int chips0 = 1000,
        int chips1 = 1000,
        int chips2 = 1000)
    {
        var game = new PokerGameEngine();

        game.AddPlayer(0, chips0);
        game.AddPlayer(1, chips1);
        game.AddPlayer(2, chips2);

        game.StartHand();
        return game;
    }

    private void Act(
        PokerGameEngine game,
        int seat,
        PokerAction action,
        int raiseTo = 0)
    {
        bool success = game.Betting.TryAction(
            seat, action, raiseTo, out string error);

        Assert(success,
            $"Seat {seat + 1} {action} failed: {error}");
    }

    private void Advance(
        PokerGameEngine game,
        PokerStage expected)
    {
        Assert(game.TryAdvanceBettingRound(),
            "Stage advancement failed.");

        Assert(game.Stage == expected,
            $"Expected {expected}, got {game.Stage}");
    }

    private void TestBlinds()
    {
        var game = CreateGame();

        Assert(game.DealerSeat == 0, "Wrong dealer.");
        Assert(game.SmallBlindSeat == 1, "Wrong small blind.");
        Assert(game.BigBlindSeat == 2, "Wrong big blind.");
        Assert(game.Betting.CurrentTurnSeat == 0,
            "Wrong first turn.");
        Assert(game.Pot == 30, "Initial pot incorrect.");
        Assert(game.Players[1].Chips == 990,
            "Small blind incorrect.");
        Assert(game.Players[2].Chips == 980,
            "Big blind incorrect.");
    }

    private void TestCallAndCheck()
    {
        var game = CreateGame();

        Act(game, 0, PokerAction.Call);
        Act(game, 1, PokerAction.Call);
        Act(game, 2, PokerAction.Check);

        Assert(game.Betting.IsRoundComplete(),
            "Round incomplete.");
        Assert(game.Pot == 60, "Pot should be 60.");
        Assert(game.Players.All(p => p.CurrentBet == 20),
            "Incorrect player bets.");
    }

    private void TestRaise()
    {
        var game = CreateGame();

        Act(game, 0, PokerAction.Raise, 60);
        Act(game, 1, PokerAction.Call);
        Act(game, 2, PokerAction.Call);

        Assert(game.Betting.IsRoundComplete(),
            "Round incomplete.");
        Assert(game.Betting.CurrentBet == 60,
            "Current bet incorrect.");
        Assert(game.Pot == 180,
            "Pot should be 180.");
        Assert(game.Players.All(p => p.Chips == 940),
            "Chip balances incorrect.");
    }

    private void TestFold()
    {
        var game = CreateGame();

        Act(game, 0, PokerAction.Fold);
        Act(game, 1, PokerAction.Call);
        Act(game, 2, PokerAction.Check);

        Assert(game.Players[0].IsFolded,
            "Seat 1 not folded.");
        Assert(game.Betting.IsRoundComplete(),
            "Round incomplete.");
        Assert(game.Pot == 40,
            "Pot should be 40.");
    }

    private void TestAllIn()
    {
        var game = CreateGame(100, 300, 500);

        Act(game, 0, PokerAction.AllIn);
        Act(game, 1, PokerAction.Call);
        Act(game, 2, PokerAction.Call);

        Assert(game.Players[0].IsAllIn,
            "Seat 1 not all-in.");
        Assert(game.Players[0].Chips == 0,
            "Seat 1 chips not zero.");
        Assert(game.Betting.IsRoundComplete(),
            "Round incomplete.");
        Assert(game.Pot == 300,
            "Pot should be 300.");
    }

    private void TestFullHand()
    {
        var game = CreateGame();

        Act(game, 0, PokerAction.Call);
        Act(game, 1, PokerAction.Call);
        Act(game, 2, PokerAction.Check);
        Advance(game, PokerStage.Flop);

        CheckPostFlop(game);
        Advance(game, PokerStage.Turn);

        CheckPostFlop(game);
        Advance(game, PokerStage.River);

        CheckPostFlop(game);
        Advance(game, PokerStage.Showdown);

        Assert(game.CommunityCards.Count == 5,
            "Community card count incorrect.");

        game.SettlePot();

        Assert(game.IsPotSettled,
            "Pot not settled.");
        Assert(game.Players.Sum(p => p.Chips) == 3000,
            "Chip conservation failed.");

        bool rejected = false;

        try
        {
            game.SettlePot();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert(rejected,
            "Duplicate settlement allowed.");
    }

    private void CheckPostFlop(PokerGameEngine game)
    {
        Act(game, 1, PokerAction.Check);
        Act(game, 2, PokerAction.Check);
        Act(game, 0, PokerAction.Check);
    }

    private HandResult Hand(params Card[] cards)
    {
        return HandEvaluator.Evaluate(cards);
    }

    private Card C(CardRank rank, CardSuit suit)
    {
        return new Card(suit, rank);
    }

    private void TestSidePots()
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
            [0] = Hand(
                C(CardRank.Ace, CardSuit.Hearts),
                C(CardRank.Ace, CardSuit.Spades),
                C(CardRank.Ace, CardSuit.Clubs),
                C(CardRank.King, CardSuit.Hearts),
                C(CardRank.Queen, CardSuit.Hearts)),

            [1] = Hand(
                C(CardRank.King, CardSuit.Spades),
                C(CardRank.King, CardSuit.Clubs),
                C(CardRank.Queen, CardSuit.Spades),
                C(CardRank.Queen, CardSuit.Clubs),
                C(CardRank.Two, CardSuit.Hearts)),

            [2] = Hand(
                C(CardRank.Jack, CardSuit.Spades),
                C(CardRank.Jack, CardSuit.Clubs),
                C(CardRank.Nine, CardSuit.Hearts),
                C(CardRank.Seven, CardSuit.Clubs),
                C(CardRank.Three, CardSuit.Diamonds))
        };

        var settlement =
            SidePotCalculator.Settle(players, hands, 0);

        Assert(a.Chips == 300,
            "Main pot incorrect.");
        Assert(b.Chips == 400,
            "Side pot incorrect.");
        Assert(c.Chips == 200,
            "Refund incorrect.");
        Assert(settlement.Pots.Count == 2,
            "Wrong pot count.");
        Assert(players.Sum(p => p.Chips) == 900,
            "Chip conservation failed.");
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

        var tie = Hand(
            C(CardRank.Ace, CardSuit.Hearts),
            C(CardRank.King, CardSuit.Hearts),
            C(CardRank.Queen, CardSuit.Hearts),
            C(CardRank.Jack, CardSuit.Hearts),
            C(CardRank.Ten, CardSuit.Hearts));

        var weak = Hand(
            C(CardRank.Two, CardSuit.Clubs),
            C(CardRank.Three, CardSuit.Hearts),
            C(CardRank.Five, CardSuit.Diamonds),
            C(CardRank.Seven, CardSuit.Spades),
            C(CardRank.Nine, CardSuit.Clubs));

        var hands = new Dictionary<int, HandResult>
        {
            [0] = tie,
            [1] = tie,
            [2] = weak
        };

        SidePotCalculator.Settle(players, hands, 0);

        Assert(a.Chips == 151,
            "Seat 1 payout incorrect.");
        Assert(b.Chips == 152,
            "Seat 2 payout incorrect.");
        Assert(c.Chips == 0,
            "Seat 3 payout incorrect.");
        Assert(players.Sum(p => p.Chips) == 303,
            "Chip conservation failed.");
    }

    private void TestEveryoneFolds()
    {
        var game = CreateGame();

        Act(game, 0, PokerAction.Fold);
        Act(game, 1, PokerAction.Fold);

        Assert(game.TryAdvanceBettingRound(),
            "Hand did not finish.");
        Assert(game.Stage == PokerStage.Showdown,
            "Wrong final stage.");
        Assert(game.IsPotSettled,
            "Pot not settled.");
        Assert(game.CommunityCards.Count == 0,
            "Community cards should not be dealt.");
        Assert(game.Pot == 30,
            "Pot should be 30.");
        Assert(game.Players[0].Chips == 1000,
            "Seat 1 balance incorrect.");
        Assert(game.Players[1].Chips == 990,
            "Seat 2 balance incorrect.");
        Assert(game.Players[2].Chips == 1010,
            "Seat 3 balance incorrect.");
        Assert(game.Players.Sum(p => p.Chips) == 3000,
            "Chip conservation failed.");
        Assert(!game.TryFinishIfEveryoneFolded(),
            "Duplicate payout allowed.");

        bool rejected = false;

        try
        {
            game.SettlePot();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Assert(rejected,
            "Duplicate settlement allowed.");
    }

    // NEW TEST 10
    // Dealer is small blind in a two-player game.
    private void TestHeadsUp()
    {
        var game = new PokerGameEngine();

        game.AddPlayer(0, 1000);
        game.AddPlayer(1, 1000);

        game.StartHand();

        Assert(game.DealerSeat == 0,
            "Dealer should be Seat 1.");
        Assert(game.SmallBlindSeat == 0,
            "Dealer must post small blind.");
        Assert(game.BigBlindSeat == 1,
            "Seat 2 must post big blind.");
        Assert(game.Betting.CurrentTurnSeat == 0,
            "Dealer must act first preflop.");
        Assert(game.Pot == 30,
            "Initial pot should be 30.");

        Act(game, 0, PokerAction.Call);
        Act(game, 1, PokerAction.Check);

        Assert(game.Betting.IsRoundComplete(),
            "Heads-up preflop incomplete.");

        Advance(game, PokerStage.Flop);

        Assert(game.Betting.CurrentTurnSeat == 1,
            "Big blind must act first on flop.");

        Act(game, 1, PokerAction.Check);
        Act(game, 0, PokerAction.Check);

        Advance(game, PokerStage.Turn);

        Assert(game.Betting.CurrentTurnSeat == 1,
            "Big blind must act first on turn.");

        Act(game, 1, PokerAction.Check);
        Act(game, 0, PokerAction.Check);

        Advance(game, PokerStage.River);

        Assert(game.Betting.CurrentTurnSeat == 1,
            "Big blind must act first on river.");

        Act(game, 1, PokerAction.Check);
        Act(game, 0, PokerAction.Check);

        Advance(game, PokerStage.Showdown);

        game.SettlePot();

        Assert(game.Players.Sum(p => p.Chips) == 2000,
            "Heads-up chip conservation failed.");

        Debug.Log("Heads-up dealer, blinds and turns correct.");
    }

    // NEW TEST 11
    // Seat 1 raises to 100.
    // Seat 2 goes all-in for 150 (only 50 more).
    // Seat 3 calls 150.
    // Seat 1 may call the extra 50 but must not re-raise.
    private void TestShortAllIn()
    {
        var game = CreateGame(1000, 150, 1000);

        Act(game, 0, PokerAction.Raise, 100);

        Assert(game.Betting.CurrentBet == 100,
            "Initial raise incorrect.");

        Act(game, 1, PokerAction.AllIn);

        Assert(game.Betting.CurrentBet == 150,
            "Short all-in bet should be 150.");

        Assert(game.Players[1].IsAllIn,
            "Seat 2 should be all-in.");

        Act(game, 2, PokerAction.Call);

        Assert(game.Betting.CurrentTurnSeat == 0,
            "Seat 1 should act again to match 150.");

        // This raise should be rejected because Seat 1
        // already acted and the short all-in did not
        // constitute a full minimum raise.
        bool raiseAccepted = game.Betting.TryAction(
            0, PokerAction.Raise, 250,
            out string raiseError);

        Assert(!raiseAccepted,
            "Short all-in incorrectly reopened raising.");

        Assert(game.Betting.CurrentTurnSeat == 0,
            "Rejected raise must not change turn.");

        Act(game, 0, PokerAction.Call);

        Assert(game.Betting.IsRoundComplete(),
            "Short all-in betting round incomplete.");

        Assert(game.Pot == 450,
            "Expected pot of 450.");

        Assert(game.Players[0].Chips == 850,
            "Seat 1 chips incorrect.");
        Assert(game.Players[1].Chips == 0,
            "Seat 2 chips incorrect.");
        Assert(game.Players[2].Chips == 850,
            "Seat 3 chips incorrect.");

        Debug.Log(
            "Short all-in correctly prevented reopening the raise.");
    }
}
