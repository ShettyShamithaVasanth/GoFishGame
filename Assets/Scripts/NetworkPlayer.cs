using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;

public class NetworkPlayer : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
    public List<int> hand = new List<int>();
    public NetworkVariable<int> score = new NetworkVariable<int>(0);
    public List<int> completedBooks = new List<int>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[OnNetworkSpawn] Player: {OwnerClientId} | IsSever : {IsServer} | IsClient: {IsClient} | IsOwner: {IsOwner } |  LocalClientId:{NetworkManager.Singleton.LocalClientId}");
        if (IsServer && IsSpawned)
        {

            if (NetworkPlayerManager.Instance != null)
            {
                Debug.Log("Registering Player: " + OwnerClientId);
                NetworkPlayerManager.Instance.RegisterPlayer(this);
            }
            else
            {
                Debug.LogError("NetworkPlayerManager is NULL");
            }
        }
        if (IsOwner)
        {
            SetPlayerNameServerRpc("Player_" + OwnerClientId);
        }
        gameObject.name = $"Player_{OwnerClientId}";
    }
    public void AddCard(int card)
    {
        hand.Add(card);
        Debug.Log($"Player {OwnerClientId} received card: {card}");
    }

    public List<int> RemoveCardsByRank(int rank)
    {
        List<int> matched = new List<int>();
        for (int i = hand.Count - 1; i >= 0; i--)
        {
            if (hand[i] == rank)
            {
                matched.Add(hand[i]);
                hand.RemoveAt(i);
            }
        }
        return matched;
    }
    public bool HasRank(int rank)
    {
        return hand.Contains(rank);
    }

    [ServerRpc]
    void SetPlayerNameServerRpc(string name)
    {
        playerName.Value = name;
    }
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkPlayerManager.Instance.UnregisterPlayer(this);
        }
    }
}