# Plan: Fix "Play Online" (Quick Match) Multiplayer Flow

## Diagnosis (Root Cause)
`QuickMatchService.cs` is a **stub**. It never talks to Unity Lobby/Relay:
- `FindMatch()` only sets `isSearching=true` + starts a 20s timer.
- `CheckMatchReady()` exists but is **never called**.
- `StartHostFlow()` / `StartClientFlow()` are empty (just `return Task.CompletedTask`).
- `OnPlayerJoined/OnPlayerLeft/OnMatchFound` events are declared but never fired.
- `MatchmakingUIController.playerSlots` (slots 0-3) is only filled for slot 0 by `MatchmakingStarter`; slots 1-3 are never updated from any lobby.

Result: Quick Match **always** times out and falls to offline, regardless of how many players click Play Online.

The **Friends lobby** (`LobbyManager`) and **Offline mode** (`GameManager.SetupGame`) already work and must NOT be touched.

Everything (menu, lobby, friends, matchmaking, gameplay, NetworkManager, NetworkPlayer prefab, NetworkGameManager, NetworkDeckManager, NetworkPlayerManager) lives in the single `GameScene.unity`, so Quick Match can reuse the **same** lobby+relay+network game-start path the Friends flow uses.

## Goal
- 4 players click **Play Online** → matched into ONE Unity Lobby.
- Matchmaking slots: **slot 0 = local player**, **slots 1-3 = the other players** (name + avatar shown, loaded from lobby player data).
- When the lobby reaches **4 players** → start the online game over Relay (host) and continue the existing `NetworkGameManager` gameplay.
- If 4 players are **not** found before the timeout → switch to offline mode (existing behavior, keep as-is).
- No hardcoding (serialized/config values), correct MVC separation, zero impact on Offline and Friends flows.

---

## Implementation Steps

### 1. `QuickMatchService.cs` — real quick-match backend (Model/Service)
Replace the stub with real Unity Lobby + Relay logic, mirroring `LobbyManager`'s proven pattern but **auto-matchmaking** (no room code):

**Config (serialized, no hardcode):**
- `requiredPlayers = 4` (already present).
- `searchTimeout` (already present).
- `pollInterval = 3f` (new serialized field, replaces dead `pollTimer`).
- `avatarDatabase` (already present).
- Add a const lobby filter key, e.g. `LOBBY_MODE_KEY = "mode"`, value `"quickmatch"` — used to **discover** quick-match lobbies so they never collide with Friends (code-based) rooms.

**`FindMatch()` flow (async):**
1. Set `isSearching=true`, reset timer.
2. Search open lobbies filtered by `mode == quickmatch` with available slots (`QueryLobbiesAsync` with filters). Use a `QuickMatchMode` data filter.
3. **If a matching lobby with space exists → JOIN** it (`JoinLobbyByIdAsync`), set own player data (`name` + `avatar`), `isHost=false`.
4. **Else → CREATE** a new lobby (`CreateLobbyAsync`, max 4, with `mode=quickmatch` data), set own player data, create Relay allocation, store `relayCode` in lobby data, `NetworkManager.Singleton.StartHost()`, `isHost=true`.
5. Start polling coroutine (`PollLobbyRoutine`).

**`PollLobbyRoutine()` (every `pollInterval`):**
- Host: `SendHeartbeatPingAsync` (keeps lobby alive).
- `GetLobbyAsync` to refresh `currentLobby`.
- Fire `OnPlayerJoined` / `OnPlayerLeft` with current count vs required (for UI).
- Call `CheckMatchReady()`.

**`CheckMatchReady()` (fix it + call it):**
- If `currentLobby.Players.Count >= requiredPlayers` → `isSearching=false`, fire `OnMatchFound`, then `StartOnlineGame()`.

**`StartOnlineGame()`:**
- Host: already started host in create-flow → directly call `NetworkGameManager.Instance.InitializeAndDeal()` (same as `LobbyManager.StartGame` game-trigger), after hiding panels via the existing `OnGameStartedChanged` path.
- Client: read `relayCode` from lobby data → `JoinAllocationAsync` → set transport → `StartClient()` (mirror `LobbyManager.JoinRelay`). The `isGameStarted` NetworkVariable change then drives game-start on the client.

**`Update()` timeout:** keep existing logic → `HandleTimeout()` → leave lobby + cleanup → fire `OnTimeout` (already wired to offline fallback). Add real lobby leave (`RemovePlayerAsync`) inside `LeaveLobbyAndCleanup`.

**Public helper for UI:** expose `GetLobbyPlayers()` (or fire `OnPlayerJoined(lobby player list)`) so the View can render slots without importing `Lobby` types directly (MVC: service owns lobby data, controller only renders).

**Robustness:** null-check `NetworkManager.Singleton`, try/catch all Lobby/Relay calls, guard against double `StartHost` (`IsListening`), unsubscribe events, clear `currentLobby`/`relayCode`/`isHost` on cleanup.

### 2. `MatchmakingUIController.cs` — render slots from lobby data (View)
- Subscribe to `QuickMatchService.OnPlayerJoined`, `OnPlayerLeft`, `OnMatchFound`, `OnTimeout` (the timeout handler already exists).
- New `RenderSlots()` method that mirrors `LobbyManager.UpdatePlayerSlotsUI` logic:
  - **Local player first → slot 0** (name + avatar from `ProfileData` / `AvatarDatabase`).
  - Other lobby players → slots 1..n (name + avatar from lobby player `Data["name"]` / `Data["avatar"]`).
  - Empty slots → "Waiting...".
- On `OnMatchFound` → `StopSearching()` (hide panel); the entering-game panel / game scene is shown by the existing `NetworkGameManager.OnGameStartedChanged → HideAllLobbyPanels/ActivateGameScene` path. Ensure `HideAllLobbyPanels` also hides `matchmakingPanel` (it already references `lm.matchmakingPanel`).
- Remove the duplicate slot-0 manual set in `MatchmakingStarter` OR keep as a pre-lobby placeholder and let `RenderSlots` overwrite once lobby data arrives (preferred: keep placeholder, overwrite on first poll).

### 3. Keep Offline & Friends untouched (verification, no code change needed)
- **Offline**: `MenuController.PlayOffline → ShowModeSelection → ContinueGame → GameManager.SetupGame()`. Not on the QuickMatch path. No change.
- **Friends**: `LobbyManager` uses code-based rooms (`CreateLobby/JoinLobby`) and is never invoked by `QuickMatchService`. The new `mode=quickmatch` lobby filter guarantees Friends rooms (which are joined by code, not discovered) never appear in quick-match search. No change to `LobbyManager`.
- `GameModeManager.isOnlineMode` still gates `MatchmakingStarter` (only runs for Play Online).

### 4. MVC structure summary
- **Model/Service**: `QuickMatchService` — owns lobby/relay lifecycle & player roster; exposes data via events/getters. No UI references.
- **View**: `MatchmakingUIController` — only renders `playerSlots` from service data; no lobby API calls.
- **Controller/Flow**: `MatchmakingStarter` (entry), `NetworkGameManager` (existing game-start trigger) — unchanged in responsibility.

---

## Files Changed
| File | Change |
|------|--------|
| `Assets/Scripts/QuickMatchService.cs` | Implement real lobby+relay quick-match, polling, match-ready, host/client start, cleanup. |
| `Assets/Scripts/MatchmakingUIController.cs` | Subscribe to service events; render slots 0-3 (local first) with name+avatar. |
| `Assets/Scripts/MatchmakingStarter.cs` | Minor: keep slot-0 placeholder; ensure `FindMatch` is awaited correctly (already called). |

No changes to: `LobbyManager.cs`, `NetworkGameManager.cs`, `GameManager.cs`, `NetworkPlayer.cs`, `MenuController.cs`, `ModeSelectionController.cs`, `OfflineFallback.cs`.

## Verification
- 4 devices/sessions click Play Online → all land in one lobby; slots fill with real names+avatars; game starts automatically over Relay and gameplay continues via existing `NetworkGameManager`.
- Fewer than 4 within timeout → cleanly leaves lobby → falls to offline mode selection (unchanged).
- Offline and Friends flows behave exactly as before.
- No compile errors (no new dependencies beyond already-used `Unity.Services.Lobbies` / `Unity.Services.Relay` / `Unity.Netcode`).
