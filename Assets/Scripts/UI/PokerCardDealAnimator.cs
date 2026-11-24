
using System;
using System.Collections;
using UnityEngine;

public class PokerCardDealAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform deckPosition;
    [SerializeField] private RectTransform animationLayer;
    [SerializeField] private RectTransform flyingCardPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.3f;
    [SerializeField] private float cardInterval = 0.12f;

    public float CardInterval => cardInterval;

    public IEnumerator DealCard(
        RectTransform target,
        Action onComplete = null)
    {
        if (target == null)
            yield break;

        RectTransform flyingCard = Instantiate(
            flyingCardPrefab, animationLayer);

        flyingCard.anchorMin = new Vector2(0.5f, 0.5f);
        flyingCard.anchorMax = new Vector2(0.5f, 0.5f);
        flyingCard.pivot = new Vector2(0.5f, 0.5f);

        flyingCard.localScale = Vector3.one;
        flyingCard.localRotation = Quaternion.identity;

        Vector3 startPosition =
            GetCenterPosition(deckPosition);

        flyingCard.position = startPosition;

        Vector2 startSize = flyingCard.sizeDelta;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, moveDuration);

        while (elapsed < duration)
        {
            if (target == null)
            {
                Destroy(flyingCard.gameObject);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration);

            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            Vector3 targetPosition =
                GetCenterPosition(target);

            flyingCard.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                smoothT);

            Vector2 targetSize = new Vector2(
                target.rect.width *
                Mathf.Abs(target.lossyScale.x) /
                Mathf.Max(0.0001f,
                    Mathf.Abs(animationLayer.lossyScale.x)),

                target.rect.height *
                Mathf.Abs(target.lossyScale.y) /
                Mathf.Max(0.0001f,
                    Mathf.Abs(animationLayer.lossyScale.y)));

            flyingCard.sizeDelta = Vector2.Lerp(
                startSize,
                targetSize,
                smoothT);

            yield return null;
        }

        Destroy(flyingCard.gameObject);

        onComplete?.Invoke();
    }

    private Vector3 GetCenterPosition(
        RectTransform rect)
    {
        return rect.TransformPoint(rect.rect.center);
    }

    public void ClearFlyingCards()
    {
        StopAllCoroutines();

        for (int i = animationLayer.childCount - 1;
             i >= 0; i--)
        {
            GameObject card =
                animationLayer.GetChild(i).gameObject;

            card.SetActive(false);
            Destroy(card);
        }
    }
}
