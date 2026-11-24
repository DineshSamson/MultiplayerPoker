
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PokerSeatUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text chipsText;
    [SerializeField] private TMP_Text betText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image turnIndicator;

    [Header("Private Card Position")]
    [SerializeField] private RectTransform cardHolder;

    public RectTransform CardHolder => cardHolder;

    public void SetPlayer(
        PokerPlayerData player,
        bool isLocalPlayer,
        bool isCurrentTurn)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        nameText.text = isLocalPlayer
            ? $"Seat {player.SeatIndex + 1} (YOU)"
            : $"Seat {player.SeatIndex + 1}";

        chipsText.text = $"Chips: {player.Chips}";
        betText.text = $"Bet: {player.CurrentBet}";

        var table = PokerTableManager.Instance;
        bool handComplete = table != null && table.IsSpawned &&
            table.CurrentStage.Value == PokerStage.Showdown;

        if (!player.IsConnected)
            statusText.text = "DISCONNECTED";
        else if (player.Chips <= 0 && (handComplete || !player.HasDealtCards))
            statusText.text = "OUT OF CHIPS";
        else if (!player.HasDealtCards)
            statusText.text = "WAITING FOR NEXT HAND";
        else if (handComplete)
            statusText.text = "HAND COMPLETE";
        else if (player.IsFolded)
            statusText.text = "FOLDED";
        else if (player.IsAllIn)
            statusText.text = "ALL-IN";
        else if (player.LastAction >= 0)
            statusText.text =
                ((PokerAction)player.LastAction).ToString();
        else
            statusText.text = "PLAYING";

        if (turnIndicator != null)
            turnIndicator.enabled = isCurrentTurn;
    }

    public void ClearSeat()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
