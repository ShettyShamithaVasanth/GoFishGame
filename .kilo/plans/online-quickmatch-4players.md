# Online Quick-Match: gather 4 players, then start (or fall back to offline)

## Problem (root cause)
When a player clicks **Play Online**, the flow is:
- `MenuController.PlayOnline()` → `GameModeManager.isOnlineMode = true` → `SceneManager.LoadScene("GameScene")` (MenuController.cs:74).
- `MatchmakingStarter.Start()` shows the matchmaking panel, fills **player_0** with the local profile, then calls `QuickMatchService.Instance.FindMatch()` (MatchmakingStarter.cs:51).
- **`QuickMatchService.FindMatch()` only sets `isSearching = true` and a 20 s timer.** It never creates/joins/searches a lobby, never calls `CheckMatchReady()`, and `OnPlayerJoined`/`OnMatchFound`/`StartHostFlow`/`StartClientFlow` are all empty stubs (QuickMatchService.cs:37–105).

Result: the timer ALWAYS expires → `MatchmakingUIController.HandleNoPlayersFound()` → `OfflineFallback.Enter()`. Online quick-match is effectively non-functional. Avatars/names of players 1–3 are never shown because no lobby data ever arrives.

## Goal
Make Play Online behave like a professional Go Fish quick-match:
1. Search for / create a **Quick-Match lobby** (max 4, tagged `gameMode=quickmatch`).
2. Show joined players in the matchmaking slots: **slot 0 = local player**, slots 1–3 = others, each with **name + avatar**.
3. When **all 4 join** → host creates Relay + starts host, clients join Relay + start client, then `NetworkGameManager` deals and the existing online gameplay runs (no gameplay-logic changes).
4. If **timeout** (not enough players) → unchanged offline fallback (`OfflineFallback.Enter()`).

**Untouched:** offline gameplay (`SetupGame` path), Play-with-Friends lobby/gameplay (`LobbyManager`), all MVC-pure logic in `core/`. Every new behaviour stays gated behind the existing quick-match path.

---

## Scope of changes (MVC mapping)

| File | Change | Layer |
|------|--------|-------|
| `QuickMatchService.cs` | **Rewrite** to a real Lobby+Relay quick-match driver (was a stub). | Network Service |
| `MatchmakingUIController.cs` | Subscribe to new events; render slots 0–3 (local=0, others=1..3) with name+avatar; keep offline fallback. | View |
| `MatchmakingStarter.cs` | Minor: ensure local slot uses `AvatarDatabase`; start search unchanged. | View/Boot |
| `LobbyManager.cs` | **No behavioural change.** Possibly extract the relay create/join helpers into `static` reusable methods (or keep duplicate-free by adding `public static` helpers) so Quick-Match and Friends share the same primitives. | Service |

No `core/` file is modified. `GameManager` (offline + online gameplay) is NOT modified. `NetworkGameManager`/`NetworkPlayerManager`/`NetworkPlayer`/`NetworkDeckManager` are NOT modified — they already support human online play end-to-end once `NetworkManager` connects and `InitializeAndDeal()` is called.

---

## Detailed design

### A. `QuickMatchService` (rewrite) — the core fix

Public config (serialized, no hardcode):
- `int requiredPlayers = 4` (already exists).
- `float searchTimeout = 20f` (already exists).
- `float pollInterval = 1.5f` (new, for lobby polling + heartbeat).
- `string gameModeKey = "gameMode"`, `string quickMatchValue = "quickmatch"`, `string relayCodeKey = "relayCode"` (constants).

State machine:
- `Idle` → `Searching` (FindMatch) → `InLobby` (joined/created) → `Starting` (4 reached) → `InGame`.
- `Searching` still drives the existing timeout (`Update` + `HandleTimeout`), so offline fallback stays identical.

`FindMatch()` (async):
1. Set `isSearching = true`, `searchTimer = 0`. Fire `OnSearchStarted`.
2. Write player data (name + avatar) into the session via `UpdatePlayerAsync` once a lobby is acquired (same pattern as `LobbyManager.JoinLobby`).
3. **Find a lobby:**
   - Use `LobbyService.Instance.QuickJoinAsync` with a `QueryFilter` on `gameMode == quickmatch` AND `availableSlots > 0`. Quick-join is the professional choice; if no matching lobby exists it throws `LobbyServiceException` (NoLobbiesFound).
   - On `NoLobbiesFound` → **create** a lobby: `CreateLobbyAsync("Quick Match", requiredPlayers, new CreateLobbyOptions { Data = { gameMode = quickmatch }, IsPrivate = false })`, then `UpdatePlayerAsync` with name/avatar, mark `isHost = true`.
4. Store `currentLobby`. Subscribe to lobby events via polling (`GetLobbyAsync` every `pollInterval`).
5. Host runs `LobbyService.Instance.SendHeartbeatPingAsync` periodically (required for host lobbies).

Polling loop (every `pollInterval`) while `InLobby`:
1. `currentLobby = await GetLobbyAsync(currentLobby.Id)`.
2. Compute joined players → emit `OnPlayersUpdated(List<PlayerProfileInfo>)` where each entry = `{playfabId, name, avatarIndex, isLocal}`. Local player is always index 0 in the list (the view renders slot 0 = local).
3. `CheckMatchReady()`:
   - If `Players.Count < requiredPlayers` → stay.
   - If `Players.Count == requiredPlayers` → `OnMatchFound`, then `StartOnlineGame()`.

`StartOnlineGame()`:
- Host path (`StartHostFlow`):
  - Create Relay allocation (`RelayService.Instance.CreateAllocationAsync(requiredPlayers - 1)`), get join code (`GetJoinCodeAsync`).
  - Store join code into lobby Data (`UpdateLobbyAsync` with `relayCode`).
  - Configure `UnityTransport.SetRelayServerData(...)`, `NetworkManager.Singleton.StartHost()`.
  - Call `NetworkGameManager.Instance.InitializeAndDeal()` (already deals cards, sets `isGameStarted`, triggers `OnGameStartedChanged` which hides panels + initialises multiplayer on every client). Reuse the EXISTING game-start path.
- Client path (`StartClientFlow`):
  - Read `relayCode` from lobby Data.
  - `JoinAllocationAsync`, configure transport, `NetworkManager.Singleton.StartClient()`.
  - `NetworkGameManager.OnGameStartedChanged` already handles client-side initialisation (waits for `NetworkPlayer`s, calls `gm.InitializeMultiplayer`, requests full state). No change.

Cleanup on timeout/cancel (already exists as `LeaveLobbyAndCleanup`):
- Add `RemovePlayerAsync(currentLobby.Id, playerId)` for the real leave, then null `currentLobby`. Keep firing `OnTimeout` (unchanged) so the existing offline fallback runs.

Edge cases:
- Lobby full mid-search → catch and fall back to create.
- Relay creation fails → `OnError` + cancel + offline fallback.
- Host leaves lobby while clients wait → on next poll, `LobbyNotFound`/`Forbidden` → cancel → offline fallback (host migration is explicitly out of scope; tracked separately in `host-migration-go-fish.md`).
- Determinism: slot ordering uses lobby player join order with local forced to index 0 (mirrors `PlayerSeatMapper.BuildSeatMap` logic) so every client sees itself at the bottom seat.

### B. `MatchmakingUIController` (view-only edits)
- Add fields: `AvatarDatabase avatarDatabase` (already used by LobbyManager; reference the same asset), keep `PlayerProfileUI[] playerSlots` (size 4).
- Subscribe to `QuickMatchService.OnPlayersUpdated` → render:
  - Slot 0 = local player (own name+avatar from `ProfileData`).
  - Slots 1..n-1 = remote players (name+avatar from lobby player data → `AvatarDatabase.avatarSprites[index]`).
  - Empty slots show "Waiting...".
- Subscribe to `QuickMatchService.OnMatchFound` → `StopSearching()` (hide panel) — host/networkmanager then drives the game scene UI.
- Keep `HandleTimeout` → `OfflineFallback.Enter()` **exactly as is**.

### C. `MatchmakingStarter` (minor)
- Resolve `AvatarDatabase` and apply local profile to `playerSlots[0]` via the controller (single source of truth), instead of directly calling `myProfileUI.SetProfile` (so the controller owns the slot rendering). Behavior preserved.
- `FindMatch()` call unchanged.

### D. `LobbyManager` (no behavioural change)
- To avoid duplicating Relay create/join code, extract two `public static` helpers used by BOTH Friends and Quick-Match:
  - `static Task<(Allocation,string)> CreateRelayAllocation(int maxPlayers)`
  - `static Task ConfigureAndStartHost(Allocation)` / `ConfigureAndJoinAndStartClient(string code)`
  - Friends flow calls the same helpers — behaviour identical. (If extraction risks the Friends path, the plan allows Quick-Match to duplicate just the ~20 lines instead; either way Friends is not regressed.)

---

## Verification (manual + console)
1. **1 client alone:** Play Online → slot 0 shows own name+avatar, slots 1–3 "Waiting..." → after 20 s → offline mode starts (offline UI unaffected).
2. **4 clients:** Play Online on each → each sees itself at slot 0, others in 1–3 with correct name+avatar → at 4, host deals, all clients enter game scene with 4 human players, turns work (existing `NetworkGameManager` path).
3. **Cancel** during search → returns to menu (unchanged).
4. **Offline (Play Offline)** → unchanged; `SetupGame` + AI run as before.
5. **Play with Friends** → create/join/start via `LobbyManager` unaffected.

## Out of scope
- Host migration (separate plan: `host-migration-go-fish.md`).
- Mixed AI+human online games / server-side AI.
- Mid-match reconnection or late join.

## Build order
1. Rewrite `QuickMatchService` (search/create/poll/start/cleanup) — compiles, still falls back to offline if lobby calls fail.
2. Wire `MatchmakingUIController` slot rendering to `OnPlayersUpdated`.
3. Extract/verify Relay helpers (Friends path re-test).
4. Manual 2-client smoke test (creates/joins lobby, names+avatars show), then 4-client full match test.
