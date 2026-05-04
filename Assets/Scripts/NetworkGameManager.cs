using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.VisualScripting;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;
    public NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> requestedRank = new NetworkVariable<int>(-1);
    public NetworkVariable<ulong> targetPlayerId = new NetworkVariable<ulong>();
    public Button askButton;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        isGameStarted.OnValueChanged += OnGameStartedChanged;
        currentTurnPlayerId.OnValueChanged += OnTurnchanged;
        Debug.Log("NetworkGameManager Spawned ✔" + $" | Server: {IsServer} | Client: {IsClient}");
        // Start game on server
        if (IsServer)
        {
            StartCoroutine(StartGameWithEnoughPlayers());
        }
    }

    System.Collections.IEnumerator StartGameWithEnoughPlayers()
    {
        Debug.Log("Waiting for enough players to start game...");
        float waitTime = 0f;
        float maxWaitTime = 60f; // safety (not hardcoding behavior, just timeout)

        while (true)
        {
            var players = NetworkPlayerManager.Instance.players;
            //Enough players
            if (players != null && players.Count >= 2)
            {
                Debug.Log("Enough players found. Starting game...");
                break;
            }

            //Timeout (fallback safety)
            if (waitTime >= maxWaitTime)
            {
                Debug.Log("Timeout reached. Starting game anyway...");
                break;
            }
            Debug.Log("Still waiting for players...");
            yield return new WaitForSeconds(1f); // ⏱ check every 1 second
            waitTime += 1f;
        }

        //START GAME FLOW (CLEAN)
        DealCardsToPlayers();
        SetFirstTurn();
        isGameStarted.Value = true;

        //CALL LOBBY MANAGER START (as your manager said)
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.StartGame();
        }
    }

    public override void OnNetworkDespawn()
    {
        isGameStarted.OnValueChanged -= OnGameStartedChanged;
        currentTurnPlayerId.OnValueChanged -= OnTurnchanged;
    }

    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        Debug.Log("StartGameClient CALLED");
        if (newValue)
        {
            Debug.Log("GAME STARTED ON ALL CLIENTS");
            StartGameClient();
            StartCoroutine(HideEnteringPanelAfterDelay());
            // ADD THIS HERE (correct place)
            CheckIfMyTurn(currentTurnPlayerId.Value);
        }
    }

    System.Collections.IEnumerator HideEnteringPanelAfterDelay()
    {
        yield return new WaitForSeconds(3f); // ⏱ 3 seconds
        if (LobbyManager.Instance != null &&
            LobbyManager.Instance.enteringGamePanel != null)
        {
            LobbyManager.Instance.enteringGamePanel.SetActive(false);
            Debug.Log("Entering panel hidden ✔");
        }
    }

    // public void StartGameServer()
    // {
    //     if (!IsServer) return;
    //     Debug.Log("Server Starting Game");
    //     // DealCardsToPlayers();
    //     // SetFirstTurn();
    //     // isGameStarted.Value = true;
    //     StartCoroutine(StartGameWithDelay());
    // }
    // System.Collections.IEnumerator StartGameWithDelay()
    // {
    //     yield return new WaitForSeconds(1f);

    //     var players = NetworkPlayerManager.Instance.players;

    //     if (players.Count < 2)
    //     {
    //         Debug.Log("Waiting for more players...");
    //         yield break;
    //     }

    //     Debug.Log("Starting Game Automatically");

    //     DealCardsToPlayers();
    //     SetFirstTurn();
    //     isGameStarted.Value = true;
    // }

    void StartGameClient()
    {
        Debug.Log("StartGameClient CALLED");
        StartCoroutine(WaitForPlayersThenInit());
        Debug.Log("Client initializing game...");
        // STEP 1 — Find GameManager in scene
        GameManager gm = FindFirstObjectByType<GameManager>();
        // Safety check
        if (gm == null)
        {
            Debug.LogError("GameManager NOT FOUND ❌");
            return;
        }

        GameModeManager.isOnlineMode = true;
        Debug.Log("Calling InitializeMultiplayer");
        // STEP 2 — Initialize multiplayer setup
        // gm.InitializeMultiplayer();
        Debug.Log("game started client side ✔");
        // // STEP 3 — Camera check (just safety, no changes)
        // Camera cam = Camera.main;
        // if (cam != null)
        // {
        //     Debug.Log("Camera ready ✔");
        // }
    }

    System.Collections.IEnumerator WaitForPlayersThenInit()
    {
        Debug.Log("Waiting for players to be ready...");
        while (true)
        {
            if (NetworkPlayerManager.Instance != null &&
                NetworkPlayerManager.Instance.players != null &&
                NetworkPlayerManager.Instance.players.Count >= 2)
            {
                break;
            }
            yield return null; // wait next frame
        }

        Debug.Log("Players ready. Initializing game");
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager NOT FOUND ❌");
            yield break;
        }
        GameModeManager.isOnlineMode = true;
        gm.InitializeMultiplayer();
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
        Debug.Log("Total Players: " + players.Count);
        Debug.Log("Cards Dealt to All Players");
        SendStateToClients();
    }

    void SetFirstTurn()
    {
        var players = NetworkPlayerManager.Instance.players;
        if (players.Count == 0) return;
        // First palyer =index 0
        currentTurnPlayerId.Value = players[0].OwnerClientId;
        Debug.Log("First Turn Set to Player: " + currentTurnPlayerId.Value);
    }

    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        Debug.Log("Turn Changed:" + newPlayer);
        CheckIfMyTurn(newPlayer);
        GameManager gm=FindFirstObjectByType<GameManager>();
        if(gm!=null)
        {
            gm.ApplyNetworkTurn(newPlayer);
        }
        else
        {
            Debug.Log("GameManager not found");
        }

    }

    void CheckIfMyTurn(ulong turnPlayerId)
    {
        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (myId == turnPlayerId)
        {
            Debug.Log("MY TURN");
            if (askButton != null)
                askButton.interactable = true; // ENABLE
        }
        else
        {
            Debug.Log("WAITING");
            if (askButton != null)
                askButton.interactable = false; // DISABLE
        }
    }

    public void NextTurn()
    {
        if (!IsServer) return;
        var players = NetworkPlayerManager.Instance.players;
        if (players == null || players.Count == 0)
        {
            Debug.LogError("No players available for turn");
            return;
        }

        int currentIndex = players.FindIndex(p => p.OwnerClientId == currentTurnPlayerId.Value);
        if (currentIndex == -1)
        {
            Debug.LogError("Current player not found");
            return;
        }

        int nextIndex = (currentIndex + 1) % players.Count;
        currentTurnPlayerId.Value = players[nextIndex].OwnerClientId;
        Debug.Log("Next Turn → Player " + currentTurnPlayerId.Value);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCardRpc(int rank, ulong targetId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != currentTurnPlayerId.Value)
        {
            Debug.Log("Not your turn...");
            return;
        }

        var players = NetworkPlayerManager.Instance.players;
        NetworkPlayer sender = players.Find(p => p.OwnerClientId == senderId);
        NetworkPlayer target = players.Find(p => p.OwnerClientId == targetId);

        if (sender == null || target == null)
        {
            Debug.LogError("Player not found");
            return;
        }
        Debug.Log($"Player {senderId} asked Player {targetId} for rank {rank}");

        //SUCCESS
        if (target.HasRank(rank))
        {
            var cards = target.RemoveCardsByRank(rank);
            foreach (var card in cards)
                sender.AddCard(card);
            Debug.Log($"Transferred {cards.Count} cards");

            //IMPORTANT ORDER
            CheckForBook(sender);
            // SAME PLAYER CONTINUES (NO turn change)
        }
        else
        {
            Debug.Log("GO FISH");
            int drawnCard = NetworkDeckManager.Instance.DrawCard();
            if (drawnCard != -1)
            {
                sender.AddCard(drawnCard);
                //IMPORTANT ORDER
                CheckForBook(sender);
                //If same rank → play again
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
                // Deck empty → still move turn
                NextTurn();
            }
        }
        //FINAL STEP (ONLY ONCE)
        SendStateToClients();
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
                Debug.Log($"BOOK CREATED! Player {player.OwnerClientId} completed rank {rank}");
                player.hand.RemoveAll(c => (c / 10) == rank);
                // score book
                player.completedBooks.Add(rank);
                // update score
                player.score.Value++;
                Debug.Log($"Player {player.OwnerClientId} scored :{player.score.Value} ");
                // Notify all clients
                BookCreatedClientRpc(player.OwnerClientId, rank, player.score.Value);
            }
        }
    }
    [ClientRpc]
    void BookCreatedClientRpc(ulong playerId, int rank, int score)
    {
        Debug.Log($"[ClientRpc] Player {playerId} completed book of rank {rank} | Score: {score}");
        // Update UI or other client-side elements here
    }

    [ClientRpc]
    void SyncPublicStateClientRpc(ulong[] ids, int[] scores, int[] cardCounts)
    {
        Debug.Log("SyncPublicStateClientRpc RECEIVED");
        GameManager gm = FindFirstObjectByType<GameManager>();

        if (gm == null)
        {
            Debug.LogError("GameManager not found ❌");
            return;
        }
        gm.ApplyPublicState(ids, scores, cardCounts);
    }

    void SendStateToClients()
    {
        var players = NetworkPlayerManager.Instance.players;
        int count = players.Count;

        ulong[] ids = new ulong[count];
        int[] scores = new int[count];
        int[] cardCounts = new int[count];

        for (int i = 0; i < count; i++)
        {
            ids[i] = players[i].OwnerClientId;
            scores[i] = players[i].score.Value;
            cardCounts[i] = players[i].hand.Count;
        }
        // send public info to all
        SyncPublicStateClientRpc(ids, scores, cardCounts);
        // send private hands individually
        foreach (var p in players)
        {
            int[] hand = p.hand.ToArray();
            ClientRpcParams sendTo = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { p.OwnerClientId }
                }
            };
            SyncPrivateHandClientRpc(hand, sendTo);
        }
    }

    [ClientRpc]
    void SyncPrivateHandClientRpc(int[] hand, ClientRpcParams rpcParams = default)
    {
        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager not found ❌");
            return;
        }
        gm.ApplyPrivateHand(hand);
    }
}
