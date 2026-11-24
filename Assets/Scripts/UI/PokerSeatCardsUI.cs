using UnityEngine;

public class PokerSeatCardsUI : MonoBehaviour
{
    [SerializeField] private PokerCardUI card1;
    [SerializeField] private PokerCardUI card2;
    private CanvasGroup group1;
    private CanvasGroup group2;

    private CanvasGroup GetGroup(PokerCardUI card)
    {
        CanvasGroup group = card.GetComponent<CanvasGroup>();
        if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
        return group;
    }

    private void EnsureGroups()
    {
        if (group1 == null) group1 = GetGroup(card1);
        if (group2 == null) group2 = GetGroup(card2);
    }

    public void PrepareDeal()
    {
        EnsureGroups();
        group1.alpha = group2.alpha = 0f;
        card1.ShowBack();
        card2.ShowBack();
    }

    public RectTransform GetTarget(int index)
    {
        return (RectTransform)(index == 0 ? card1.transform : card2.transform);
    }

    public void ShowDealtCard(int index)
    {
        EnsureGroups();
        (index == 0 ? group1 : group2).alpha = 1f;
    }

    public void ShowFaceDown()
    {
        EnsureGroups();
        group1.alpha = group2.alpha = 1f;
        card1.ShowBack();
        card2.ShowBack();
    }

    public void ShowFaceUp(string first, string second)
    {
        EnsureGroups();
        group1.alpha = group2.alpha = 1f;
        card1.ShowCard(first);
        card2.ShowCard(second);
    }

    public void HideCards()
    {
        card1.HideCard();
        card2.HideCard();
    }
}
