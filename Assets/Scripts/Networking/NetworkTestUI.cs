
using UnityEngine;
using Unity.Netcode;

public class NetworkTestUI : MonoBehaviour
{
    private void OnGUI()
    {
        var network = NetworkManager.Singleton;

        if (network == null)
            return;

        GUILayout.BeginArea(new Rect(20, 20, 350, 400));

        if (!network.IsListening)
        {
            if (GUILayout.Button("Start Server", GUILayout.Height(40)))
                network.StartServer();

            if (GUILayout.Button("Start Client", GUILayout.Height(40)))
                network.StartClient();
        }
        else
        {
            GUILayout.Label(network.IsServer
                ? "Server Running"
                : network.IsConnectedClient
                    ? "Client Connected"
                    : "Connecting...");

            if (network.IsServer)
                GUILayout.Label(
                    $"Connected Clients: {network.ConnectedClientsIds.Count}"
                );

            if (PokerTableManager.Instance != null &&
                PokerTableManager.Instance.IsSpawned)
            {
                var players = PokerTableManager.Instance.Players;

                GUILayout.Space(15);
                GUILayout.Label($"Poker Table ({players.Count}/6)");

                foreach (var player in players)
                {
                    GUILayout.Label(
                        $"Seat {player.SeatIndex + 1} | " +
                        $"Player {player.ClientId} | " +
                        $"{player.Chips} Chips"
                    );
                }
            }

            GUILayout.Space(15);

            if (GUILayout.Button("Disconnect"))
                network.Shutdown();
        }

        GUILayout.EndArea();
    }
}
