using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;

public struct TurnResultData : INetworkSerializable
{
    public ulong askerClientId;
    public ulong targetClientId;
    public int rank;
    public bool success;
    public int transferCount;
    public bool goFish;
    public bool waitingForDraw;
    public int[] transferredCards;
    public int drawnCardValue;
    public bool isLucky;
    public bool bookFormed;
    public int bookRank;
    public ulong bookPlayerClientId;
    public int bookPlayerScore;
    public bool continueTurn;
    public ulong nextTurnClientId;
    public int deckRemaining;

    public int[] handRefillCards;
    public ulong handRefillClientId;

    public bool isGameOver;

    public int[] gameOverScores;
    public ulong[] gameOverPlayerIds;
    public int[] gameOverAvatarIndices;
    public bool[] gameOverIsHuman;

    public int gameOverPlayerCount;
    public void InitializeArrays()
    {
        transferredCards ??= new int[0];

        handRefillCards ??= new int[0];

        gameOverScores ??= new int[0];

        gameOverPlayerIds ??= new ulong[0];

        gameOverAvatarIndices ??= new int[0];

        gameOverIsHuman ??= new bool[0];
    }


    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref askerClientId);
        serializer.SerializeValue(ref targetClientId);
        serializer.SerializeValue(ref rank);
        serializer.SerializeValue(ref success);
        serializer.SerializeValue(ref transferCount);
        serializer.SerializeValue(ref goFish);
        serializer.SerializeValue(ref waitingForDraw);
        serializer.SerializeValue(ref transferredCards);
        serializer.SerializeValue(ref drawnCardValue);
        serializer.SerializeValue(ref isLucky);
        serializer.SerializeValue(ref bookFormed);
        serializer.SerializeValue(ref bookRank);
        serializer.SerializeValue(ref bookPlayerClientId);
        serializer.SerializeValue(ref bookPlayerScore);
        serializer.SerializeValue(ref continueTurn);
        serializer.SerializeValue(ref nextTurnClientId);
        serializer.SerializeValue(ref deckRemaining);

        serializer.SerializeValue(ref handRefillCards);
        serializer.SerializeValue(ref handRefillClientId);

        serializer.SerializeValue(ref isGameOver);

        serializer.SerializeValue(ref gameOverScores);
        serializer.SerializeValue(ref gameOverPlayerIds);
        serializer.SerializeValue(ref gameOverAvatarIndices);
        serializer.SerializeValue(ref gameOverIsHuman);

        serializer.SerializeValue(ref gameOverPlayerCount);
    }
}

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance;
    public NetworkVariable<bool> isGameStarted = new NetworkVariable<bool>(false);
    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> requestedRank = new NetworkVariable<int>(-1);
    public NetworkVariable<ulong> targetPlayerId = new NetworkVariable<ulong>();
    public Button askButton;
    private ulong pendingDrawPlayerId = ulong.MaxValue;
    private int pendingDrawAskedRank = -1;
    private ulong pendingDrawTargetId = ulong.MaxValue;
    public NetworkVariable<int> deckRemainingCards = new NetworkVariable<int>(0);

    private Dictionary<ulong, HashSet<string>> serverAskedThisTurn = new Dictionary<ulong, HashSet<string>>();
    private HashSet<int> serverCompletedRanks = new HashSet<int>();

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
        HideAllLobbyPanels();
        ActivateGameScene();

        // Initialize this client's GameManager (works for BOTH host and client)
        // StartCoroutine(WaitForPlayersThenInit());
    }
    void HideAllLobbyPanels()
    {
        if (LobbyManager.Instance != null)
        {
            var lm = LobbyManager.Instance;
            if (lm.enteringGamePanel != null)
                lm.enteringGamePanel.SetActive(false);
            if (lm.lobbyPanel != null)
                lm.lobbyPanel.SetActive(false);
            if (lm.friendsPanel != null)
                lm.friendsPanel.SetActive(false);
            if (lm.matchmakingPanel != null)
                lm.matchmakingPanel.SetActive(false);
            if (lm.modeSelectionPanel != null)
                lm.modeSelectionPanel.SetActive(false);
            if (lm.menuBackground != null)
                lm.menuBackground.SetActive(false);
            if (lm.creatingRoomPanel != null)
                lm.creatingRoomPanel.SetActive(false);
        }
        MenuController menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            if (menu.MenuUI != null)
                menu.MenuUI.SetActive(false);
            if (menu.LoadingPanel != null)
                menu.LoadingPanel.SetActive(false);
            if (menu.ModeSelectionPanel != null)
                menu.ModeSelectionPanel.SetActive(false);
            if (menu.FriendsPanel != null)
                menu.FriendsPanel.SetActive(false);
        }
    }

    void ActivateGameScene()
    {
        GameSceneUI gameSceneUI = FindAnyObjectByType<GameSceneUI>();
        gameSceneUI?.ShowPanel();
        StartCoroutine(DelayedMultiplayerInitialization());
    }
    IEnumerator DelayedMultiplayerInitialization()
    {
        Debug.Log("WAITING FOR FULL CLIENT INITIALIZATION");

        // WAIT FOR SCENE OBJECTS
        GameManager gm = null;

        while (gm == null)
        {
            gm = FindAnyObjectByType<GameManager>();

            yield return null;
        }

        Debug.Log("GameManager FOUND");

        // WAIT FOR NETWORK PLAYERS
        NetworkPlayer[] netPlayers = null;

        while (netPlayers == null || netPlayers.Length < 2)
        {
            netPlayers = FindObjectsByType<NetworkPlayer>();

            yield return null;
        }
        Debug.Log("Network Players READY" + netPlayers.Length);

        // EXTRA SAFETY WAIT
        yield return new WaitForSeconds(2f);

        ActivatePlayerPosition(netPlayers.Length);
        GameModeManager.isOnlineMode = true;

        Debug.Log("CLIENT READY - Initializing Multiplayer");

        gm.InitializeMultiplayer();

        yield return new WaitForSeconds(1f);

        if (!IsServer)
        {
            RequestFullStateServerRpc();
        }
        else
        {
            ApplyServerStateLocally();
        }

        gm.ApplyNetworkTurn(currentTurnPlayerId.Value);

        Debug.Log("MULTIPLAYER INIT COMPLETE");
    }


    void ActivatePlayerPosition(int playerCount)
    {
        MenuController menu = FindAnyObjectByType<MenuController>();
        if (menu == null)
            return;
        menu.TopPlayer?.SetActive(false);
        menu.BottomPlayer?.SetActive(false);
        menu.LeftPlayer?.SetActive(false);
        menu.RightPlayer?.SetActive(false);
        menu.DeckPosition?.SetActive(false);
        menu.BottomPlayer?.SetActive(true);
        if (playerCount == 2)
        {
            menu.TopPlayer?.SetActive(true);
        }
        else if (playerCount == 3)
        {
            menu.LeftPlayer?.SetActive(true);
            menu.RightPlayer?.SetActive(true);
        }
        else
        {
            menu.TopPlayer?.SetActive(true);
            menu.LeftPlayer?.SetActive(true);
            menu.RightPlayer?.SetActive(true);
        }
        menu.DeckPosition?.SetActive(true);
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
        yield return new WaitUntil(() => NetworkPlayerManager.Instance.players.Count >= 2);

        // Additional safety: wait for player names to sync
        yield return new WaitForSeconds(2f);
        ActivatePlayerPosition(NetworkPlayerManager.Instance.players.Count);

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager NOT FOUND");
            yield break;
        }
        Debug.Log("CLIENT READY - Initializing Multiplayer");
        GameModeManager.isOnlineMode = true;
        gm.InitializeMultiplayer();

        // NOW request fresh state from server
        if (!IsServer)
        {
            RequestFullStateServerRpc();
            yield return new WaitForSeconds(1f);
        }
        else
        {
            // Host: apply state directly (it's already server-side)
            ApplyServerStateLocally();
        }
        gm.ApplyNetworkTurn(currentTurnPlayerId.Value);
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

    [ServerRpc(RequireOwnership = false)]
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
        serverAskedThisTurn.Clear();
        serverCompletedRanks.Clear();
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

        deckRemainingCards.Value = NetworkDeckManager.Instance.deck.Count;
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
        if (gm != null && gm.IsInitialized())
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
        Debug.Log(
    $"[SERVER-NEXT-TURN] FROM Client:{currentTurnPlayerId.Value} " +
    $"TO Client:{players[nextIndex].OwnerClientId}"
);
        serverAskedThisTurn.Clear();
        currentTurnPlayerId.Value = players[nextIndex].OwnerClientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestCardRpc(int rank, ulong targetId, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!serverAskedThisTurn.ContainsKey(senderId))
        {
            serverAskedThisTurn[senderId] =
                new HashSet<string>();
        }

        serverAskedThisTurn[senderId]
            .Add(targetId + "_" + rank);
        Debug.Log(
    $"[SERVER-ASK] FROM Client:{senderId} " +
    $"TO Client:{targetId} rank:{rank}"
);

        if (senderId != currentTurnPlayerId.Value)
            return;

        var players = NetworkPlayerManager.Instance.players;

        NetworkPlayer sender = players.Find(p => p.OwnerClientId == senderId);
        NetworkPlayer target = players.Find(p => p.OwnerClientId == targetId);

        if (sender == null || target == null)
            return;

        TurnResultData result = new TurnResultData
        {
            askerClientId = senderId,
            targetClientId = targetId,
            rank = rank
        };

        result.InitializeArrays();
        if (target.HasRank(rank))
        {
            var cards = target.RemoveCardsByRank(rank);
            result.transferredCards = cards.ToArray();
            foreach (var card in cards)
            {
                sender.AddCard(card);
            }

            result.success = true;
            result.transferCount = cards.Count;
            Debug.Log(
    $"[SERVER-SUCCESS] {cards.Count} cards " +
    $"FROM Client:{targetId} " +
    $"TO Client:{senderId}"
);
            result.goFish = false;
            result.drawnCardValue = -1;
            result.isLucky = false;

            int bookedRank = CheckForBook(sender);
            result.bookFormed = bookedRank != -1;
            if (result.bookFormed)
            {
                result.bookRank = bookedRank;
                result.bookPlayerClientId = sender.OwnerClientId;
                result.bookPlayerScore = sender.score.Value;
            }

            int[] refillCards =
    RefillHandIfEmpty(sender);

            result.handRefillCards = refillCards;

            result.handRefillClientId =
                refillCards.Length > 0
                ? sender.OwnerClientId
                : ulong.MaxValue;

            FillGameOverData(ref result);

            result.deckRemaining =
                NetworkDeckManager.Instance.deck.Count;
            result.continueTurn = GameRules.HasValidMovesServer(
    players,
    sender,
    serverAskedThisTurn,
    serverCompletedRanks
);

            if (!result.continueTurn)
            {
                NextTurn();

                result.nextTurnClientId =
                    currentTurnPlayerId.Value;
            }
        }
        else
        {
            result.success = false;
            result.transferCount = 0;
            result.goFish = true;

            result.waitingForDraw = true;

            result.drawnCardValue = -1;

            result.transferredCards = new int[0];

            pendingDrawPlayerId = senderId;
            pendingDrawAskedRank = rank;
            pendingDrawTargetId = targetId;

            Debug.Log(
    $"[SERVER-GOFISH] FROM Client:{senderId} " +
    $"TO DECK | pendingDraw:{senderId}"
);
            result.InitializeArrays();

            TurnResultClientRpc(result);

            StartCoroutine(DelayedStateSync(5f));

            return;
        }

        result.InitializeArrays();

        TurnResultClientRpc(result);

        StartCoroutine(DelayedStateSync(5f));
    }

    [Rpc(SendTo.Server)]
    public void RequestDrawFromDeckRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        Debug.Log(
    $"[SERVER-DRAW] FROM DECK " +
    $"TO Client:{senderId}"
);
        if (senderId != pendingDrawPlayerId)
            return;

        var players = NetworkPlayerManager.Instance.players;

        NetworkPlayer sender =
            players.Find(p => p.OwnerClientId == senderId);

        if (sender == null)
            return;

        TurnResultData result = new TurnResultData();

        result.InitializeArrays();
        result.transferredCards = new int[0];
        result.askerClientId = senderId;
        result.targetClientId = pendingDrawTargetId;

        result.goFish = true;
        result.waitingForDraw = false;

        int drawnCard = NetworkDeckManager.Instance.DrawCard();

        deckRemainingCards.Value =
            NetworkDeckManager.Instance.deck.Count;

        if (drawnCard != -1)
        {
            sender.AddCard(drawnCard);

            result.drawnCardValue = drawnCard;

            int drawnRank = drawnCard / 10;

            result.isLucky =
                (drawnRank == pendingDrawAskedRank);

            int bookedRank = CheckForBook(sender);
            result.bookFormed = bookedRank != -1;
            if (result.bookFormed)
            {
                result.bookRank = bookedRank;
                result.bookPlayerClientId =
                    sender.OwnerClientId;

                result.bookPlayerScore =
                    sender.score.Value;
            }

            int[] refillCards =
    RefillHandIfEmpty(sender);

            result.handRefillCards = refillCards;

            result.handRefillClientId =
                refillCards.Length > 0
                ? sender.OwnerClientId
                : ulong.MaxValue;
            if (result.isLucky)
            {
                result.continueTurn =
                    GameRules.HasValidMovesServer(
                        players,
                        sender,
                        serverAskedThisTurn,
                        serverCompletedRanks
                    );

                if (!result.continueTurn)
                {
                    NextTurn();

                    result.nextTurnClientId =
                        currentTurnPlayerId.Value;
                }
            }
            else
            {
                result.continueTurn = false;

                NextTurn();

                result.nextTurnClientId =
                    currentTurnPlayerId.Value;
            }
        }
        else
        {
            result.drawnCardValue = -1;
            result.continueTurn = false;

            NextTurn();

            result.nextTurnClientId =
                currentTurnPlayerId.Value;
        }

        result.deckRemaining =
            NetworkDeckManager.Instance.deck.Count;
        FillGameOverData(ref result);
        pendingDrawPlayerId = ulong.MaxValue;
        Debug.Log(
    $"[SERVER-DRAW-RESULT] FROM DECK " +
    $"TO Client:{senderId} " +
    $"drew:{drawnCard} " +
    $"Lucky:{result.isLucky} " +
    $"DeckRemaining:{NetworkDeckManager.Instance.deck.Count}"
);
        pendingDrawAskedRank = -1;
        pendingDrawTargetId = ulong.MaxValue;

        result.InitializeArrays();

        TurnResultClientRpc(result);

        StartCoroutine(DelayedStateSync(2.5f));
    }

    [ClientRpc]
    void TurnResultClientRpc(TurnResultData data, ClientRpcParams rpcParams = default)
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            gm.PlayTurnResult(data);
        }
    }
    IEnumerator DelayedStateSync(float delay)
    {
        yield return new WaitForSeconds(delay);

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

    int CheckForBook(NetworkPlayer player)
    {
        int bookedRank = -1;

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
                bookedRank = kvp.Key;

                player.hand.RemoveAll(c => (c / 10) == bookedRank);

                player.completedBooks.Add(bookedRank);
                serverCompletedRanks.Add(bookedRank);
                player.score.Value++;
                Debug.Log(
    $"[SERVER-BOOK] Player Client:{player.OwnerClientId} " +
    $"completed book rank:{bookedRank} " +
    $"score:{player.score.Value}"
);

                // BookCreatedClientRpc(
                //     player.OwnerClientId,
                //     bookedRank,
                //     player.score.Value
                // );
            }
        }

        return bookedRank;
    }

    int[] RefillHandIfEmpty(NetworkPlayer player)
    {
        if (player.hand.Count > 0)
            return new int[0];

        Debug.Log(
            $"[SERVER-REFILL] Player:{player.OwnerClientId} hand empty"
        );

        List<int> drawnCards = new List<int>();

        int drawCount = Mathf.Min(
            5,
            NetworkDeckManager.Instance.deck.Count
        );

        for (int i = 0; i < drawCount; i++)
        {
            int card = NetworkDeckManager.Instance.DrawCard();

            if (card == -1)
                break;

            player.AddCard(card);

            drawnCards.Add(card);
        }

        deckRemainingCards.Value =
            NetworkDeckManager.Instance.deck.Count;

        Debug.Log(
            $"[SERVER-REFILL] Drew {drawnCards.Count} cards"
        );

        return drawnCards.ToArray();
    }

    void FillGameOverData(ref TurnResultData result)
    {
        if (NetworkDeckManager.Instance.deck.Count > 0)
            return;

        var players = NetworkPlayerManager.Instance.players;

        result.isGameOver = true;

        result.gameOverScores =
            new int[players.Count];

        result.gameOverPlayerIds =
            new ulong[players.Count];

        result.gameOverAvatarIndices =
            new int[players.Count];

        result.gameOverIsHuman =
            new bool[players.Count];

        result.gameOverPlayerCount =
            players.Count;

        for (int i = 0; i < players.Count; i++)
        {
            result.gameOverScores[i] =
                players[i].score.Value;

            result.gameOverPlayerIds[i] =
                players[i].OwnerClientId;

            result.gameOverAvatarIndices[i] =
                players[i].avatarIndex.Value;

            result.gameOverIsHuman[i] =
                players[i].OwnerClientId ==
                NetworkManager.Singleton.LocalClientId;
        }

        Debug.Log("[SERVER-GAMEOVER] Game Over Broadcasted");
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