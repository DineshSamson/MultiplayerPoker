
using System;
using System.Collections.Generic;

public class BettingManager
{
    private readonly List<PokerPlayer> players;
    private readonly int bigBlind;

    public int CurrentBet { get; private set; }
    public int MinimumRaise { get; private set; }
    public int CurrentTurnSeat { get; private set; } = -1;

    public BettingManager(List<PokerPlayer> players, int bigBlind)
    {
        this.players = players;
        this.bigBlind = bigBlind;
        MinimumRaise = bigBlind;
    }

    public void StartRound(int firstSeat, bool preFlop)
    {
        MinimumRaise = bigBlind;

        if (preFlop)
        {
            CurrentBet = bigBlind;
        }
        else
        {
            CurrentBet = 0;

            foreach (var player in players)
            {
                player.CurrentBet = 0;
                player.HasActed = false;
                player.LastFacedBet = 0;
            }
        }

        foreach (var player in players)
        {
            player.HasActed = false;
            player.LastFacedBet = 0;
        }

        //CurrentTurnSeat = FindNextEligible(firstSeat - 1);
        CurrentTurnSeat = firstSeat < 0 ? -1 : FindNextEligible((firstSeat + 5) % 6);
    }

    public bool TryAction(
        int seat, PokerAction action, int raiseTo,
        out string error)
    {
        error = null;

        PokerPlayer player =
            players.Find(p => p.SeatIndex == seat);

        if (player == null)
        {
            error = "Player not found.";
            return false;
        }

        if (seat != CurrentTurnSeat)
        {
            error = "Not your turn.";
            return false;
        }

        if (player.IsFolded || player.IsAllIn || player.Chips <= 0)
        {
            error = "Player cannot act.";
            return false;
        }

        int toCall = Math.Max(0, CurrentBet - player.CurrentBet);

        switch (action)
        {
            case PokerAction.Fold:
                player.IsFolded = true;
                break;

            case PokerAction.Check:
                if (toCall != 0)
                {
                    error = "Cannot check when facing a bet.";
                    return false;
                }
                break;

            case PokerAction.Call:
                if (toCall == 0)
                {
                    error = "Nothing to call.";
                    return false;
                }

                Commit(player, Math.Min(toCall, player.Chips));
                break;

            case PokerAction.Raise:
                {
                    if (!CanRaise(player))
                    {
                        error = "Betting is not reopened.";
                        return false;
                    }

                    int maxBet = player.CurrentBet + player.Chips;

                    if (raiseTo > maxBet || raiseTo <= CurrentBet)
                    {
                        error = "Invalid raise amount.";
                        return false;
                    }

                    int raiseSize = raiseTo - CurrentBet;

                    if (raiseSize < MinimumRaise)
                    {
                        error = "Raise is below minimum.";
                        return false;
                    }

                    Commit(player, raiseTo - player.CurrentBet);
                    CurrentBet = raiseTo;
                    MinimumRaise = raiseSize;
                    break;
                }

            case PokerAction.AllIn:
                {
                    int total = player.CurrentBet + player.Chips;

                    if (total > CurrentBet && !CanRaise(player))
                    {
                        error = "Betting is not reopened.";
                        return false;
                    }

                    int oldBet = CurrentBet;
                    Commit(player, player.Chips);

                    if (total > oldBet)
                    {
                        int raiseSize = total - oldBet;
                        CurrentBet = total;

                        if (raiseSize >= MinimumRaise)
                            MinimumRaise = raiseSize;
                    }
                    break;
                }

            default:
                error = "Unknown action.";
                return false;
        }

        player.HasActed = true;
        player.LastFacedBet = CurrentBet;
        player.LastAction = action;

        CurrentTurnSeat = FindNextEligible(seat);

        return true;
    }

    public void FoldDisconnectedPlayer(int seat)
    {
        PokerPlayer player = players.Find(p => p.SeatIndex == seat);
        if (player == null || player.IsFolded || player.IsAllIn)
            return;

        player.IsFolded = true;
        player.HasActed = true;
        player.LastAction = PokerAction.Fold;

        if (CurrentTurnSeat == seat)
            CurrentTurnSeat = FindNextEligible(seat);
    }

    public bool IsRoundComplete()
    {
        int activeCount = 0;
        PokerPlayer lastActive = null;
        foreach (var player in players)
        {
            if (player.IsFolded || player.IsAllIn || player.Chips <= 0)
                continue;
            activeCount++;
            lastActive = player;
        }
        if (activeCount == 0) return true;
        if (activeCount == 1)
            return lastActive.CurrentBet >= CurrentBet;

        foreach (var player in players)
        {
            if (player.IsFolded || player.IsAllIn || player.Chips <= 0)
                continue;
            if (!player.HasActed || player.CurrentBet != CurrentBet)
                return false;
        }
        return true;
    }

    private bool CanRaise(PokerPlayer player)
    {
        return !player.HasActed ||
            CurrentBet - player.LastFacedBet >= MinimumRaise;
    }

    private void Commit(PokerPlayer player, int amount)
    {
        player.Chips -= amount;
        player.CurrentBet += amount;
        player.TotalContribution += amount;

        if (player.Chips == 0)
            player.IsAllIn = true;
    }

    private int FindNextEligible(int afterSeat)
    {
        for (int offset = 1; offset <= 6; offset++)
        {
            int seat = (afterSeat + offset + 6) % 6;

            PokerPlayer player =
                players.Find(p => p.SeatIndex == seat);

            if (player != null &&
                !player.IsFolded &&
                !player.IsAllIn &&
                player.Chips > 0)
                return seat;
        }

        return -1;
    }
}
