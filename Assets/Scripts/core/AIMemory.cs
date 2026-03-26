using System.Collections.Generic;

public class AIMemory
{
    private Dictionary<string, float> memory = new Dictionary<string, float>();

    public void AddConfidence(int playerID, int rank, float value)
    {
        string key = playerID + "_" + rank;

        if (memory.ContainsKey(key))
            memory[key] += value;
        else
            memory[key] = value;
    }

    public float GetConfidence(int playerID, int rank)
    {
        string key = playerID + "_" + rank;

        if (memory.ContainsKey(key))
            return memory[key];

        return 0f;
    }

    public void ClearRank(int rank)
    {
        List<string> removeKeys = new List<string>();

        foreach (var k in memory.Keys)
        {
            if (k.EndsWith("_" + rank))
                removeKeys.Add(k);
        }

        foreach (var k in removeKeys)
            memory.Remove(k);
    }
}