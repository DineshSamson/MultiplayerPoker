using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections;

public class NetworkTestUI : MonoBehaviour
{
    [SerializeField] private GameObject connectionPanel;
    [SerializeField] private GameObject connectedPanel;
    [SerializeField] private Button startServerButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text connectionStatusText;

    private bool reloadPending;
    private bool clientSession;
    private bool connecting;
    private bool stopping;
    private bool wasConnected;
    private float connectDeadline;
    private string idleMessage = "Not connected";

    private void OnEnable()
    {
        startServerButton.onClick.AddListener(StartServer);
        startClientButton.onClick.AddListener(StartClient);
        disconnectButton.onClick.AddListener(Disconnect);
#if UNITY_WEBGL && !UNITY_EDITOR
        startServerButton.gameObject.SetActive(false);
#endif
        Refresh();
    }

    private void OnDisable()
    {
        startServerButton.onClick.RemoveListener(StartServer);
        startClientButton.onClick.RemoveListener(StartClient);
        disconnectButton.onClick.RemoveListener(Disconnect);
    }

    private void Update() => Refresh();

    private void StartServer() => StartNetwork(true);
    private void StartClient() => StartNetwork(false);

    private void StartNetwork(bool server)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (server) return;
#endif
        var network = NetworkManager.Singleton;
        if (network == null || network.IsListening || connecting || stopping)
            return;

        idleMessage = "Not connected";
        clientSession = !server;
        connecting = !server;
        connectDeadline = Time.realtimeSinceStartup + 30f;
        bool started = server ? network.StartServer() : network.StartClient();
        if (!started)
        {
            connecting = false;
            clientSession = false;
            idleMessage = server ? "Could not start server" : "Could not start client";
        }
        Refresh();
    }

    private void Disconnect()
    {
        var network = NetworkManager.Singleton;
        connecting = false;
        wasConnected = false;
        idleMessage = "Disconnected";
        if (network != null && network.IsListening)
        {
            stopping = true;
            network.Shutdown();
        }
        if (clientSession) BeginReload();
        Refresh();
    }

    private void BeginReload()
    {
        if (reloadPending) return;
        reloadPending = true;
        StartCoroutine(ReloadClientScene());
    }

    private IEnumerator ReloadClientScene()
    {
        // Wait until NGO has fully completed shutdown.
        yield return null;
        while (NetworkManager.Singleton != null &&
               (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress))
            yield return null;

        // NGO may preserve its manager across scene loads. Remove the old
        // local manager so the scene can create a fresh one.
        var network = NetworkManager.Singleton;
        if (network != null)
        {
            Destroy(network.gameObject);
            yield return null;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Refresh()
    {
        var network = NetworkManager.Singleton;
        bool listening = network != null && network.IsListening;
        bool server = listening && network.IsServer;
        bool connected = listening && (server || network.IsConnectedClient);

        if (reloadPending)
        {
            connectionPanel.SetActive(true);
            connectedPanel.SetActive(false);
            startServerButton.interactable = false;
            startClientButton.interactable = false;
            SetText(statusText, "Resetting connection...");
            return;
        }
        if (clientSession && !listening && wasConnected)
        {
            BeginReload();
            return;
        }
        if (!listening) stopping = false;
        if (connected && !stopping)
        {
            connecting = false;
            wasConnected = true;
        }
        else if (!listening && wasConnected)
        {
            wasConnected = false;
            idleMessage = "Connection lost. Please try again.";
        }

        if (connecting && (!listening || Time.realtimeSinceStartup >= connectDeadline))
        {
            connecting = false;
            idleMessage = "Connection failed. Please try again.";
            if (listening)
            {
                stopping = true;
                network.Shutdown();
            }
            if (clientSession)
            {
                BeginReload();
                return;
            }
        }

        bool showConnected = connected && !stopping;
        if (connectionPanel.activeSelf == showConnected)
            connectionPanel.SetActive(!showConnected);
        if (connectedPanel.activeSelf != showConnected)
            connectedPanel.SetActive(showConnected);

        bool canStart = network != null && !listening && !connecting && !stopping;
        startServerButton.interactable = canStart;
        startClientButton.interactable = canStart;
        disconnectButton.interactable = !stopping;
        SetText(statusText, network == null ? "NetworkManager unavailable" :
            stopping ? "Disconnecting..." : connecting || (listening && !connected) ? "Connecting..." : idleMessage);
        if (showConnected)
            SetText(connectionStatusText, server ?
                $"Server Running | Clients: {network.ConnectedClientsIds.Count}" : "Client Connected");
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value)
            target.text = value;
    }
}
