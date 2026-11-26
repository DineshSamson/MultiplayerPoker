using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class PokerSessionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text handsPlayedText;
    [SerializeField] private GameObject reminderPanel;
    [SerializeField] private Toggle remindersEnabled;
    [SerializeField] private Button dismissButton;
    [SerializeField, Min(1f)] private float reminderSeconds = 1800f;
    private const string Preference = "PokerBreakReminders";
    private PokerTableManager sessionTable;
    private ulong sessionClient;
    private int hands, lastHand = -1;
    private double deadline;
    private bool enabledReminder;
    private void Start()
    {
        enabledReminder = PlayerPrefs.GetInt(Preference, 1) == 1;
        if (remindersEnabled != null) { remindersEnabled.SetIsOnWithoutNotify(enabledReminder); remindersEnabled.onValueChanged.AddListener(SetReminder); }
        if (dismissButton != null) dismissButton.onClick.AddListener(Dismiss);
        ResetSession();
    }
    private void OnDestroy()
    {
        if (remindersEnabled != null) remindersEnabled.onValueChanged.RemoveListener(SetReminder);
        if (dismissButton != null) dismissButton.onClick.RemoveListener(Dismiss);
    }
    private void SetReminder(bool value)
    {
        enabledReminder = value;
        PlayerPrefs.SetInt(Preference, value ? 1 : 0);
        PlayerPrefs.Save();
        Dismiss();
    }
    private void Dismiss()
    {
        if (reminderPanel != null) reminderPanel.SetActive(false);
        deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(1f, reminderSeconds);
    }
    private void ResetSession()
    {
        sessionTable = null; hands = 0; lastHand = -1;
        if (handsPlayedText != null) handsPlayedText.text = "Hands played: 0";
        Dismiss();
    }
    private void Update()
    {
        var net = NetworkManager.Singleton;
        var table = PokerTableManager.Instance;
        if (net == null || !net.IsConnectedClient || table == null || !table.IsSpawned)
        { if (sessionTable != null) ResetSession(); return; }
        bool seated = false, dealt = false;
        foreach (var player in table.Players)
            if (player.ClientId == net.LocalClientId && player.IsConnected)
            { seated = true; dealt = player.HasDealtCards; break; }
        if (!seated) { if (sessionTable != null) ResetSession(); return; }
        if (sessionTable != table || sessionClient != net.LocalClientId)
        { ResetSession(); sessionTable = table; sessionClient = net.LocalClientId; }
        int hand = table.HandNumber.Value;
        if (hand > 0 && dealt && hand != lastHand)
        { lastHand = hand; hands++; if (handsPlayedText != null) handsPlayedText.text = $"Hands played: {hands}"; }
        if (enabledReminder && reminderPanel != null && !reminderPanel.activeSelf && Time.realtimeSinceStartupAsDouble >= deadline)
            reminderPanel.SetActive(true);
    }
}
