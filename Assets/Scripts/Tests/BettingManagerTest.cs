
using UnityEngine;
using System.Collections.Generic;

public class BettingManagerTest : MonoBehaviour
{
    private void Start()
    {
        var players = new List<PokerPlayer>
        {
            new PokerPlayer(0),
            new PokerPlayer(1),
            new PokerPlayer(2)
        };

        // Simulate blinds.
        players[1].Chips -= 10;
        players[1].CurrentBet = 10;
        players[1].TotalContribution = 10;

        players[2].Chips -= 20;
        players[2].CurrentBet = 20;
        players[2].TotalContribution = 20;

        var betting = new BettingManager(players, 20);
        betting.StartRound(0, true);

        Debug.Log("Starting turn: " + betting.CurrentTurnSeat);

        TestAction(betting, 0, PokerAction.Call, 0);
        TestAction(betting, 1, PokerAction.Call, 0);
        TestAction(betting, 2, PokerAction.Check, 0);

        Debug.Log("Round complete: " + betting.IsRoundComplete());

        foreach (var player in players)
        {
            Debug.Log(
                $"Seat {player.SeatIndex} | " +
                $"Chips: {player.Chips} | " +
                $"Bet: {player.CurrentBet} | " +
                $"Total: {player.TotalContribution}"
            );
        }
    }

    private void TestAction(
        BettingManager betting, int seat,
        PokerAction action, int raiseTo)
    {
        bool success =
            betting.TryAction(seat, action, raiseTo, out string error);

        Debug.Log(
            $"Seat {seat}: {action} | " +
            $"Success: {success} | " +
            $"Error: {error}"
        );
    }
}
