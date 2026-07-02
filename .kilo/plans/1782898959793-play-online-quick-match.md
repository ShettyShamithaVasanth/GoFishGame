# Play Online — Auto Quick Match (Professional Multiplayer Go Fish)

## Goal
Make "Play Online" a real auto-matchmaking mode:
1. Click **Play Online** → load GameScene → show a "entering online mode / matchmaking" panel.
2. Within **20s**, the instant the matched lobby has **>= 2 real players**, the multiplayer game **auto-starts** using the **same gameplay path as Play with Friends** (`NetworkGameManager.InitializeAndDeal()`).
3. If **20s** pass with **< 2 players** → fall back to **offline** exactly as it works now (`OfflineFallback.Enter()`).

## Constraints (must NOT break)
- **Play with Friends** (`LobbyManager.cs`): UNTOUCHED — no edits.
- **Offline** (`GameManager.SetupGame`, `ModeSelectionController`, `OfflineFallback`): UNTOUCHED.
- All gameplay/turn logic (`NetworkGameManager` RPCs, `NetworkPlayer`, `NetworkPlayerManager`, dealing): UNTOUCHED.
- `MatchMakingStarter`, `MenuController.PlayOnline`, `GameModeManager`: UNTOUCHED (already do the right setup).
- **MVC preserved:** matchmaking *Service* = `QuickMatchService` (logic/network); *View* = `MatchmakingUIController` (rendering only). No gameplay logic in the view.

## Confirmed facts from codebase (verified)
- `GameScene.unity` already contains all needed objects: `NetworkManager`, `LobbyManager`, `QuickMatchService`, `MatchmakingUIController`, `MatchmakingStarter`, `NetworkPlayerManager`, `NetworkGameManager`, `GameManager`, `GameSceneUI`, player seats, deck. **No new prefabs / scene changes required.**
- Online game-start flow (used by Friends) = host `NetworkGameManager.InitializeAndDeal()` (NetworkGameManager.cs:124) → `isGameStarted.Value=true` (line 137) → `OnGameStartedChanged` (line 140) fires on every client → `HideAllLobbyPanels()` (already hides `LobbyManager.matchmakingPanel` at line 165) → `ActivateGameScene()` → multiplayer init + dealing. Play Online reuses this exact flow; clients need no manual trigger.
- Friends relay/transport pattern lives in `LobbyManager.CreateRelay` (lines 319-344) / `JoinRelay` (lines 347-385) — copy the ~10 transport-setup lines into `QuickMatchService`, do NOT edit `LobbyManager`.
- `LobbyManager.StartGame` (line 470) shows `enteringGamePanel` (line 489) before `InitializeAndDeal()`. Host auto-start must mirror this.
- Timeout already wired: `QuickMatchService.OnTimeout` → `MatchmakingUIController.HandleTimeout` → "No players found. Switching to offline..." → `OfflineFallback.Enter()`. Keep this.
- `MatchMakingStarter.Start()` already calls `StartSearching()` + `QuickMatchService.FindMatch()`. Leave as-is.
- `MenuController.PlayOnline()` already sets `isOnlineMode=true` + loads GameScene. Leave as-is.
- Profile/avatar data: `ProfileData.PlayerName`, `ProfileData.PlayerAvatarIndex`, `AvatarDatabase` (via `LobbyManager.avatarDatabase`).

## Root cause of current bug
`QuickMatchService` sets `isSearching=true` and counts a timer, but:
- `StartHostFlow()` (line 95) / `StartClientFlow()` (line 101) are **empty stubs** (only `Debug.Log`).
- `CheckMatchReady()` (line 70) is **never called**.
- No lobby is created/joined, no Relay, no `NetworkManager.StartHost/Client`.
- `requiredPlayers = 4` (line 46) — should be 2.
So Play Online never connects; it always times out to offline.

## Design decisions
1. **Matchmaking technique:** Unity Lobby **QuickJoin** + Relay, filtered by lobby `Data mode = "GoFish"` so quick-match only matches this game.
2. **Auto-start rule:** the instant `lobby.Players.Count >= minPlayersToStart`, the **host** calls `NetworkGameManager.Instance.InitializeAndDeal()`. Clients are driven automatically by the `isGameStarted` NetworkVariable.
3. **Start threshold:** replace `requiredPlayers=4` with `[SerializeField] int minPlayersToStart = 2;` (configurable, not hardcoded). `>= minPlayersToStart` triggers auto-start.
4. **No Friends edit:** `QuickMatchService` is self-contained — owns its own Lobby create/quickjoin/relay/host/client. It does NOT call into `LobbyManager`. The transport-setup lines are duplicated here (acceptable, ~10 lines) to guarantee `LobbyManager` is untouched.
5. **Host vs client resolution:** try `QuickJoinLobbyAsync` first (joiner). If no quick-match lobby exists (exception / empty result), become **host**: `CreateLobby` (`isPrivate=false`, `maxPlayers`) with `mode` Data + Relay allocation + `StartHost`.
6. **Player Data:** each player publishes `name` + `avatar` via `UpdatePlayerAsync` (same pattern as LobbyManager lines 206-224), so all clients can render every player's avatar+name.
7. **Lobby detection:** poll `LobbyService.Instance.GetLobbyAsync(id)` (reuse existing 20s timer cadence) every `pollInterval` to detect player count, then check readiness. Stop polling once game started/cancelled.
8. **Host-first slot ordering (authoritative from lobby data):** slot 0 = player whose `Id == currentLobby.HostId`; remaining slots = other players sorted by `JoinedTime` (join order). `isLocal = player.Id == AuthenticationService.Instance.PlayerId`. UI consumes only this ordered list → host always appears in `player_slot1` on every client.
9. **Cleanup:** on cancel/timeout/leave, host `DeleteLobbyAsync` (or `RemovePlayerAsync` if joiner) + `NetworkManager.Shutdown()` if listening.
10. **Friends isolation:** Play Online filters strictly on `mode=GoFish`; Friends' `CreateLobby` sets no `mode` key, so the two flows never collide.

## Implementation tasks

### A. `Assets/Scripts/QuickMatchService.cs` — full implementation (primary)
Keep the public API used elsewhere: `Instance`, `IsSearching`, `RemainingSeconds`, `FindMatch()`, `Cancel()`, events `OnMatchFound`, `OnTimeout`, `OnError`. Keep the 20s `Update()` timeout.

**New config (no hardcode, all `[SerializeField]`):**
- `int minPlayersToStart = 2;`
- `int maxPlayers = 4;`
- `float searchTimeout = 20f;`
- `float pollInterval = 1.5f;`
- `AvatarDatabase avatarDatabase;`
- `string gameModeKey = "GoFish";`

**New roster data for UI (host-first ordering):**
```
public struct MatchPlayerInfo { public string name; public int avatarIndex; public bool isHost; public bool isLocal; }
public event System.Action<IReadOnlyList<MatchPlayerInfo>> OnRosterChanged;
```

**Implement:**
1. `FindMatch()`:
   - Reset state, `isSearching=true`, `searchTimer=0`, `pollTimer=0`.
   - `await TryQuickJoin();` → on success `isHost=false`, begin polling.
   - on `LobbyServiceException` (no lobby) → `await CreateAndHost();` → `isHost=true`.
   - on any exception → `OnError?.Invoke(msg)` then `LeaveAndCleanup()`.
2. `TryQuickJoin()`:
   - `QuickJoinLobbyAsync` with `QueryFilter` on `mode == gameModeKey`.
   - Read `relayCode` from `lobby.Data["relayCode"]`, `JoinAllocationAsync`, configure `UnityTransport.SetRelayServerData(...)` (copy from LobbyManager.JoinRelay lines 360-370), `NetworkManager.Singleton.StartClient()`.
   - Add own player Data (`name`, `avatar`) via `UpdatePlayerAsync`.
3. `CreateAndHost()`:
   - `CreateLobbyAsync(lobbyName, maxPlayers, new CreateLobbyOptions{ IsPrivate=false, Data={mode=gameModeKey} })`.
   - `CreateAllocationAsync(maxPlayers-1)` → `GetJoinCodeAsync` → store as `relayCode` in lobby Data (copy pattern from LobbyManager.CreateRelay lines 321-331).
   - Configure transport + `NetworkManager.Singleton.StartHost()` (copy lines 333-342).
   - Add own player Data (`name`, `avatar`).
4. **Polling in `Update()`** (only while `isSearching`): accumulate `pollTimer`; when `>= pollInterval`, call `RefreshLobby()` then `CheckMatchReady()`.
5. `RefreshLobby()` → `GetLobbyAsync(currentLobby.Id)`:
   - Build the **host-first ordered roster** (host = `HostId` first, then others by `JoinedTime`), reading each player's `name`/`avatar` from their player Data (local player from `ProfileData`), set `isLocal`.
   - Fire `OnRosterChanged(orderedRoster)` so the panel re-renders slots.
6. `CheckMatchReady()`:
   - if `currentLobby.Players.Count >= minPlayersToStart`:
     - `isSearching=false` (stop timeout), `OnMatchFound?.Invoke()`.
     - if `isHost`: activate `LobbyManager.Instance.enteringGamePanel` (mirror LobbyManager.StartGame line 489) for smooth transition, hide matchmaking/lobby/menu panels, then `NetworkGameManager.Instance.InitializeAndDeal()`.
     - if client: do nothing; `isGameStarted` NetworkVariable drives init.
   - else: return (keep waiting).
7. `Cancel()` / timeout / leave → `LeaveAndCleanup()`:
   - `isSearching=false`.
   - host: `DeleteLobbyAsync(id)`; joiner: `RemovePlayerAsync(id, playerId)`.
   - `NetworkManager.Shutdown()` if `IsListening`.
   - clear `currentLobby`, `relayCode`, `isHost`.
8. **Robustness:** null-check `NetworkManager.Singleton`, ensure `AuthenticationService.IsSignedIn`; catch `LobbyNotFound`/`Forbidden` during poll → treat as timeout (→ offline fallback).

### B. `Assets/Scripts/MatchmakingUIController.cs` — UX only
- Subscribe `OnRosterChanged` in `OnEnable`, unsubscribe in `OnDisable`.
- `StartSearching()`: `panel.SetActive(true)`, `statusText = "Searching for players..."`.
- **Slot rendering (`OnRosterChanged`):** iterate ordered roster and fill `playerSlots`:
  - **Slot 0 (`player_slot1`) = HOST** — host's name + avatar, on every client.
  - **Slots 1, 2, 3 = clients** in join order.
  - Resolve avatar sprite via `AvatarDatabase` — reuse `LobbyManager.Instance?.avatarDatabase` (no new reference).
  - Empty slots beyond roster count → `"Waiting..."` + null avatar (same as LobbyManager.UpdatePlayerSlotsUI line 515).
  - Do NOT hardcode the local player to slot 0 — local player sits wherever it falls in host-first ordering.
- Show `RemainingSeconds` in `timerText` (already done in `Update()`).
- On `OnMatchFound`: `statusText = "Match found! Starting game..."` (do NOT deactivate panel — `OnGameStartedChanged` hides it).
- Keep existing `HandleTimeout` → "No players found. Switching to offline..." → `OfflineFallback.Enter()` unchanged.
- Keep `OnCancelClicked` → `QuickMatchService.Cancel()` + return to menu unchanged.

### C. NO changes to
`LobbyManager.cs`, `NetworkGameManager.cs`, `NetworkPlayer.cs`, `NetworkPlayerManager.cs`, `NetworkController.cs`, `GameManager.cs`, `ModeSelectionController.cs`, `OfflineFallback.cs`, `MenuController.cs`, `MatchMakingStarter.cs`, `GameModeManager.cs`.

## Edge cases
- Two clients: A clicks Play Online first (becomes host), B clicks within 20s (quick-joins) → count=2 → host auto-starts → both enter multiplayer game (same as Friends).
- Only 1 player in 20s → timeout → offline fallback (offline mode selection + AI game).
- Cancel mid-search → clean leave of lobby + relay + network shutdown; menu restored.
- Host disconnects mid-search → client's `RefreshLobby` catches `LobbyNotFound` → treat as timeout → offline fallback.
- >=3 players: game still starts at 2 (does not wait for 4). Late joiners after start are out of scope (matchmaking stops once started).

## Validation plan (manual in Unity Editor with 2 builds)
1. **Offline regression:** Play Offline → 2/3/4 select → AI game works as before.
2. **Friends regression:** Create room + join by code → lobby → host Start → multiplayer game works as before.
3. **Online success (2 players):** Build 2 instances, both Play Online → matchmaking panel shows both within 20s → game auto-starts identically to Friends (seats, dealing, turns, books, go-fish, game-over).
4. **Online timeout:** 1 instance Play Online alone → 20s → "No players found" → offline mode selection appears.
5. **Online cancel:** cancel during search → returns to menu, no stuck network state.
6. **Slot ordering:** with host A + client B: on BOTH clients `player_slot1` shows **A (host)** name+avatar, slot 2 shows B. Add a 2nd/3rd client → fill slots 2,3 in join order; host stays slot 1. Empty slots show "Waiting...".

## Open items (safe defaults chosen)
- Quick-match host lobby name: generic constant `"Go Fish Match"` (filtered by `mode` Data, not by name).
- Friends lobbies do NOT set a `mode` key → never match the quick-join filter → the two flows stay isolated automatically.

## Files to edit (final scope)
1. `Assets/Scripts/QuickMatchService.cs` — full real quick-match implementation.
2. `Assets/Scripts/MatchmakingUIController.cs` — slot/entering UX only.
No other files. No scene/prefab changes.
