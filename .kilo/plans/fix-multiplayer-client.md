# Fix: Multiplayer Client Stuck in Lobby

## Root Causes Identified

1. **UI panels not hidden** when game starts on client (FriendsPanel, lobby panels remain visible, blocking game view)
2. **OnTurnchanged doesn't propagate** to GameManager (lines 252-254 commented out)
3. **Turn hardcoded to player 0** in `InitializeMultiplayer()` instead of using network state
4. **HandleUIRankSelected** doesn't check network turn state in online mode
5. **State sync delay too long** (5 seconds)
6. **Player position GameObjects** (TopPlayer, BottomPlayer, etc.) and DeckPosition not activated in multiplayer
7. **ApplyNetworkTurn** doesn't reset interaction flags for human turn start

## Files to Change

- `Assets/Scripts/NetworkGameManager.cs`
- `Assets/Scripts/GameManager.cs`

---

## CHANGE 1: NetworkGameManager.cs — OnGameStartedChanged (line 97-109)

### OLD CODE (lines 97-109):
```csharp
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
```

### NEW CODE:
```csharp
    void OnGameStartedChanged(bool oldValue, bool newValue)
    {
        if (!newValue) return;

        Debug.Log("GAME STARTED on this client");

        HideAllLobbyPanels();

        ActivateGameScene();

        StartCoroutine(WaitForPlayersThenInit());
    }

    void HideAllLobbyPanels()
    {
        if (LobbyManager.Instance != null)
        {
            var lm = LobbyManager.Instance;
            if (lm.enteringGamePanel != null) lm.enteringGamePanel.SetActive(false);
            if (lm.lobbyPanel != null) lm.lobbyPanel.SetActive(false);
            if (lm.friendsPanel != null) lm.friendsPanel.SetActive(false);
            if (lm.matchmakingPanel != null) lm.matchmakingPanel.SetActive(false);
            if (lm.modeSelectionPanel != null) lm.modeSelectionPanel.SetActive(false);
            if (lm.menuBackground != null) lm.menuBackground.SetActive(false);
            if (lm.creatingRoomPanel != null) lm.creatingRoomPanel.SetActive(false);
        }

        MenuController menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            if (menu.MenuUI != null) menu.MenuUI.SetActive(false);
            if (menu.LoadingPanel != null) menu.LoadingPanel.SetActive(false);
            if (menu.ModeSelectionPanel != null) menu.ModeSelectionPanel.SetActive(false);
            if (menu.FriendsPanel != null) menu.FriendsPanel.SetActive(false);
        }
    }

    void ActivateGameScene()
    {
        GameSceneUI gameSceneUI = FindAnyObjectByType<GameSceneUI>();
        if (gameSceneUI != null)
            gameSceneUI.ShowPanel();
    }

    void ActivatePlayerPositions(int playerCount)
    {
        MenuController menu = FindAnyObjectByType<MenuController>();
        if (menu == null) return;

        if (menu.TopPlayer != null) menu.TopPlayer.SetActive(false);
        if (menu.BottomPlayer != null) menu.BottomPlayer.SetActive(false);
        if (menu.LeftPlayer != null) menu.LeftPlayer.SetActive(false);
        if (menu.RightPlayer != null) menu.RightPlayer.SetActive(false);
        if (menu.DeckPosition != null) menu.DeckPosition.SetActive(false);

        if (menu.BottomPlayer != null) menu.BottomPlayer.SetActive(true);

        if (playerCount == 2)
        {
            if (menu.TopPlayer != null) menu.TopPlayer.SetActive(true);
        }
        else if (playerCount == 3)
        {
            if (menu.LeftPlayer != null) menu.LeftPlayer.SetActive(true);
            if (menu.RightPlayer != null) menu.RightPlayer.SetActive(true);
        }
        else
        {
            if (menu.TopPlayer != null) menu.TopPlayer.SetActive(true);
            if (menu.LeftPlayer != null) menu.LeftPlayer.SetActive(true);
            if (menu.RightPlayer != null) menu.RightPlayer.SetActive(true);
        }

        if (menu.DeckPosition != null) menu.DeckPosition.SetActive(true);
    }
```

---

## CHANGE 2: NetworkGameManager.cs — OnTurnchanged (line 249-255)

### OLD CODE (lines 249-255):
```csharp
    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        CheckIfMyTurn(newPlayer);
        // GameManager gm = FindAnyObjectByType<GameManager>();
        // if (gm != null)
        //     gm.ApplyNetworkTurn(newPlayer);
    }
```

### NEW CODE:
```csharp
    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        CheckIfMyTurn(newPlayer);
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.ApplyNetworkTurn(newPlayer);
    }
```

---

## CHANGE 3: NetworkGameManager.cs — WaitForPlayersThenInit (lines 111-147)

### OLD CODE (lines 111-147):
```csharp
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
        yield return new WaitForSeconds(1f);

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
            yield return new WaitForSeconds(1f);
        }
        else
        {
            // Host: apply state directly (it's already server-side)
            ApplyServerStateLocally();
        }

        CheckIfMyTurn(currentTurnPlayerId.Value);
    }
```

### NEW CODE:
```csharp
    System.Collections.IEnumerator WaitForPlayersThenInit()
    {
        while (NetworkPlayerManager.Instance == null ||
               NetworkPlayerManager.Instance.players == null ||
               NetworkPlayerManager.Instance.players.Count < 2)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        int playerCount = NetworkPlayerManager.Instance.players.Count;

        ActivatePlayerPositions(playerCount);

        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("GameManager NOT FOUND");
            yield break;
        }

        GameModeManager.isOnlineMode = true;
        gm.InitializeMultiplayer();

        if (!IsServer)
        {
            RequestFullStateServerRpc();
            yield return new WaitForSeconds(1f);
        }
        else
        {
            ApplyServerStateLocally();
        }

        gm.ApplyNetworkTurn(currentTurnPlayerId.Value);
    }
```

---

## CHANGE 4: NetworkGameManager.cs — Reduce DelayedStateSync delay

Change all 3 occurrences of `DelayedStateSync(5f)` to `DelayedStateSync(1.5f)`:

### Line 345:
OLD: `StartCoroutine(DelayedStateSync(5f));`
NEW: `StartCoroutine(DelayedStateSync(1.5f));`

### Line 350:
OLD: `StartCoroutine(DelayedStateSync(5f));`
NEW: `StartCoroutine(DelayedStateSync(1.5f));`

### Line 441:
OLD: `StartCoroutine(DelayedStateSync(5f));`
NEW: `StartCoroutine(DelayedStateSync(1.5f));`

---

## CHANGE 5: GameManager.cs — InitializeMultiplayer (lines 1800-1806)

### OLD CODE (lines 1800-1806):
```csharp
        currentPlayer = 0;
        players[0].StartTurn();
        if (players[0].IsHuman)
        {
            toastUI.ShowToast("Your turn! Select a rank card");
        }
        // StartCurrentTurn();
```

### NEW CODE:
```csharp
        // Turn is set by NetworkGameManager.ApplyNetworkTurn() via OnTurnchanged
        // Do not hardcode turn here
```

---

## CHANGE 6: GameManager.cs — HandleUIRankSelected (lines 279-291)

### OLD CODE (lines 279-291):
```csharp
    private void HandleUIRankSelected(UIPlayer uiPlayer, int rank)
    {
        if (players == null || activePlayers == null)
            return;

        if (!players[currentPlayer].IsHuman)
            return;

        if (uiPlayer.GetPlayerID() != currentPlayer)
            return;

        SetSelectedRank(rank);
    }
```

### NEW CODE:
```csharp
    private void HandleUIRankSelected(UIPlayer uiPlayer, int rank)
    {
        if (players == null || activePlayers == null)
            return;

        if (GameModeManager.isOnlineMode)
        {
            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            if (NetworkGameManager.Instance == null) return;
            if (NetworkGameManager.Instance.currentTurnPlayerId.Value != myClientId)
                return;
            int myLocalId = GetLocalPlayerId(myClientId);
            if (myLocalId == -1) return;
            if (uiPlayer.GetPlayerID() != myLocalId)
                return;
        }
        else
        {
            if (!players[currentPlayer].IsHuman)
                return;
            if (uiPlayer.GetPlayerID() != currentPlayer)
                return;
        }

        SetSelectedRank(rank);
    }
```

---

## CHANGE 7: GameManager.cs — ApplyNetworkTurn (lines 1672-1694)

### OLD CODE (lines 1672-1694):
```csharp
    public void ApplyNetworkTurn(ulong clientId)
    {
        //convert clientId → local player index
        int localId = GetLocalPlayerId(clientId);
        //safety check
        if (localId == -1)
        {
            Debug.LogError("Turn player not found ❌");
            return;
        }
        //update current player
        currentPlayer = localId;

        Debug.Log("Turn applied to: " + players[localId].PlayerName);
        //OPTIONAL UI feedback
        if (players[localId].IsHuman)
        {
            if (toastUI != null)
            {
                toastUI.ShowToast("Your turn!");
            }
        }
    }
```

### NEW CODE:
```csharp
    public void ApplyNetworkTurn(ulong clientId)
    {
        int localId = GetLocalPlayerId(clientId);
        if (localId == -1)
        {
            Debug.LogError("Turn player not found");
            return;
        }

        currentPlayer = localId;
        players[localId].StartTurn();

        Debug.Log("Turn applied to: " + players[localId].PlayerName);

        if (players[localId].IsHuman)
        {
            selectedRank = -1;
            waitingForTarget = false;
            waitingForDeckClick = false;
            lockTargetSelection = false;
            turnActionRunning = false;
            askedRankTargetThisTurn.Clear();

            if (toastUI != null)
            {
                toastUI.HideToast();
                toastUI.ShowToast("Your turn! Select a rank card");
            }
        }
    }
```

---

## Summary of Changes

| # | File | Method | Fix |
|---|------|--------|-----|
| 1 | NetworkGameManager.cs | OnGameStartedChanged | Hide ALL lobby/menu panels + show game scene |
| 2 | NetworkGameManager.cs | OnTurnchanged | Uncomment ApplyNetworkTurn propagation |
| 3 | NetworkGameManager.cs | WaitForPlayersThenInit | Activate player positions + use ApplyNetworkTurn |
| 4 | NetworkGameManager.cs | DelayedStateSync | Reduce 5s → 1.5s (3 occurrences) |
| 5 | GameManager.cs | InitializeMultiplayer | Remove hardcoded turn (player 0) |
| 6 | GameManager.cs | HandleUIRankSelected | Add online mode network turn check |
| 7 | GameManager.cs | ApplyNetworkTurn | Reset interaction flags + proper toast |

No offline mode code is changed. MVC structure is preserved.
