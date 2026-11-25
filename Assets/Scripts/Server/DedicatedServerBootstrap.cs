using UnityEngine;
using Unity.Netcode;

public class DedicatedServerBootstrap : MonoBehaviour
{
#if UNITY_SERVER

    private void Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 30;

        NetworkManager networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[Server] NetworkManager not found.");
            return;
        }

        if (networkManager.IsListening)
            return;

        bool started = networkManager.StartServer();

        if (started)
        {
            Debug.Log("[Server] Dedicated poker server started.");
        }
        else
        {
            Debug.LogError("[Server] Failed to start.");
        }
    }

#endif
}