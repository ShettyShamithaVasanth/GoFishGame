using UnityEngine;

public class DeckDisplayManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public CardData cardData;
    private Deck deck;

    void Start()
    {
        // deck = new Deck();
        DisplayAllCards();
    }

    void DisplayAllCards()
    {
        int index = 0;

        foreach (Card card in deck.GetAllCards())
        {
            GameObject newCard = Instantiate(cardPrefab, transform);
            newCard.transform.localPosition =
                new Vector3((index % 13) * 1.5f - 9f,
                            -(index / 13) * 2.2f + 5f, 0);

            UICard uiCard = newCard.GetComponent<UICard>();
            uiCard.cardData = cardData;

            uiCard.SetCard(card.Rank,
     cardData.GetSuitSprite(card.Suit),
     cardData.SuitColors[(int)card.Suit], null);

            uiCard.ShowFront(true);
            index++;
        }
    }
}
