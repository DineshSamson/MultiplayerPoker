
using UnityEngine;
using Unity.Netcode;

public class PokerBlindIndicatorsUI : MonoBehaviour
{
    [Header("Seat Index")]
    [SerializeField, Range(0, 5)]
    private int seatIndex;

    [Header("Indicators")]
    [SerializeField] private GameObject dealerIndicator;
    [SerializeField] private GameObject smallBlindIndicator;
    [SerializeField] private GameObject bigBlindIndicator;

    private PokerTableManager table;

    private void OnEnable()
    {
        HideAllIndicators();
    }

    private void LateUpdate()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient)
        {
            HideAllIndicators();
            return;
        }

        if (table == null || !table.IsSpawned)
            table = PokerTableManager.Instance;

        if (table == null || !table.IsSpawned)
        {
            HideAllIndicators();
            return;
        }

        if (table.CurrentStage.Value == PokerStage.Waiting)
        {
            HideAllIndicators();
            return;
        }

        RefreshIndicators();
    }

    private void RefreshIndicators()
    {
        SetIndicator(
            dealerIndicator,
            table.DealerSeat.Value == seatIndex);

        SetIndicator(
            smallBlindIndicator,
            table.SmallBlindSeat.Value == seatIndex);

        SetIndicator(
            bigBlindIndicator,
            table.BigBlindSeat.Value == seatIndex);
    }

    private void HideAllIndicators()
    {
        SetIndicator(dealerIndicator, false);
        SetIndicator(smallBlindIndicator, false);
        SetIndicator(bigBlindIndicator, false);
    }

    private void SetIndicator(
        GameObject indicator, bool visible)
    {
        if (indicator != null &&
            indicator.activeSelf != visible)
        {
            indicator.SetActive(visible);
        }
    }
}
