# Fix Plan: Online Multiplayer Popup Positioning Bug

## Root Cause

In `GameManager.InitializeMultiplayer()` (line 1888-1893), `activePlayers` is built from `Dictionary<int, ulong>` keys in insertion order. On the client machine, the dictionary gets entries as `{1: hostClientId, 0: clientClientId}` because `PlayerSeatMapper` assigns local player=seat0, remote=seat1, but `netPlayers` are sorted by clientId (host=0 first).

Result: `activePlayers = [1, 0]` on client (host gets `[0, 1]` which works).

`GetUIIndex(playerID)` searches `activePlayers` for a match:
- `GetUIIndex(0)` returns 1 -> `uiPlayers[1]` = host's UI (top) -- but playerID 0 is CLIENT (bottom) **WRONG**
- `GetUIIndex(1)` returns 0 -> `uiPlayers[0]` = client's UI (bottom) -- but playerID 1 is HOST (top) **WRONG**

This swaps ALL UI lookups on the client: popups, card face-up/down, animations.

## Fix 1 -- CRITICAL: Sort activePlayers (GameManager.cs:1888-1893)

### File: Assets/Scripts/GameManager.cs
### Location: InitializeMultiplayer(), lines 1888-1893

BEFORE:
```csharp
List<int> activeList = new List<int>();
foreach (var kvp in playerIdToClientId)
{
    activeList.Add(kvp.Key);
}
activePlayers = activeList.ToArray();
```

AFTER:
```csharp
List<int> activeList = new List<int>();
foreach (var kvp in playerIdToClientId)
{
    activeList.Add(kvp.Key);
}
activeList.Sort();
activePlayers = activeList.ToArray();
```

Offline safety: Offline activePlayers are already sorted ({0,2}, {0,1,3}, {0,1,2,3}).

## Fix 2 -- POLISH: Skip empty popup for remote players (UIPlayer.cs:98-104)

### File: Assets/Scripts/UIPlayer.cs
### Location: HandleTurnChanged(), lines 98-104

BEFORE:
```csharp
if (!player.IsHuman)
{
    ShowCurrentPlayerPopup();
}
```

AFTER:
```csharp
if (!player.IsHuman && !GameModeManager.isOnlineMode)
{
    ShowCurrentPlayerPopup();
}
```

Reason: In online mode, remote players are IsHuman=false locally. ShowCurrentPlayerPopup() shows an EMPTY popup1 on them -- looks broken. Popup should only show with actual content from PlayTurnResultCoroutine.

## Fix 3 -- POLISH: Don't show empty target popup prematurely (GameManager.cs:817)

### File: Assets/Scripts/GameManager.cs
### Location: HumanSelectTarget(), online branch, line 817

BEFORE:
```csharp
if (GameModeManager.isOnlineMode)
{
    turnActionRunning = true;
    uiPlayers[currentUIIndex].ShowAskPopup(displayName, selectedRank);
    ulong targetClientId = playerIdToClientId[targetID];
    currentTargetUI.ShowTargetPopup();
    NetworkGameManager.Instance.RequestCardRpc(selectedRank, targetClientId);
    waitingForTarget = false;
    lockTargetSelection = false;
    return;
}
```

AFTER:
```csharp
if (GameModeManager.isOnlineMode)
{
    turnActionRunning = true;
    uiPlayers[currentUIIndex].ShowAskPopup(displayName, selectedRank);
    ulong targetClientId = playerIdToClientId[targetID];
    NetworkGameManager.Instance.RequestCardRpc(selectedRank, targetClientId);
    waitingForTarget = false;
    lockTargetSelection = false;
    return;
}
```

Reason: ShowTargetPopup() shows popup2 with empty text. The actual reply comes from PlayTurnResultCoroutine. Let the server result handle target popup display.

## Impact

| Area | Affected? |
|------|-----------|
| Offline gameplay | No change |
| Lobby / Friends | No change |
| MVC structure | No change |
| Host gameplay | No change |
| Client popup positioning | FIXED |
| Client card face display | FIXED (was also broken) |
| Client animation targets | FIXED (was also broken) |

Total: 3 lines changed across 2 files. Minimal, targeted, zero risk to existing functionality.
