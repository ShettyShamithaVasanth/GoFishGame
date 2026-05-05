using UnityEngine;
using System.Collections.Generic;
[System.Serializable]
public class Player
{
    public int PlayerID { get; private set; }
    public string PlayerName;
    public bool IsHuman;
    public Hand PlayerHand;
    public int AvatarIndex;
    public int Score { get; private set; } = 0;
    public int LastBookTurn = int.MaxValue;
    // private int publicCardCount=0;

    public bool IsMyTurn { get; private set; }

    public Player(int id, string name, bool isHuman, int avatarIndex = 0)
    {
        PlayerID = id;
        PlayerName = name;
        IsHuman = isHuman;
        AvatarIndex = avatarIndex;
        PlayerHand = new Hand();
    }
    public event System.Action<Player, bool> OnTurnChanged;
    public void StartTurn()
    {
        IsMyTurn = true;

        OnTurnChanged?.Invoke(this, true);

        Debug.Log(PlayerName + " Turn Started");
    }

    public void EndTurn()
    {
        IsMyTurn = false;

        OnTurnChanged?.Invoke(this, false);

        Debug.Log(PlayerName + " Turn Ended");
    }

    public void AddCard(Card card)
    {
        PlayerHand.AddCard(card);
    }

    public void RemoveCard(Card card)
    {
        PlayerHand.RemoveCard(card);
    }
    public void SetScore(int value)
    {
        Score = value;
    }

    public int CardCount()
    {
        return PlayerHand.Count();
    }
    public bool HasRank(int rank)
    {
        return PlayerHand.GetCardsByRank(rank).Count > 0;
    }

    public List<Card> GetCardsByRank(int rank)
    {
        return PlayerHand.GetCardsByRank(rank);
    }

    public List<Card> RemoveCardsByRank(int rank)
    {
        List<Card> cardsToRemove = PlayerHand.GetCardsByRank(rank);
        // Make a safe copy
        List<Card> removedCards = new List<Card>(cardsToRemove);

        foreach (Card c in removedCards)
        {
            PlayerHand.RemoveCard(c);
        }
        return removedCards;
    }
    public void AddPoint()
    {
        Score++;
    }

    // public void SetPublicCardCount(int count){
    //     publicCardCount=count;
    // }
    // public void GetPublicCardCount(){
    //     return publicCardCount;
    // }
}