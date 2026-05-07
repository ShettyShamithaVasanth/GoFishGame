using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections.Generic;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;
    public NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> requestedRank = new NetworkVariable<int>(-1);
    public NetworkVariable<ulong> targetPlayerId = new NetworkVariable<ulong>();
    public Button askButton;
    public int deckRemainingCards = 0;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        isGameStarted.OnValueChanged += OnGameStartedChanged;
        currentTurnPlayerId.OnValueChanged += OnTurnchanged;
        Debug.Log("NetworkGameManager Spawned | Server: " + IsServer + " | Client: " + IsClient);
    }

    public override void OnNetworkDespawn()
    {
        isGameStarted.OnValueChanged -= OnGameStartedChanged;
        currentTurnPlayerId.OnValueChanged -= OnTurnchanged;
    }

    // Called by LobbyManager.StartGame() when host presses Start
    public void InitializeAndDeal()
    {
        if (!IsServer) return;

        var netPlayers = NetworkPlayerManager.Instance.players;
        if (netPlayers == null || netPlayers.Count < 2)
        {
            Debug.LogError("Not enough players to deal");
            return;
        }

        DealCardsToPlayers();
        SetFirstTurn();
        isGameStarted.Value = true;
    }

    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;

        Debug.Log("GAME STARTED on this client");

        // Hide entering panel
        if (LobbyManager.Instance != null && LobbyManager.Instance.enteringGamePanel != null)
            LobbyManager.Instance.enteringGamePanel.SetActive(false);

        // Initialize this client's GameManager (works for BOTH host and client)
        StartCoroutine(WaitForPlayersThenInit());
    }

    System.Collections.IEnumerator WaitForPlayersThenInit()
    {
        // Wait for NetworkPlayerManager to have players
        while (NetworkPlayerManager.Instance == null ||
               NetworkPlayerManager.Instance.players == null ||
               NetworkPlayerManager.Instance.players.Count < 2)
        {
            yield return null;
        }

        // Additional safety: wait for player names to sync
        yield return new WaitForSeconds(0.5f);

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager NOT FOUND");
            yield break;
        }

        GameModeManager.isOnlineMode = true;
        gm.InitializeMultiplayer();

        // NOW request fresh state from server
        if (!IsServer)
        {
            RequestFullStateServerRpc();
        }
        else
        {
            // Host: apply state directly (it's already server-side)
            ApplyServerStateLocally();
        }

        CheckIfMyTurn(currentTurnPlayerId.Value);
    }

    void ApplyServerStateLocally()
    {
        var netPlayers = NetworkPlayerManager.Instance.players;
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null) return;

        // Apply public state
        ulong[] ids = new ulong[netPlayers.Count];
        int[] scores = new int[netPlayers.Count];
        int[] cardCounts = new int[netPlayers.Count];
        for (int i = 0; i < netPlayers.Count; i++)
        {
            ids[i] = netPlayers[i].OwnerClientId;
            scores[i] = netPlayers[i].score.Value;
            cardCounts[i] = netPlayers[i].hand.Count;
        }
        gm.ApplyPublicState(ids, scores, cardCounts);

        // Apply private hand (find my own)
        ulong myId = NetworkManager.Singleton.LocalClientId;
        var myPlayer = netPlayers.Find(p => p.OwnerClientId == myId);
        if (myPlayer != null)
        {
            gm.ApplyPrivateHand(myPlayer.hand.ToArray());
        }
    }

    [ServerRpc]
    void RequestFullStateServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        SendStateToSpecificClient(clientId);
    }

    void SendStateToSpecificClient(ulong clientId)
    {
        var netPlayers = NetworkPlayerManager.Instance.players;
        int count = netPlayers.Count;

        ulong[] ids = new ulong[count];
        int[] scores = new int[count];
        int[] cardCounts = new int[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = netPlayers[i].OwnerClientId;
            scores[i] = netPlayers[i].score.Value;
            cardCounts[i] = netPlayers[i].hand.Count;
        }

        ClientRpcParams sendTo = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        SyncPublicStateClientRpc(ids, scores, cardCounts, sendTo);

        // Send private hand
        var targetPlayer = netPlayers.Find(p => p.OwnerClientId == clientId);
        if (targetPlayer != null)
        {
            SyncPrivateHandClientRpc(targetPlayer.hand.ToArray(), sendTo);
        }

        // Send deck count
        SyncDeckCountClientRpc(NetworkDeckManager.Instance.deck.Count, sendTo);
    }

    void DealCardsToPlayers()
    {
        if (!IsServer) return;
        int cardsPerPlayer = 7;
        var players = NetworkPlayerManager.Instance.players;

        for (int i = 0; i < cardsPerPlayer; i++)
        {
            foreach (var player in players)
            {
                int card = NetworkDeckManager.Instance.DrawCard();
                if (card != -1)
                {
                    player.AddCard(card);
                }
            }
        }

        deckRemainingCards = NetworkDeckManager.Instance.deck.Count;
        Debug.Log("Cards Dealt. Deck remaining: " + deckRemainingCards);
    }

    void SetFirstTurn()
    {
        var players = NetworkPlayerManager.Instance.players;
        if (players.Count == 0) return;
        currentTurnPlayerId.Value = players[0].OwnerClientId;
        Debug.Log("First Turn: Player " + currentTurnPlayerId.Value);
    }

    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        CheckIfMyTurn(newPlayer);
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.ApplyNetworkTurn(newPlayer);
    }

    void CheckIfMyTurn(ulong turnPlayerId)
    {
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (askButton != null)
            askButton.interactable = (myId == turnPlayerId);
    }

    public void NextTurn()
    {
        if (!IsServer) return;
        var players = NetworkPlayerManager.Instance.players;
        if (players == null || players.Count == 0) return;

        int currentIndex = players.FindIndex(p => p.OwnerClientId == currentTurnPlayerId.Value);
        if (currentIndex == -1) return;

        int nextIndex = (currentIndex + 1) % players.Count;
        currentTurnPlayerId.Value = players[nextIndex].OwnerClientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCardRpc(int rank, ulong targetId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != currentTurnPlayerId.Value) return;

        var players = NetworkPlayerManager.Instance.players;
        NetworkPlayer sender = players.Find(p => p.OwnerClientId == senderId);
        NetworkPlayer target = players.Find(p => p.OwnerClientId == targetId);

        if (sender == null || target == null) return;

        if (target.HasRank(rank))
        {
            var cards = target.RemoveCardsByRank(rank);
            foreach (var card in cards)
                sender.AddCard(card);
            CheckForBook(sender);
        }
        else
        {
            int drawnCard = NetworkDeckManager.Instance.DrawCard();
            deckRemainingCards = NetworkDeckManager.Instance.deck.Count;
            if (drawnCard != -1)
            {
                sender.AddCard(drawnCard);
                CheckForBook(sender);
                if (drawnCard == rank)
                {
                    Debug.Log("Drawn same rank → continue turn");
                }
                else
                {
                    NextTurn();
                }
            }
            else
            {
                NextTurn();
            }
        }
        BroadcastStateToAll();
    }

    void BroadcastStateToAll()
    {
        var netPlayers = NetworkPlayerManager.Instance.players;
        int count = netPlayers.Count;

        ulong[] ids = new ulong[count];
        int[] scores = new int[count];
        int[] cardCounts = new int[count];
        for (int i = 0; i < count; i++)
        {
            ids[i] = netPlayers[i].OwnerClientId;
            scores[i] = netPlayers[i].score.Value;
            cardCounts[i] = netPlayers[i].hand.Count;
        }

        SyncPublicStateClientRpc(ids, scores, cardCounts);
        SyncDeckCountClientRpc(NetworkDeckManager.Instance.deck.Count);

        foreach (var p in netPlayers)
        {
            ClientRpcParams sendTo = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { p.OwnerClientId }
                }
            };
            SyncPrivateHandClientRpc(p.hand.ToArray(), sendTo);
        }
    }

    void CheckForBook(NetworkPlayer player)
    {
        Dictionary<int, int> count = new Dictionary<int, int>();
        foreach (var card in player.hand)
        {
            int rank = card / 10;
            if (!count.ContainsKey(rank))
                count[rank] = 0;
            count[rank]++;
        }

        foreach (var kvp in count)
        {
            if (kvp.Value == 4)
            {
                int rank = kvp.Key;
                player.hand.RemoveAll(c => (c / 10) == rank);
                player.completedBooks.Add(rank);
                player.score.Value++;
                BookCreatedClientRpc(player.OwnerClientId, rank, player.score.Value);
            }
        }
    }

    [ClientRpc]
    void BookCreatedClientRpc(ulong playerId, int rank, int score)
    {
        Debug.Log("Book! Player " + playerId + " rank " + rank + " score " + score);
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.OnNetworkBookCreated(playerId, rank, score);
    }

    [ClientRpc]
    void SyncPublicStateClientRpc(ulong[] ids, int[] scores, int[] cardCounts, ClientRpcParams rpcParams = default)
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null) return;
        gm.ApplyPublicState(ids, scores, cardCounts);
    }

    [ClientRpc]
    void SyncPrivateHandClientRpc(int[] hand, ClientRpcParams rpcParams = default)
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null) return;
        gm.ApplyPrivateHand(hand);
    }

    [ClientRpc]
    void SyncDeckCountClientRpc(int remaining, ClientRpcParams rpcParams = default)
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.UpdateDeckVisualCount(remaining);
    }
}