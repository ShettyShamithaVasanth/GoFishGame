using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private List<Card> cards = new List<Card>();
    // private GameManager gameManager;
    public Deck()
    {
        // this.gameManager = gameManager;
        CreateDeck();
        //Shuffle();
    }

    // 1️⃣ Create 52 Cards
    public void CreateDeck()
    {
        cards.Clear();

        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                cards.Add(new Card(rank, suit));
            }
        }
    }

    // 2️⃣ Shuffle Deck
    public void Shuffle()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int randomIndex = Random.Range(i, cards.Count);

            Card temp = cards[i];
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }

    // 3️⃣ Get (Draw) Top Card
    public Card GetCard()
    {
        if (cards.Count == 0)
        {
            Debug.LogWarning("Deck is empty!");
            return null;
        }

        Card topCard = cards[0];
        cards.RemoveAt(0);
        return topCard;
    }

    // 4️⃣ Return Card To Deck
    public void ReturnCard(Card card)
    {
        cards.Add(card);
    }

    // 5️⃣ Get All Cards (For Display Testing)
    public List<Card> GetAllCards()
    {
        return cards;
    }

    public int CardCount()
    {
        return cards.Count;
    }
}
