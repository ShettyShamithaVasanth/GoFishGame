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
    public NetworkVariable<int> avatarIndex = new NetworkVariable<int>();

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[OnNetworkSpawn] Player: {OwnerClientId} | IsSever : {IsServer} | IsClient: {IsClient} | IsOwner: {IsOwner} |  LocalClientId:{NetworkManager.Singleton.LocalClientId}");
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
            SetAvatarIndexServerRpc(ProfileData.PlayerAvatarIndex);
        }
        gameObject.name = $"Player_{OwnerClientId}";
    }

    [ServerRpc]
    void SetAvatarIndexServerRpc(int index)
    {
        avatarIndex.Value = index;
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
            int cardRank = hand[i] / 10;
            if (cardRank == rank)
            {
                matched.Add(hand[i]);
                hand.RemoveAt(i);
            }
        }
        return matched;
    }
    
    public bool HasRank(int rank)
    {
        foreach (int card in hand)
        {
            int cardRank = card / 10;
            if (cardRank == rank)
                return true;
        }
        return false;
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