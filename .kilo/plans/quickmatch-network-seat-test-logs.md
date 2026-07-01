# Plan: Temporary Network-Id / Seat-Id Test Logging for Play Online (Quick Match) Matchmaking Panel

## Goal
Give the Play Online (Quick Match) matchmaking panel the **same temporary test visibility** that exists for Play With Friends: show each player's **network id** (lobby player id at this stage) and **seat id** (slot index 0-3) so you can confirm:
- Slot 0 = Host
- Slots 1-3 = Clients (in join order)

This is a **temporary testing aid** only, clearly marked, easy to remove, and must NOT affect offline mode, the Friends flow, MVC structure, or live gameplay.

## Background / Key Facts (from code inspection)
1. **QuickMatch and Friends share the same gameplay code path**:
   `NetworkGameManager.InitializeAndDeal()` -> `DelayedMultiplayerInitialization()` -> `GameManager.InitializeMultiplayer()` -> `PlayerSeatMapper.BuildSeatMap()`.
   That path already logs `===== SEAT MAP =====` (`PlayerSeatMapper.cs:35-42`) and `[NET-INIT] ... clientId:` / "Initialized UI seat" (`GameManager.cs:1855-1868`) for BOTH modes. So there is nothing extra to add in gameplay — those logs already fire in Play Online too.
2. The **only place lacking this test logging** is the QuickMatch **matchmaking panel** itself (`MatchmakingUIController.RenderSlots()`), which runs during search, before the relay is joined. At this stage the "network id" available is the **lobby player id** (`player.Id`); the "seat id" is the **slot index** (0-3).
3. The previous plan (`quickmatch-host-first-slots.md`) already adds a `string Id` field to `QuickMatchService.LobbyPlayerInfo` and makes `GetLobbyPlayers()` host-first. This test logging builds on that `Id`.

## Changes (all temporary / test-only, clearly flagged)

### 1. `Assets/Scripts/QuickMatchService.cs` — `LobbyPlayerInfo`
- Already (from host-first plan): add `public string Id;`.
- In `GetLobbyPlayers()` loop, populate `Id = player.Id`.
- Also add a clear **temporary debug log** of the raw lobby player order + who the host is:
  ```csharp
  #if UNITY_EDITOR  // TEMP TEST: remove after verification
  Debug.Log($"[QM-TEST][Lobby] HostId={currentLobby.HostId} Players={currentLobby.Players.Count}");
  foreach (var player in currentLobby.Players)
      Debug.Log($"[QM-TEST][Lobby] Id={player.Id} IsHost={player.Id==currentLobby.HostId}");
  #endif
  ```
  Wrapped in `#if UNITY_EDITOR` so it never ships in a build (no risk to release players).

### 2. `Assets/Scripts/MatchmakingUIController.cs` — `RenderSlots()`
Add a **temporary, editor-only** log that prints the final ordered slot map (network id + seat id), mirroring the Friends `===== SEAT MAP =====` output:
```csharp
#if UNITY_EDITOR  // TEMP TEST: remove after verification
Debug.Log("[QM-TEST][Slots] ===== QUICK MATCH SLOTS =====");
for (int i = 0; i < players.Count && i < playerSlots.Length; i++)
{
    Debug.Log($"[QM-TEST][Slots] Seat {i} -> NetId={players[i].Id} Name={players[i].PlayerName} IsLocal={players[i].IsLocal}");
}
#endif
```
- Seat id = loop index `i` (the slot). NetId = `players[i].Id`.
- With the host-first sort from the previous plan, this will show `Seat 0 -> <host id>` and clients in `Seat 1..3`.

### 3. (Optional, on-screen) — only if you want it visible in the panel
If a visible label is wanted, extend `PlayerProfileUI` with an optional `TextMeshProUGUI testTagText` and in `SetProfile(string name, Sprite avatar, string testTag = null)` set it. `RenderSlots()` would pass `testTag = $"id:{players[i].Id} seat:{i}"`. Because this changes a shared UI component, it is kept **optional** and defaults to `null` (no visual change unless the field is assigned in the prefab). To keep the change minimal and MVC-clean, the default plan is **console logging only** (steps 1-2); the on-screen tag is listed only as an opt-in.

## Why this is correct & safe
- **No hardcode:** ids come from the lobby (`player.Id`, `currentLobby.HostId`); seat ids are the real slot indices.
- **MVC respected:** logging lives in the service/controller layers where the data already exists; no game-state logic changes.
- **Offline mode untouched:** `QuickMatchService`/`MatchmakingUIController` are online-only.
- **Friends untouched:** `LobbyManager` and the Friends gameplay path are not modified.
- **Ships clean:** all test logs are inside `#if UNITY_EDITOR`, so production builds contain none of it.
- **Easy to remove:** search for `[QM-TEST]` and delete those blocks.

## Expected Test Output (verification)
On host during search (3 players, you are host):
```
[QM-TEST][Lobby] HostId=<your id> Players=3
[QM-TEST][Lobby] Id=<your id> IsHost=True
[QM-TEST][Lobby] Id=<client A> IsHost=False
[QM-TEST][Lobby] Id=<client B> IsHost=False
[QM-TEST][Slots] ===== QUICK MATCH SLOTS =====
[QM-TEST][Slots] Seat 0 -> NetId=<your id>   Name=You     IsLocal=True
[QM-TEST][Slots] Seat 1 -> NetId=<client A>  Name=PlayerA IsLocal=False
[QM-TEST][Slots] Seat 2 -> NetId=<client B>  Name=PlayerB IsLocal=False
```
On a joining client, `Seat 0` will still show the host's id, and the local client appears in a later seat — proving host-first ordering works.

## Files Changed
- `Assets/Scripts/QuickMatchService.cs` (populate `Id`; temporary `[QM-TEST][Lobby]` logs — depends on host-first plan)
- `Assets/Scripts/MatchmakingUIController.cs` (temporary `[QM-TEST][Slots]` logs)

## Out of Scope
- No changes to `LobbyManager.cs`, `NetworkGameManager.cs`, `GameManager.cs`, `PlayerSeatMapper.cs`, or any offline code.
- On-screen tag (section 3) is optional and not included by default.
