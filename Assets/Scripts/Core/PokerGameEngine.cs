
using System;
using System.Collections.Generic;
using System.Linq;

public class PokerGameEngine
{
    public const int SmallBlind = 10;
    public const int BigBlind = 20;

    public PokerStage Stage { get; private set; } = PokerStage.Waiting;

    public List<PokerPlayer> Players { get; } = new();
    public List<Card> CommunityCards { get; } = new();

    public int DealerSeat { get; private set; } = -1;
    public int SmallBlindSeat { get; private set; } = -1;
    public int BigBlindSeat { get; private set; } = -1;

    public int Pot => Players.Sum(p => p.TotalContribution);

    public BettingManager Betting { get; private set; }

    public bool IsPotSettled { get; private set; }

    private Deck deck = new Deck();

    public void AddPlayer(int seatIndex, int chips = 1000)
    {
        if (Stage != PokerStage.Waiting &&
            Stage != PokerStage.Showdown)
            throw new InvalidOperationException(
                "Cannot add player during an active hand.");

        if (seatIndex < 0 || seatIndex >= 6)
            throw new ArgumentOutOfRangeException(
                nameof(seatIndex), "Seat must be between 0 and 5.");

        if (chips < 0)
            throw new ArgumentOutOfRangeException(nameof(chips));

        if (Players.Exists(p => p.SeatIndex == seatIndex))
            throw new InvalidOperationException("Seat already occupied.");

        if (Players.Count >= 6)
            throw new InvalidOperationException("Maximum six players.");

        Players.Add(new PokerPlayer(seatIndex, chips));
        Players.Sort((a, b) => a.SeatIndex.CompareTo(b.SeatIndex));
    }

    public void StartHand()
    {
        if (Stage != PokerStage.Waiting &&
            Stage != PokerStage.Showdown)
            throw new InvalidOperationException("Hand already active.");

        if (Stage == PokerStage.Showdown && !IsPotSettled)
            throw new InvalidOperationException(
                "Settle the previous hand before starting another.");

        var active = Players.Where(p => p.Chips > 0).ToList();

        if (active.Count < 2)
            throw new InvalidOperationException(
                "At least two players with chips are required.");

        DealerSeat = NextSeat(DealerSeat, active);

        if (active.Count == 2)
            SmallBlindSeat = DealerSeat;
        else
            SmallBlindSeat = NextSeat(DealerSeat, active);

        BigBlindSeat = NextSeat(SmallBlindSeat, active);

        deck.Reset();
        deck.Shuffle();

        CommunityCards.Clear();
        IsPotSettled = false;

        foreach (var player in Players)
            player.ResetForNewHand();

        int firstSeat = NextSeat(DealerSeat, active);
        var dealOrder = RotateFrom(active, firstSeat);

        foreach (var player in dealOrder)
            player.HoleCards[0] = deck.Deal();

        foreach (var player in dealOrder)
            player.HoleCards[1] = deck.Deal();

        PostBlind(SmallBlindSeat, SmallBlind);
        PostBlind(BigBlindSeat, BigBlind);

        Betting = new BettingManager(Players, BigBlind);

        int firstToAct = FindNextActiveSeat(BigBlindSeat);

        Betting.StartRound(firstToAct, true);

        Stage = PokerStage.PreFlop;
    }

    private void PostBlind(int seat, int amount)
    {
        PokerPlayer player = Players.Find(p => p.SeatIndex == seat);
        int paid = Math.Min(player.Chips, amount);

        player.Chips -= paid;
        player.CurrentBet += paid;
        player.TotalContribution += paid;
        player.IsAllIn = player.Chips == 0;
    }

    // Returns true when only one non-folded player remains.
    // Awards the entire pot without evaluating cards.
    public bool TryFinishIfEveryoneFolded()
    {
        if (IsPotSettled)
            return false;

        if (Stage != PokerStage.PreFlop &&
            Stage != PokerStage.Flop &&
            Stage != PokerStage.Turn &&
            Stage != PokerStage.River)
            return false;

        var remaining = Players.Where(p =>
            !p.IsFolded && p.TotalContribution > 0).ToList();

        if (remaining.Count != 1)
            return false;

        PokerPlayer winner = remaining[0];

        winner.Chips += Pot;

        IsPotSettled = true;
        Stage = PokerStage.Showdown;

        return true;
    }

    public bool TryAdvanceBettingRound()
    {
        // Check for an uncontested winner before requiring
        // the betting round to be complete.
        if (TryFinishIfEveryoneFolded())
            return true;

        if (IsPotSettled ||
            Betting == null ||
            !Betting.IsRoundComplete())
            return false;

        if (Stage != PokerStage.PreFlop &&
            Stage != PokerStage.Flop &&
            Stage != PokerStage.Turn &&
            Stage != PokerStage.River)
            return false;

        AdvanceStage();

        if (Stage == PokerStage.Showdown)
            return true;

        int firstToAct = FindNextActiveSeat(DealerSeat);

        if (firstToAct == -1)
            return true;

        Betting.StartRound(firstToAct, false);

        return true;
    }

    public void AdvanceStage()
    {
        if (IsPotSettled)
            throw new InvalidOperationException(
                "Cannot advance a settled hand.");

        switch (Stage)
        {
            case PokerStage.PreFlop:
                deck.Deal();
                DealCommunity(3);
                Stage = PokerStage.Flop;
                break;

            case PokerStage.Flop:
                deck.Deal();
                DealCommunity(1);
                Stage = PokerStage.Turn;
                break;

            case PokerStage.Turn:
                deck.Deal();
                DealCommunity(1);
                Stage = PokerStage.River;
                break;

            case PokerStage.River:
                Stage = PokerStage.Showdown;
                break;

            default:
                throw new InvalidOperationException(
                    "Cannot advance from current stage.");
        }

        foreach (var player in Players)
            player.CurrentBet = 0;
    }

    private void DealCommunity(int count)
    {
        for (int i = 0; i < count; i++)
            CommunityCards.Add(deck.Deal());
    }

    public List<PokerPlayer> DetermineWinners()
    {
        if (Stage != PokerStage.Showdown)
            throw new InvalidOperationException(
                "Showdown has not started.");

        var eligible = Players.Where(p =>
            !p.IsFolded && p.TotalContribution > 0).ToList();

        if (eligible.Count == 0)
            throw new InvalidOperationException("No eligible players.");

        // An uncontested winner does not require five cards.
        if (eligible.Count == 1)
            return eligible;

        HandResult best = null;
        var winners = new List<PokerPlayer>();

        foreach (var player in eligible)
        {
            var cards = new List<Card>(CommunityCards);
            cards.AddRange(player.HoleCards);

            HandResult result = HandEvaluator.Evaluate(cards);

            if (best == null || result.CompareTo(best) > 0)
            {
                best = result;
                winners.Clear();
                winners.Add(player);
            }
            else if (result.CompareTo(best) == 0)
            {
                winners.Add(player);
            }
        }

        return winners;
    }

    public PotSettlement SettlePot()
    {
        if (Stage != PokerStage.Showdown)
            throw new InvalidOperationException(
                "Cannot settle before showdown.");

        if (IsPotSettled)
            throw new InvalidOperationException(
                "Pot has already been settled.");

        var hands = new Dictionary<int, HandResult>();

        foreach (var player in Players)
        {
            if (player.IsFolded || player.TotalContribution <= 0)
                continue;

            var cards = new List<Card>(CommunityCards);
            cards.AddRange(player.HoleCards);

            hands[player.SeatIndex] = HandEvaluator.Evaluate(cards);
        }

        PotSettlement result =
            SidePotCalculator.Settle(Players, hands, DealerSeat);

        IsPotSettled = true;

        return result;
    }

    private int NextSeat(int currentSeat, List<PokerPlayer> active)
    {
        foreach (var player in active)
        {
            if (player.SeatIndex > currentSeat)
                return player.SeatIndex;
        }

        return active[0].SeatIndex;
    }

    private List<PokerPlayer> RotateFrom(
        List<PokerPlayer> active, int firstSeat)
    {
        int index = active.FindIndex(p => p.SeatIndex == firstSeat);
        var result = new List<PokerPlayer>();

        for (int i = 0; i < active.Count; i++)
            result.Add(active[(index + i) % active.Count]);

        return result;
    }

    private int FindNextActiveSeat(int fromSeat)
    {
        for (int offset = 1; offset <= 6; offset++)
        {
            int seat = (fromSeat + offset) % 6;

            PokerPlayer player = Players.Find(p =>
                p.SeatIndex == seat &&
                !p.IsFolded &&
                !p.IsAllIn &&
                p.Chips > 0);

            if (player != null)
                return seat;
        }

        return -1;
    }
}
