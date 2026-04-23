using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerManager : NetworkBehaviour
{
    public static NetworkPlayerManager Instance;
    public List<NetworkPlayer> players = new List<NetworkPlayer>();
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("NetworkPlayerManager Initialized");
    }

    public void RegisterPlayer(NetworkPlayer player)
    {
        Debug.Log($"[RegisterPlayer] Server: {IsServer} | Player: {player.OwnerClientId} | AlreadyExists: {players.Contains(player)}");
        if (!players.Contains(player))
        {
            players.Add(player);
            // Sort properly
            players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            Debug.Log("Player Registered: " + player.OwnerClientId);
            Debug.Log("Total Players: " + players.Count);
        }
    }

    public void UnregisterPlayer(NetworkPlayer player)
    {
        if (!IsServer) return;
        if (players.Contains(player))
        {
            players.Remove(player);
            Debug.Log("Player Removed: " + player.OwnerClientId);
            Debug.Log("Total Players: " + players.Count);
        }
    }
}