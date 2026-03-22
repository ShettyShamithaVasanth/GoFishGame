using System;

[Serializable]
public class Card
{
    public int Rank;          // 1 - 13
    public CardSuit Suit;     // Spade, Heart, Diamond, Club

    public Card(int rank, CardSuit suit)
    {
        Rank = rank;
        Suit = suit;
    }
}
