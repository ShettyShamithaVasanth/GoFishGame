# Fix: Multiplayer Bugs — NullRef, Turn Errors, Missing UI

## Bug Analysis

### Bug 1: Host NullReferenceException in TurnResultData serialization
**File:** `NetworkGameManager.cs` line 439  
**Cause:** In `RequestDrawFromDeckRpc`, `result.transferredCards` is never initialized. Default `int[]` is null. Netcode's `NetworkSerialize` tries to serialize a null array → crash.  
**Fix:** Add `result.transferredCards = new int[0];` after creating the struct.

### Bug 2: Client "Turn player not found" error  
**File:** `NetworkGameManager.cs` line 319-325  
**Cause:** `OnTurnchanged` fires when `currentTurnPlayerId` changes (during `SetFirstTurn()`). This happens BEFORE `InitializeMultiplayer` runs. `playerIdToClientId` is empty → `GetLocalPlayerId` returns -1.  
**Fix:** Guard `ApplyNetworkTurn` call — skip if `playerIdToClientId` is empty (initialization not done yet).

### Bug 3: Host toast/animation missing at game start
**File:** `NetworkGameManager.cs` line 216, `GameManager.cs` line 1677  
**Cause:** `WaitForPlayersThenInit` calls `CheckIfMyTurn()` instead of `gm.ApplyNetworkTurn()`. And `ApplyNetworkTurn` doesn't call `players[localId].StartTurn()` (which triggers profile rotation animation via UIPlayer.HandleTurnChanged).  
**Fix:** Replace `CheckIfMyTurn` with `gm.ApplyNetworkTurn`, and add `StartTurn()` in `ApplyNetworkTurn`.

### Bug 4: Client shows only deck — players/hands invisible
**File:** `GameManager.cs` line 1677-1703  
**Cause:** `ApplyNetworkTurn` doesn't call `StartTurn()`. Without it, UIPlayer never gets the turn event, no profile animation, and the visual refresh cycle is incomplete. Also the initial `RefreshAllHands()` in `InitializeMultiplayer` runs with empty hands before state sync arrives.

### Bug 5: HandleUIRankSelected online check incomplete
**File:** `GameManager.cs` line 279-296  
**Cause:** The online mode check validates network turn BUT still falls through to `uiPlayer.GetPlayerID() != currentPlayer` which fails if `currentPlayer` hasn't been set by `ApplyNetworkTurn` yet.  
**Fix:** Restructure to use local player ID for online mode.

---

## CHANGE 1: NetworkGameManager.cs — Fix NullRef in RequestDrawFromDeckRpc

### Location: Line 439, after `TurnResultData result = new TurnResultData();`

Add this line after line 439:
```csharp
        result.transferredCards = new int[0];
```

### Full context (lines 439-446), OLD:
```csharp
        TurnResultData result = new TurnResultData();

        result.askerClientId = senderId;
        result.targetClientId = pendingDrawTargetId;

        result.goFish = true;
        result.waitingForDraw = false;
```

### NEW:
```csharp
        TurnResultData result = new TurnResultData();
        result.transferredCards = new int[0];

        result.askerClientId = senderId;
        result.targetClientId = pendingDrawTargetId;

        result.goFish = true;
        result.waitingForDraw = false;
```

---

## CHANGE 2: NetworkGameManager.cs — Guard OnTurnchanged

### Location: Lines 319-325

### OLD:
```csharp
    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        CheckIfMyTurn(newPlayer);
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null)
            gm.ApplyNetworkTurn(newPlayer);
    }
```

### NEW:
```csharp
    void OnTurnchanged(ulong oldPlayer, ulong newPlayer)
    {
        CheckIfMyTurn(newPlayer);
        GameManager gm = FindAnyObjectByType<GameManager>();
        if (gm != null && gm.IsInitialized())
            gm.ApplyNetworkTurn(newPlayer);
    }
```

---

## CHANGE 3: NetworkGameManager.cs — Apply turn after init in WaitForPlayersThenInit

### Location: Line 216

### OLD:
```csharp
        CheckIfMyTurn(currentTurnPlayerId.Value);
```

### NEW:
```csharp
        gm.ApplyNetworkTurn(currentTurnPlayerId.Value);
```

---

## CHANGE 4: GameManager.cs — Fix ApplyNetworkTurn (add StartTurn + animation)

### Location: Lines 1677-1703

### OLD:
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
            waitingForTarget=false;
            waitingForDeckClick=false;
            turnActionRunning=false;
            lockTargetSelection=false;
            selectedRank=-1;
            if (toastUI != null)
            {
                toastUI.ShowToast("Your turn!Select any rank card");
            }
        }
    }
```

### NEW:
```csharp
    public void ApplyNetworkTurn(ulong clientId)
    {
        int localId = GetLocalPlayerId(clientId);
        if (localId == -1) return;

        currentPlayer = localId;
        players[localId].StartTurn();

        if (toastUI != null)
            toastUI.HideToast();

        Debug.Log("Turn applied to: " + players[localId].PlayerName);

        if (players[localId].IsHuman)
        {
            selectedRank = -1;
            waitingForTarget = false;
            waitingForDeckClick = false;
            turnActionRunning = false;
            lockTargetSelection = false;
            askedRankTargetThisTurn.Clear();

            if (toastUI != null)
                toastUI.ShowToast("Your turn! Select a rank card");
        }
    }

    public bool IsInitialized()
    {
        return players != null && playerIdToClientId != null && playerIdToClientId.Count > 0;
    }
```

---

## CHANGE 5: GameManager.cs — Fix HandleUIRankSelected for online mode

### Location: Lines 279-296

### OLD:
```csharp
    private void HandleUIRankSelected(UIPlayer uiPlayer, int rank)
    {
        if (players == null || activePlayers == null)
            return;

        if (!players[currentPlayer].IsHuman)
            return;
        if (GameModeManager.isOnlineMode)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            if (NetworkGameManager.Instance.currentTurnPlayerId.Value != myId)
                return;
        }
        if (uiPlayer.GetPlayerID() != currentPlayer)
            return;

        SetSelectedRank(rank);
    }
```

### NEW:
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

## Summary

| # | File | Line | Bug | Fix |
|---|------|------|-----|-----|
| 1 | NetworkGameManager.cs | 439 | NullRef: transferredCards null | Initialize `new int[0]` |
| 2 | NetworkGameManager.cs | 319 | Turn error before init | Guard with `IsInitialized()` |
| 3 | NetworkGameManager.cs | 216 | No toast/animation on host | Replace `CheckIfMyTurn` → `ApplyNetworkTurn` |
| 4 | GameManager.cs | 1677 | No StartTurn() → no animation | Add `StartTurn()` + `HideToast` + proper flags |
| 5 | GameManager.cs | 279 | Rank select broken online | Restructure online/offline checks |

No offline code touched. MVC preserved.
