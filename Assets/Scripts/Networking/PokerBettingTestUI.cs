
using UnityEngine;
using Unity.Netcode;

public class PokerBettingTestUI : MonoBehaviour
{
    private string raiseAmount = "60";

    private void OnGUI()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient)
            return;

        PokerTableManager table = PokerTableManager.Instance;

        if (table == null || !table.IsSpawned)
            return;

        int mySeat = -1;
        ulong myClientId = NetworkManager.Singleton.LocalClientId;

        foreach (var player in table.Players)
        {
            if (player.ClientId == myClientId)
            {
                mySeat = player.SeatIndex;
                break;
            }
        }

        bool isShowdown =
            table.CurrentStage.Value == PokerStage.Showdown;

        bool myTurn =
            mySeat >= 0 &&
            mySeat == table.CurrentTurnSeat.Value;

        // Scrollable area prevents controls from disappearing
        // when all six players are connected.
        GUILayout.BeginArea(
            new Rect(10, 130, 430, Screen.height - 145),
            GUI.skin.box);

        GUILayout.Label("MULTIPLAYER POKER TEST");
        GUILayout.Label($"Hand: {table.HandNumber.Value}");

        GUILayout.Label(
            mySeat < 0
                ? "You are a spectator"
                : $"Your Seat: {mySeat + 1}");

        GUILayout.Label($"Stage: {table.CurrentStage.Value}");
        GUILayout.Label($"Pot: {table.TotalPot.Value}");
        GUILayout.Label($"Current Bet: {table.CurrentBet.Value}");
        GUILayout.Label($"Minimum Raise: {table.MinimumRaise.Value}");

        GUILayout.Label(
            table.CurrentTurnSeat.Value < 0
                ? "Current Turn: None"
                : $"Current Turn: Seat " +
                  $"{table.CurrentTurnSeat.Value + 1}");

        GUILayout.Space(10);

        if (isShowdown)
        {
            GUILayout.Label("===== HAND FINISHED =====");

            string result = table.HandResultText.Value.ToString();

            GUILayout.Label(
                string.IsNullOrEmpty(result)
                    ? "Waiting for result..."
                    : result);

            GUI.enabled = mySeat >= 0;

            if (GUILayout.Button(
                "START NEW HAND",
                GUILayout.Height(40)))
            {
                table.RequestNewHand();
            }

            GUI.enabled = true;
            GUILayout.Space(10);
        }

        GUILayout.Label("===== PLAYERS =====");

        foreach (PokerPlayerData player in table.Players)
        {
            string status = "Playing";

            if (player.IsFolded)
                status = "Folded";
            else if (player.IsAllIn)
                status = "All-In";

            if (player.SeatIndex == table.CurrentTurnSeat.Value)
                status += " - TURN";

            string lastAction =
                player.LastAction < 0
                    ? "None"
                    : ((PokerAction)player.LastAction).ToString();

            string marker =
                player.SeatIndex == mySeat ? " (YOU)" : "";

            GUILayout.Label(
                $"Seat {player.SeatIndex + 1}{marker}");

            GUILayout.Label(
                $"Chips: {player.Chips} | " +
                $"Current Bet: {player.CurrentBet}");

            GUILayout.Label(
                $"Total Contribution: {player.TotalContribution}");

            GUILayout.Label(
                $"Status: {status} | Last Action: {lastAction}");

            GUILayout.Space(5);
        }

        GUILayout.Space(10);
        GUILayout.Label("===== MY PRIVATE CARDS =====");

        if (table.HasPrivateCards)
        {
            GUILayout.Label($"Card 1: {table.PrivateCard1}");
            GUILayout.Label($"Card 2: {table.PrivateCard2}");
        }
        else
        {
            GUILayout.Label("Waiting for private cards...");
        }

        GUILayout.Space(10);

        GUILayout.Label(
            $"===== COMMUNITY CARDS " +
            $"({table.CommunityCards.Count}/5) =====");

        foreach (var card in table.CommunityCards)
            GUILayout.Label(card.ToString());

        GUILayout.Space(10);

        if (!isShowdown)
        {
            GUI.enabled = myTurn;

            if (GUILayout.Button("FOLD", GUILayout.Height(30)))
                table.SubmitAction(PokerAction.Fold);

            if (GUILayout.Button("CHECK", GUILayout.Height(30)))
                table.SubmitAction(PokerAction.Check);

            if (GUILayout.Button("CALL", GUILayout.Height(30)))
                table.SubmitAction(PokerAction.Call);

            GUILayout.Label("Raise To (total bet):");
            raiseAmount = GUILayout.TextField(raiseAmount);

            if (GUILayout.Button("RAISE", GUILayout.Height(30)))
            {
                if (int.TryParse(raiseAmount, out int amount))
                    table.SubmitAction(PokerAction.Raise, amount);
            }

            if (GUILayout.Button("ALL-IN", GUILayout.Height(30)))
                table.SubmitAction(PokerAction.AllIn);

            GUI.enabled = true;
        }

        GUILayout.EndArea();
    }
}
