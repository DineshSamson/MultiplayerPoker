
using UnityEngine;
using Unity.Netcode;

public class NetworkTestUI : MonoBehaviour
{
    private void OnGUI()
    {
        var network = NetworkManager.Singleton;

        if (network == null)
            return;

        GUILayout.BeginArea(new Rect(20, 20, 280, 220));

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
            {
                GUILayout.Label(
                    "Connected Clients: " +
                    network.ConnectedClientsIds.Count
                );
            }

            if (GUILayout.Button("Disconnect"))
                network.Shutdown();
        }

        GUILayout.EndArea();
    }
}
