
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

    [Header("Additional Seat Visuals")]
    [SerializeField] private Image seatBackground;
    [SerializeField] private Sprite normalSprite, activeSprite, foldedSprite, allInSprite, disconnectedSprite;
    [SerializeField] private CanvasGroup seatArtworkGroup;
    [SerializeField] private PokerTurnTimerUI turnTimer;

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

        bool active = isCurrentTurn && player.IsConnected && !player.IsFolded && !player.IsAllIn;
        if (turnIndicator != null)
            turnIndicator.enabled = active;
        Sprite sprite = !player.IsConnected ? disconnectedSprite :
            player.IsFolded ? foldedSprite : player.IsAllIn ? allInSprite :
            active ? activeSprite : normalSprite;
        if (seatBackground != null)
        {
            if (sprite != null) seatBackground.sprite = sprite;
            else if (normalSprite != null) seatBackground.sprite = normalSprite;
        }
        if (seatArtworkGroup != null)
            seatArtworkGroup.alpha = !player.IsConnected ? 0.5f : player.IsFolded ? 0.6f : player.IsAllIn ? 0.8f : 1f;
        if (turnTimer != null) turnTimer.SetSeat(player.SeatIndex);
    }

    public void ClearSeat()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
