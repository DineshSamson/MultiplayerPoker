
using System;
using System.Collections.Generic;
using System.Linq;

public class SidePot
{
    public int Amount { get; }
    public List<PokerPlayer> EligiblePlayers { get; }

    public SidePot(int amount, List<PokerPlayer> eligible)
    {
        Amount = amount;
        EligiblePlayers = eligible;
    }
}

public class PotSettlement
{
    public List<SidePot> Pots { get; } = new();
    public Dictionary<int, int> Payouts { get; } = new();
    public Dictionary<int, int> Refunds { get; } = new();
}

public static class SidePotCalculator
{
    public static PotSettlement Settle(
        List<PokerPlayer> players,
        Dictionary<int, HandResult> hands,
        int dealerSeat)
    {
        var result = new PotSettlement();

        if (players == null || hands == null)
            throw new ArgumentNullException();

        int totalBefore = players.Sum(p => p.TotalContribution);

        var contributions = players.ToDictionary(
            p => p.SeatIndex,
            p => p.TotalContribution);

        if (contributions.Values.Any(v => v < 0))
            throw new InvalidOperationException(
                "Negative contribution.");

        // Return unmatched contribution above the
        // second-highest contribution.
        var ordered = contributions.Values
            .OrderByDescending(v => v).ToList();

        if (ordered.Count >= 2 && ordered[0] > ordered[1])
        {
            int max = ordered[0];
            int second = ordered[1];

            int seat = contributions.First(x => x.Value == max).Key;
            int refund = max - second;

            contributions[seat] -= refund;
            result.Refunds[seat] = refund;
        }

        var levels = contributions.Values
            .Where(v => v > 0)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        int previous = 0;

        foreach (int level in levels)
        {
            var contributors = players
                .Where(p => contributions[p.SeatIndex] >= level)
                .ToList();

            int amount = (level - previous) * contributors.Count;

            var eligible = contributors
                .Where(p => !p.IsFolded)
                .ToList();

            if (amount > 0)
            {
                if (eligible.Count == 0)
                    throw new InvalidOperationException(
                        "Pot has no eligible player.");

                result.Pots.Add(new SidePot(amount, eligible));
            }

            previous = level;
        }

        foreach (SidePot pot in result.Pots)
        {
            HandResult best = null;
            var winners = new List<PokerPlayer>();

            foreach (var player in pot.EligiblePlayers)
            {
                if (!hands.TryGetValue(player.SeatIndex, out var hand))
                    throw new InvalidOperationException(
                        $"Missing hand for seat {player.SeatIndex}");

                int comparison = best == null
                    ? 1 : hand.CompareTo(best);

                if (comparison > 0)
                {
                    best = hand;
                    winners.Clear();
                    winners.Add(player);
                }
                else if (comparison == 0)
                {
                    winners.Add(player);
                }
            }

            int share = pot.Amount / winners.Count;
            int remainder = pot.Amount % winners.Count;

            foreach (var winner in winners)
                AddAmount(result.Payouts, winner.SeatIndex, share);

            // Odd chips: first eligible winner clockwise
            // from the dealer button.
            winners.Sort((a, b) =>
                ClockwiseDistance(a.SeatIndex, dealerSeat)
                .CompareTo(
                    ClockwiseDistance(b.SeatIndex, dealerSeat)));

            for (int i = 0; i < remainder; i++)
                AddAmount(result.Payouts, winners[i].SeatIndex, 1);
        }

        int totalDistributed =
            result.Payouts.Values.Sum() +
            result.Refunds.Values.Sum();

        if (totalDistributed != totalBefore)
            throw new InvalidOperationException(
                $"Chip mismatch: {totalBefore} vs {totalDistributed}");

        // Apply settlement only after validation.
        foreach (var player in players)
        {
            if (result.Payouts.TryGetValue(
                player.SeatIndex, out int payout))
                player.Chips += payout;

            if (result.Refunds.TryGetValue(
                player.SeatIndex, out int refund))
                player.Chips += refund;
        }

        return result;
    }

    private static void AddAmount(
        Dictionary<int, int> amounts, int seat, int amount)
    {
        if (!amounts.ContainsKey(seat))
            amounts[seat] = 0;

        amounts[seat] += amount;
    }

    private static int ClockwiseDistance(int seat, int dealer)
    {
        return (seat - dealer + 5) % 6;
    }
}
