
using UnityEngine;
using System.Collections.Generic;

public class DeckTest : MonoBehaviour
{
    private void Start()
    {
        Deck deck = new Deck();
        deck.Shuffle();

        HashSet<Card> dealtCards = new HashSet<Card>();

        for (int i = 0; i < 52; i++)
        {
            Card card = deck.Deal();
            dealtCards.Add(card);

            Debug.Log($"Card {i + 1}: {card}");
        }

        Debug.Log($"Unique cards: {dealtCards.Count}");
        Debug.Log($"Remaining cards: {deck.RemainingCards}");
    }
}
