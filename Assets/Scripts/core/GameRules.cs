using System.Collections.Generic;

public static class GameRules
{
    public static (int rank, List<Card> cards)? CheckForBook(Player player)
    {
        Dictionary<int, List<Card>> rankGroups = new Dictionary<int, List<Card>>();
        
        foreach (Card card in player.PlayerHand.Cards)
        {
            if (!rankGroups.ContainsKey(card.Rank))
                rankGroups[card.Rank] = new List<Card>();
            rankGroups[card.Rank].Add(card);
        }

        foreach (var group in rankGroups)
        {
            if (group.Value.Count == 4)
            {
                return (group.Key, group.Value);
            }
        }
        return null;
    }

    public static Player GetWinner(Player[] players)
    {
        Player winner = players[0];
        foreach (Player p in players)
        {
            if (p.Score > winner.Score)
                winner = p;
        }
        return winner;
    }

    public static int GetNextPlayer(int[] activePlayers, int currentPlayer)
    {
        int currentIndex = System.Array.IndexOf(activePlayers, currentPlayer);
        currentIndex++;
        if (currentIndex >= activePlayers.Length)
            currentIndex = 0;
        return activePlayers[currentIndex];
    }
}