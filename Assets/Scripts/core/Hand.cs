using System.Collections.Generic;

public class Hand
{
    private List<Card> cards = new List<Card>();
    public List<Card> Cards => cards;

    public void AddCard(Card card)
    {
        cards.Add(card);
        SortHand(); // 🔥 Important
    }
    public void RemoveCard(Card card)
    {
        cards.Remove(card);
    }
    public void Clear()
    {
        Cards.Clear();
    }

    public int Count()
    {
        return cards.Count;
    }

    public List<Card> GetCardsByRank(int rank)
    {
        return cards.FindAll(c => c.Rank == rank);
    }

    // 🔥 ORDER CONTROLLED HERE
    private void SortHand()
    {
        cards.Sort((a, b) =>
        {
            if (a.Rank == b.Rank)
            {
                return a.Suit.CompareTo(b.Suit);
            }

            return a.Rank.CompareTo(b.Rank);
        });
    }
}
