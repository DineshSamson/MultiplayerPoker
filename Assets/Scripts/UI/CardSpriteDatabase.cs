
using UnityEngine;

[CreateAssetMenu(
    fileName = "CardSpriteDatabase",
    menuName = "Poker/Card Sprite Database")]
public class CardSpriteDatabase : ScriptableObject
{
    [Header("Order: Two to Ace")]
    [SerializeField] private Sprite[] clubs = new Sprite[13];
    [SerializeField] private Sprite[] diamonds = new Sprite[13];
    [SerializeField] private Sprite[] hearts = new Sprite[13];
    [SerializeField] private Sprite[] spades = new Sprite[13];

    [Header("Card Back")]
    [SerializeField] private Sprite cardBack;

    public Sprite CardBack => cardBack;

    public Sprite GetSprite(Card card)
    {
        int index = (int)card.Rank - 2;

        Sprite[] sprites;

        switch (card.Suit)
        {
            case CardSuit.Clubs:
                sprites = clubs;
                break;

            case CardSuit.Diamonds:
                sprites = diamonds;
                break;

            case CardSuit.Hearts:
                sprites = hearts;
                break;

            case CardSuit.Spades:
                sprites = spades;
                break;

            default:
                return null;
        }

        if (sprites == null ||
            index < 0 ||
            index >= sprites.Length)
            return null;

        return sprites[index];
    }
}
