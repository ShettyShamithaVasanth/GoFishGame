using System.Collections.Generic;
using UnityEngine;

public static class AIStrategy
{
    public static bool PlayerHasRankInHand(Player player, int rank)
    {
        foreach (Card c in player.PlayerHand.Cards)
        {
            if (c.Rank == rank)
                return true;
        }
        return false;
    }

    public static bool AlreadyAskedThisTurn(HashSet<string> askedSet, int targetID, int rank)
    {
        string key = targetID + "_" + rank;
        return askedSet.Contains(key);
    }

    public static int SelectBestTarget(AIMemory aiMemory, List<int> possibleTargets, 
        int rank, int currentPlayer, HashSet<string> askedRankTargetThisTurn, 
        Player[] players)
    {
           int bestTarget = -1;
        float bestScore = float.MinValue;

        foreach (int target in possibleTargets)
        {
            if (AlreadyAskedThisTurn(askedRankTargetThisTurn,target, rank))
                continue;

            float memoryScore = aiMemory.GetConfidence(target, rank);
            // ⭐ STEP 4 ADD HERE (VERY IMPORTANT)
            if (PlayerHasRankInHand(players[currentPlayer], rank))
            {
                memoryScore += 2f; // AI prefers ranks it owns
            }

            float randomFactor = Random.Range(0f, 1f);

            float totalScore = (memoryScore * 0.8f) + (randomFactor * 0.2f);

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestTarget = target;
            }
        }

        // fallback
        if (bestTarget == -1 && possibleTargets.Count > 0)
        {
            bestTarget = possibleTargets[Random.Range(0, possibleTargets.Count)];
        }

        return bestTarget;
    }

    public static List<int> GetSortedRanks(AIMemory aiMemory, List<Card> aiCards, 
        HashSet<int> completedRanks, int currentPlayer, Player[] players)
    {
             Dictionary<int, float> rankScores = new Dictionary<int, float>();

        foreach (Card c in aiCards)
        {
            int rank = c.Rank;

            if (completedRanks.Contains(rank))
                continue;

            if (!rankScores.ContainsKey(rank))
                rankScores[rank] = 0f;

            // memory score
            for (int i = 0; i < players.Length; i++)
            {
                if (i == currentPlayer) continue;
                rankScores[rank] += aiMemory.GetConfidence(i, rank);
            }

            // count bonus
            int count = aiCards.FindAll(x => x.Rank == rank).Count;
            rankScores[rank] += count * 1.5f;
        }

        // sort ranks by score (highest first)
        List<int> sortedRanks = new List<int>(rankScores.Keys);
        sortedRanks.Sort((a, b) => rankScores[b].CompareTo(rankScores[a]));

        return sortedRanks;
    }
}