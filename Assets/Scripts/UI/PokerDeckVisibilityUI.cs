
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class PokerDeckVisibilityUI : MonoBehaviour
{
    [SerializeField] private Image deckImage;

    private PokerTableManager table;

    private void Awake()
    {
        SetVisibility(false);
    }

    private void LateUpdate()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsClient)
        {
            SetVisibility(false);
            return;
        }

        if (table == null || !table.IsSpawned)
            table = PokerTableManager.Instance;

        if (table == null || !table.IsSpawned)
        {
            SetVisibility(false);
            return;
        }

        SetVisibility(
            table.CurrentStage.Value == PokerStage.PreFlop);
    }

    private void SetVisibility(bool visible)
    {
        if (deckImage != null &&
            deckImage.enabled != visible)
        {
            deckImage.enabled = visible;
        }
    }
}
