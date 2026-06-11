# Plan: Fix "Already Asked This Rank" Toast Message

## Issue

In a 2-player Go Fish game (and multi-player), when Player A's turn continues:
1. Player A asks rank 9 from Player B → SUCCESS, gets cards
2. Player A asks another rank → lucky draw from deck, continues turn
3. Player A tries to ask rank 9 again → game silently blocks it with no feedback

The player sees nothing — no toast, no message — and is confused about why clicking does nothing.

**Root Cause:** `GameManager.HumanSelectTarget()` (line 809) blocks duplicate asks via `AIStrategy.AlreadyAskedThisTurn()` but only does a `return;` with no toast.

**Scope:** Both offline mode and online "play with friends" mode. Only toast messages — no other logic changes.

## Architecture Flow

```
Card Selection:  UICard.HandleClick → UIPlayer.OnCardSelected → HandleUIRankSelected → SetSelectedRank(rank)
Target Click:    UIPlayer.HandleClick → HandleUITargetClicked → HumanSelectTarget(targetID)
                   └── checks askedRankTargetThisTurn (line 809) → silent return ← BUG

Turn Tracking:
  Offline:  askedRankTargetThisTurn (HashSet<string>) — cleared on EndTurn()
  Online:   askedRankTargetThisTurn (HashSet<string>) — cleared on ApplyNetworkTurn()
  Server:   serverAskedThisTurn (Dictionary<ulong, HashSet<string>>) — cleared on NextTurn()
```

Both offline and online modes use the same `HumanSelectTarget()` code path — the `AlreadyAskedThisTurn` check at line 809 runs BEFORE the offline/online branch at line 824. A single toast addition covers both modes.

## Changes (3 edits in 1 file)

### Change 1: Add toast in `HumanSelectTarget()` — covers both offline & online

**File:** `Assets/Scripts/GameManager.cs`
**Location:** `HumanSelectTarget()` method, lines 809-813

```csharp
// CURRENT:
if (AIStrategy.AlreadyAskedThisTurn(askedRankTargetThisTurn, targetID, selectedRank))
{
    Debug.Log("You already asked this player for this rank this turn.");
    return;
}

// CHANGE TO:
if (AIStrategy.AlreadyAskedThisTurn(askedRankTargetThisTurn, targetID, selectedRank))
{
    Debug.Log("You already asked this player for this rank this turn.");
    toastUI.ShowToastWithAutoHide("You have already asked this card in this turn!", 2.5f);
    return;
}
```

This is the core fix. It works for both offline and online because:
- Offline: `askedRankTargetThisTurn` is maintained locally
- Online: `askedRankTargetThisTurn` is maintained locally per client, cleared in `ApplyNetworkTurn()`

### Change 2: Add proactive early check in `SetSelectedRank()` — better UX

**File:** `Assets/Scripts/GameManager.cs`
**Location:** `SetSelectedRank()` method, lines 901-911

When a player selects a rank card, if ALL targets have already been asked for that rank this turn, show toast immediately (at card-selection time, not after clicking a target). This prevents the confusing flow of: select card → "select a player" → click player → nothing happens.

```csharp
// CURRENT:
public void SetSelectedRank(int rank)
{
    selectedRank = rank;
    waitingForTarget = true;
    toastUI.HideToast();
    toastUI.ShowToast("Now select a player");
}

// CHANGE TO:
public void SetSelectedRank(int rank)
{
    if (IsRankFullyAskedThisTurn(rank))
    {
        toastUI.ShowToastWithAutoHide("You have already asked this card in this turn!", 2.5f);
        return;
    }

    selectedRank = rank;
    waitingForTarget = true;
    toastUI.HideToast();
    toastUI.ShowToast("Now select a player");
}
```

### Change 3: Add `IsRankFullyAskedThisTurn()` helper method

**File:** `Assets/Scripts/GameManager.cs`
**Location:** Add as a private method near `CurrentPlayerHasValidMoves()` (around line 534)

```csharp
private bool IsRankFullyAskedThisTurn(int rank)
{
    if (activePlayers == null)
        return false;

    foreach (int targetId in activePlayers)
    {
        if (targetId == currentPlayer)
            continue;

        string key = targetId + "_" + rank;
        if (!askedRankTargetThisTurn.Contains(key))
            return false;
    }
    return true;
}
```

## What This Does NOT Change

- No changes to offline game logic, AI behavior, turn flow, or card mechanics
- No changes to online lobby, matchmaking, room creation, or scene management
- No changes to `NetworkGameManager.cs` (server-side already handles correctly; client-side toast is sufficient)
- No changes to `TurnResultData`, RPCs, or network serialization
- No changes to `UIPlayer.cs`, `UICard.cs`, `ToastUI.cs`, or any other scripts
- MVC structure preserved — changes are purely in the controller layer (GameManager)

## Validation

| Scenario | Before Fix | After Fix |
|----------|-----------|-----------|
| 2P offline: ask rank 9 → success → try rank 9 again | Silent block, player confused | Toast: "You have already asked this card in this turn!" |
| 2P online: same scenario | Silent block, player confused | Same toast (same code path) |
| 3P+: ask rank 9 to Player B → try rank 9 to Player C | Works (different target, not blocked) | Works (unchanged, `IsRankFullyAskedThisTurn` returns false for Player C) |
| 3P+: ask rank 9 to all targets → select rank 9 card | "Now select a player" → confused | Toast at card selection: "You have already asked this card!" |
| Normal first ask of any rank | Works | Works (unchanged) |

## Files Modified

| # | File | Lines | Change |
|---|------|-------|--------|
| 1 | `Assets/Scripts/GameManager.cs` | ~809-813 | Add toast in `HumanSelectTarget()` duplicate check |
| 2 | `Assets/Scripts/GameManager.cs` | ~901-911 | Add `IsRankFullyAskedThisTurn()` check in `SetSelectedRank()` |
| 3 | `Assets/Scripts/GameManager.cs` | ~534 | Add new `IsRankFullyAskedThisTurn()` helper method |
