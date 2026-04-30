using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class NetworkDeckManager : NetworkBehaviour
{
    public static NetworkDeckManager Instance;
    public List<int> deck = new List<int>();

    private void Awake()
    {
        Instance = this;
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CreateDeck();
            ShuffleDeck();
        }
    }

    void CreateDeck()
    {
        deck.Clear();
        for (int suit = 0; suit < 4; suit++)
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                int cardValue = (rank * 10) + suit;
                deck.Add(cardValue);
            }
        }
        Debug.Log("Deck Created: " + deck.Count);
    }

    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int rand = Random.Range(i, deck.Count);
            (deck[i], deck[rand]) = (deck[rand], deck[i]);
        }
        Debug.Log("Deck Shuffled");
    }

    public int DrawCard()
    {
        if (deck.Count == 0) return -1;
        int card = deck[0];
        deck.RemoveAt(0);
        return card;
    }
}
