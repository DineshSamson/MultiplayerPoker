
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PokerCardUI : MonoBehaviour
{
    [Header("Card References")]
    [SerializeField] private Image frontImage;
    [SerializeField] private Image backImage;

    [Header("Sprite Database")]
    [SerializeField] private CardSpriteDatabase spriteDatabase;

    [Header("Animation")]
    [SerializeField] private bool animateFlip = true;
    [SerializeField] private float flipDuration = 0.4f;

    private string currentCardValue;
    private bool isRevealed;
    private Coroutine flipCoroutine;

    private void Awake()
    {
        if (spriteDatabase != null)
            backImage.sprite = spriteDatabase.CardBack;
    }

    public void ShowBack()
    {
        if (gameObject.activeSelf &&
            !isRevealed &&
            backImage.gameObject.activeSelf)
            return;

        StopFlip();

        currentCardValue = null;
        isRevealed = false;

        transform.localRotation = Quaternion.identity;

        gameObject.SetActive(true);
        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);
    }

    public void ShowCard(string value)
    {
        if (currentCardValue == value &&
            gameObject.activeSelf &&
            isRevealed)
            return;

        if (!TryParseCard(value, out Card card))
            return;

        Sprite sprite = spriteDatabase.GetSprite(card);

        if (sprite == null)
        {
            Debug.LogWarning($"Missing sprite: {value}");
            return;
        }

        StopFlip();

        currentCardValue = value;
        isRevealed = true;

        gameObject.SetActive(true);
        frontImage.sprite = sprite;

        if (animateFlip && flipDuration > 0f)
            flipCoroutine = StartCoroutine(FlipCard());
        else
            ShowFrontImmediately();
    }

    public void HideCard()
    {
        StopFlip();

        currentCardValue = null;
        isRevealed = false;

        transform.localRotation = Quaternion.identity;

        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);

        gameObject.SetActive(false);
    }

    private IEnumerator FlipCard()
    {
        float halfDuration = flipDuration * 0.5f;

        frontImage.gameObject.SetActive(false);
        backImage.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / halfDuration);

            transform.localRotation =
                Quaternion.Euler(0f, 90f * t, 0f);

            yield return null;
        }

        backImage.gameObject.SetActive(false);
        frontImage.gameObject.SetActive(true);

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / halfDuration);

            transform.localRotation =
                Quaternion.Euler(
                    0f, Mathf.Lerp(-90f, 0f, t), 0f);

            yield return null;
        }

        transform.localRotation = Quaternion.identity;
        flipCoroutine = null;
    }

    private void ShowFrontImmediately()
    {
        transform.localRotation = Quaternion.identity;

        frontImage.gameObject.SetActive(true);
        backImage.gameObject.SetActive(false);
    }

    private void StopFlip()
    {
        if (flipCoroutine != null)
        {
            StopCoroutine(flipCoroutine);
            flipCoroutine = null;
        }
    }

    private bool TryParseCard(
        string value, out Card card)
    {
        card = default;

        if (string.IsNullOrEmpty(value))
            return false;

        string[] parts = value.Split(
            new[] { " of " },
            StringSplitOptions.None);

        if (parts.Length != 2)
            return false;

        if (!Enum.TryParse(parts[0], true,
            out CardRank rank))
            return false;

        if (!Enum.TryParse(parts[1], true,
            out CardSuit suit))
            return false;

        if ((int)rank < 2 || (int)rank > 14 ||
            (int)suit < 0 || (int)suit > 3)
            return false;

        card = new Card(suit, rank);
        return true;
    }
}
