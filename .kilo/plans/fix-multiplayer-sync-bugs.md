# Fix Multiplayer Go Fish Synchronization Bugs

## Problem Summary

In 2-player "Play with Friends" mode, there are critical state synchronization bugs between host and client:

1. **Double card display on host**: When client's hand becomes empty and deck has 2 remaining cards, the refill draws 2 cards to client. On the client side this displays correctly (2 cards). On the host side, cards appear before animation AND after animation, totaling 4 cards shown when the game over panel appears.

2. **Premature game over panel**: When host's hand becomes empty with 1 card left in deck, the game over panel appears before the refill/draw animation completes. On the client side, the host correctly shows 1 card in hand.

## Root Causes

### RC-1: `DelayedStateSync` corrupts animation state
- `NetworkGameManager.DelayedStateSync()` fires on `NetworkGameManager` (separate MonoBehaviour), so it is NOT stopped by `GameManager.StopAllCoroutines()`.
- It sends `SyncPublicStateClientRpc` → `ApplyPublicState()` which silently replaces OTHER players' cards with dummy cards (via `PlayerHand.Clear()` + `new Card(0, CardSuit.Spade)`).
- This happens while `PlayTurnResultCoroutine` is still running animations, causing dummy cards to exist alongside real cards added by the animation.
- **The `turnActionRunning` guard is insufficient** because the RPCs are queued and may execute between coroutine yields.

**Example flow causing 4-card bug:**
1. Server: Book formed on client → client hand empty → `RefillHandIfEmpty` draws 2 cards → deck empty → `isGameOver = true`
2. `TurnResultClientRpc` sent → client & host start `PlayTurnResultCoroutine`
3. At ~2.5s, `DelayedStateSync` fires → `ApplyPublicState` adds 2 dummy cards for client on host
4. Animation continues → adds 2 more real cards for client on host
5. `RefreshAllHands()` → shows 4 cards total (2 dummy + 2 real)

### RC-2: Missing `AddCard` in SUCCESS path hand refill
- In `GameManager.PlayTurnResultCoroutine()`, the SUCCESS case (line ~2358-2396) animates refill cards from deck to player but **never calls `players[refillPlayerId].AddCard(refillCard)`**.
- The GO FISH Phase 2 case (line ~2614-2653) correctly calls `AddCard`. This inconsistency causes the local state to have 0 cards when it should have the refill cards, making the `DelayedStateSync` dummy card replacement trigger (since `currentCount != targetCount`).

### RC-3: `turnActionRunning = false` set before game over check
- In the GO FISH Phase 2 path, `turnActionRunning = false` (line 2684) is set BEFORE the `data.isGameOver` check (line 2686).
- This creates a window where state syncs can apply and refresh visuals between the flag reset and `TriggerGameOver()`.

### RC-4: No `gameOver` guard in state sync handlers
- `ApplyPublicState()` and `ApplyPrivateHand()` only check `turnActionRunning` but not `gameOver`.
- After game over, `turnActionRunning` is set to `true` in `TriggerGameOver()`, but there's a brief window during the game over sequence where state syncs could slip through.

## Files to Modify

1. **`Assets/Scripts/NetworkGameManager.cs`** - Server-side turn resolution, state sync, game over detection
2. **`Assets/Scripts/GameManager.cs`** - Client-side animation processing, state application, game over triggering

## Detailed Changes

### Change 1: Fix `DelayedStateSync` — prevent it from corrupting animations

**File: `NetworkGameManager.cs`**

**1a.** Replace the timed `DelayedStateSync` with a smarter approach that checks game state before syncing:

```csharp
IEnumerator DelayedStateSync(float delay)
{
    yield return new WaitForSeconds(delay);

    // Don't sync if game is over or a turn is in progress
    if (gameOverTriggered) yield break;

    BroadcastStateToAll();
}
```

**1b.** Add a `gameOverTriggered` server-side flag to prevent post-game-over syncs:

```csharp
private bool gameOverTriggered = false;
```

Set `gameOverTriggered = true` in `FillGameOverData` when `isGameOver` is set to true.

Reset `gameOverTriggered = false` in `InitializeAndDeal`.

**1c.** In `RequestCardRpc` and `RequestDrawFromDeckRpc`, increase the delay from `5f`/`2.5f` to `8f` to ensure animations complete before sync fires. This is a safety net — the primary fix is 1a.

### Change 2: Fix missing `AddCard` in SUCCESS path hand refill

**File: `GameManager.cs`** — `PlayTurnResultCoroutine`, SUCCESS case (around line 2358-2396)

Add `players[refillPlayerId].AddCard(refillCard)` inside the refill card loop, matching the GO FISH Phase 2 path:

```csharp
foreach (int value in data.handRefillCards)
{
    RemoveTopDeckVisual();
    Card refillCard = ConvertToCard(value);
    yield return StartCoroutine(
        AnimateCardMove(deckPosition, refillUI.transform, refillCard, refillPlayerId)
    );
    players[refillPlayerId].AddCard(refillCard); // ADD THIS LINE
}
RefreshAllHands();
```

### Change 3: Fix `turnActionRunning` ordering before game over check

**File: `GameManager.cs`** — `PlayTurnResultCoroutine`, GO FISH Phase 2 path (around line 2680-2696)

Move the game over check BEFORE setting `turnActionRunning = false`:

```csharp
// Check game over FIRST (before resetting turnActionRunning)
if (data.isGameOver)
{
    ModeSelectionController.selectedPlayers = data.gameOverPlayerCount;
    TriggerGameOver();
    yield break; // turnActionRunning stays true from TriggerGameOver
}

// Only reset if NOT game over
selectedRank = -1;
waitingForTarget = false;
waitingForDeckClick = false;
lockTargetSelection = false;
turnActionRunning = false;
```

Apply the same fix to the SUCCESS case (around line 2440-2458).

### Change 4: Add `gameOver` guard to state application methods

**File: `GameManager.cs`**

**4a.** `ApplyPublicState()` — add `gameOver` check:

```csharp
public void ApplyPublicState(ulong[] ids, int[] scores, int[] cardCounts)
{
    if (players == null || activePlayers == null) return;
    if (gameOver) return; // ADD THIS
    if (turnActionRunning) return; // ADD THIS - block all updates during animation
    // ... rest of method
}
```

**4b.** `ApplyPrivateHand()` — add `gameOver` check:

```csharp
public void ApplyPrivateHand(int[] hand)
{
    if (players == null || playerIdToClientId.Count == 0) return;
    if (gameOver) return; // ADD THIS
    if (turnActionRunning) return; // already exists, keep it
    // ... rest of method
}
```

**4c.** `UpdateDeckVisualCount()` — add `gameOver` check:

```csharp
public void UpdateDeckVisualCount(int remainingCards, bool force = false)
{
    if (gameOver) return; // ADD THIS
    if (!force && turnActionRunning) return;
    // ... rest of method
}
```

### Change 5: Remove duplicate book+refill code via shared method

**File: `GameManager.cs`**

Extract the repeated book animation + hand refill logic from `PlayTurnResultCoroutine` (it appears identically in both SUCCESS and GO FISH Phase 2 cases) into a single helper method:

```csharp
IEnumerator HandleOnlineBookAndRefill(TurnResultData data)
{
    if (!data.bookFormed) yield break;

    int bookLocalId = GetLocalPlayerId(data.bookPlayerClientId);
    if (toastUI != null)
        toastUI.ShowToast("Book forming...");

    yield return new WaitForSeconds(1.5f);

    yield return StartCoroutine(AnimateOnlineBook(bookLocalId, data.bookRank));

    int myLocalId = GetLocalPlayerId(NetworkManager.Singleton.LocalClientId);

    if (bookLocalId == myLocalId)
    {
        List<Card> toRemove = players[bookLocalId].PlayerHand.GetCardsByRank(data.bookRank);
        foreach (Card c in toRemove)
            players[bookLocalId].PlayerHand.RemoveCard(c);
    }

    players[bookLocalId].SetScore(data.bookPlayerScore);
    uiPlayers[GetUIIndex(bookLocalId)].UpdateScore(data.bookPlayerScore);

    if (toastUI != null)
    {
        if (players[bookLocalId].IsHuman)
            toastUI.ShowToastWithAutoHide("You completed a book!", 3f);
        else
            toastUI.ShowToastWithAutoHide(players[bookLocalId].PlayerName + " completed a book!", 3f);
    }

    RefreshAllHands();

    if (data.handRefillCards != null && data.handRefillCards.Length > 0)
    {
        int refillPlayerId = GetLocalPlayerId(data.handRefillClientId);
        UIPlayer refillUI = uiPlayers[GetUIIndex(refillPlayerId)];

        foreach (int value in data.handRefillCards)
        {
            RemoveTopDeckVisual();
            Card refillCard = ConvertToCard(value);
            yield return StartCoroutine(
                AnimateCardMove(deckPosition, refillUI.transform, refillCard, refillPlayerId)
            );
            players[refillPlayerId].AddCard(refillCard);
        }

        RefreshAllHands();

        if (toastUI != null)
            toastUI.ShowToastWithAutoHide("Drew new cards from deck", 2f);
    }

    yield return new WaitForSeconds(2f);
}
```

Then replace both duplicated blocks in SUCCESS and GO FISH Phase 2 with:
```csharp
yield return StartCoroutine(HandleOnlineBookAndRefill(data));
```

This eliminates ~100 lines of duplicated code and ensures both paths behave identically.

## Impact Assessment

| Change | Offline Mode | Multiplayer Lobby | Multiplayer Gameplay |
|--------|-------------|-------------------|---------------------|
| Change 1 | No impact | No impact | Fixes double-card bug |
| Change 2 | No impact | No impact | Fixes missing cards |
| Change 3 | No impact | No impact | Fixes premature game over |
| Change 4 | No impact | No impact | Prevents post-game corruption |
| Change 5 | No impact | No impact | Code quality (no behavior change) |

All changes are gated behind `GameModeManager.isOnlineMode` checks or only affect the online RPC processing paths. Offline gameplay (`SetupGame`, `ResolveAsk`, `AISelectRandomTarget`, etc.) is completely untouched.

## Execution Order

1. Change 2 (missing AddCard) — simplest fix, immediate impact
2. Change 4 (gameOver guards) — safety net for all state handlers
3. Change 3 (turnActionRunning ordering) — fixes race condition
4. Change 5 (extract shared method) — eliminates duplication, ensures consistency
5. Change 1 (DelayedStateSync fix) — removes root cause of state corruption
