using System.Collections.Generic;
using Unity.Netcode;

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
        Player winner = null;

        foreach (Player p in players)
        {
            // Skip empty slots
            if (p == null)
                continue;

            // First valid player becomes winner
            if (winner == null)
            {
                winner = p;
                continue;
            }

            // Compare scores
            if (p.Score > winner.Score)
            {
                winner = p;
            }
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

    public static bool HasValidMoves(
    Player[] players,
    int[] activePlayers,
    int currentPlayer,
    HashSet<string> askedRankTargetThisTurn,
    HashSet<int> completedRanks)
    {
        Player current = players[currentPlayer];

        // Collect unique non-completed ranks in player's hand
        HashSet<int> handRanks = new HashSet<int>();

        foreach (Card c in current.PlayerHand.Cards)
        {
            if (!completedRanks.Contains(c.Rank))
                handRanks.Add(c.Rank);
        }

        // Check if ANY target/rank combination is still available
        foreach (int rank in handRanks)
        {
            foreach (int targetId in activePlayers)
            {
                if (targetId == currentPlayer)
                    continue;

                string key = targetId + "_" + rank;

                if (!askedRankTargetThisTurn.Contains(key))
                    return true;
            }
        }

        return false;
    }

    public static bool HasValidMovesServer(
    List<NetworkPlayer> netPlayers,
    NetworkPlayer asker,
    Dictionary<ulong, HashSet<string>> serverAskedThisTurn,
    HashSet<int> completedRanks)
    {
        HashSet<int> handRanks = new HashSet<int>();

        foreach (int card in asker.hand)
        {
            int rank = card / 10;

            if (!completedRanks.Contains(rank))
                handRanks.Add(rank);
        }

        foreach (int rank in handRanks)
        {
            foreach (var target in netPlayers)
            {
                if (target.OwnerClientId == asker.OwnerClientId)
                    continue;

                string key = target.OwnerClientId + "_" + rank;

                if (!serverAskedThisTurn.ContainsKey(asker.OwnerClientId) ||
                    !serverAskedThisTurn[asker.OwnerClientId].Contains(key))
                {
                    return true;
                }
            }
        }

        return false;
    }
}