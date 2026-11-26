using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;

public class PokerTurnTimerUI : MonoBehaviour
{
    [SerializeField] private Image ring;
    [SerializeField] private TMP_Text secondsText;
    private int seat = -1;
    public void SetSeat(int index) { seat = index; }
    private void OnDisable() { Hide(); }
    private void Hide()
    {
        if (ring != null) ring.enabled = false;
        if (secondsText != null) secondsText.enabled = false;
    }
    private void Update()
    {
        var table = PokerTableManager.Instance;
        var net = NetworkManager.Singleton;
        if (table == null || !table.IsSpawned || net == null || !net.IsConnectedClient ||
            seat < 0 || table.CurrentTurnSeat.Value != seat || table.TurnDeadline.Value < 0)
        { Hide(); return; }
        float remaining = Mathf.Max(0f, (float)(table.TurnDeadline.Value - net.ServerTime.Time));
        float fraction = Mathf.Clamp01(remaining / Mathf.Max(1f, table.TurnDuration.Value));
        if (ring != null) { ring.enabled = true; ring.fillAmount = fraction; }
        if (secondsText != null) secondsText.enabled = false;
    }
}
