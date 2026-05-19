using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSeatMapper : MonoBehaviour
{
    public static PlayerSeatMapper Instance;
    private Dictionary<ulong, int> clientToSeat =new Dictionary<ulong, int>();
    private void Awake()
    {
        Instance = this;
    }

    public void BuildSeatMap(List<NetworkPlayer> players)
    {
        clientToSeat.Clear();
        ulong localClientId =NetworkManager.Singleton.LocalClientId;
        List<ulong> orderedClients =new List<ulong>();
        // LOCAL PLAYER ALWAYS FIRST
        orderedClients.Add(localClientId);
        // Add remaining players
        foreach (var p in players)
        {
            if (p.OwnerClientId != localClientId)
            {
                orderedClients.Add(p.OwnerClientId);
            }
        }

        // Assign seats
        for (int i = 0; i < orderedClients.Count; i++)
        {
            clientToSeat[orderedClients[i]] = i;
        }
        Debug.Log("===== SEAT MAP =====");
        foreach (var kvp in clientToSeat)
        {
            Debug.Log(
                "Client: " + kvp.Key +
                " Seat: " + kvp.Value
            );
        }
    }
    public int GetSeatIndex(ulong clientId)
    {
        if (clientToSeat.ContainsKey(clientId))
            return clientToSeat[clientId];
        return -1;
    }
}