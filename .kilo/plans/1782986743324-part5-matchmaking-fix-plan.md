# Part 5 Matchmaking Fix Plan — QuickMatchService + MatchmakingUIController

## Goal
Make the full Part-5 matchmaking flow behave as expected for 1..4 players:
lobby creation, reliable polling, a second device actually joining, roster/slot
UI updating on both sides, match-ready detection, and clean state on
cancel/timeout. Currently the host shows `1 / 4 Players Found` (correct), but
the rest of the Part-5 flow (refreshing, 2nd player join, slots, match found,
cleanup) does not complete because of several defects below.

> Note: `1 / 4 Players Found` for a single host is CORRECT. Nothing to change there.

## Scope of this plan (Parts 1–5 only)
- IN SCOPE: lobby + relay wiring reliability, polling, roster UI, heartbeat,
  leave/cleanup, event-subscription robustness, match-ready signal.
- OUT OF SCOPE (later parts): actual gameplay scene transition / game start,
  in-game Netcode gameplay sync, AI fallback balancing. `StartHostFlow` /
  `StartClientFlow` stay stubs that only log (Part 6+).

## Root-cause defects to fix

### D1. No lobby heartbeat (BLOCKER for 2nd player)
- File: `Assets/Scripts/QuickMatchService.cs`
- Unity deletes a lobby ~30s after creation if the host never pings it. The host
  never calls `LobbyService.Instance.SendHeartbeatPingAsync`, so a second device
  running `QuickJoinLobbyAsync` finds nothing and the host stays at `1 / 4`
  forever, then times out.
- Fix: in `Update()`, for host only, accumulate a heartbeat timer (~every 15s,
  configurable field `heartbeatInterval`, default 15f) and fire
  `SendHeartbeatPingAsync(currentLobby.Id)` with fire-and-forget + try/catch.

### D2. Event-subscription race (BLOCKER for UI updates)
- File: `Assets/Scripts/MatchmakingUIController.cs`
- `OnEnable()` bails completely when `QuickMatchService.Instance` is null.
  Because Unity init order is non-deterministic, the UI may never subscribe to
  `OnRosterChanged` / `OnMatchFound` / `OnTimeout`, so `RenderRoster` is never
  called even though the service fires the event.
- Fix: do not subscribe in `OnEnable` guarded by null. Instead subscribe in
  `Start()` and re-subscribe defensively in `StartSearching()` (idempotent:
  always `-=` then `+=`). Also expose a public helper on the UI to bind late.
- Also guard against double-subscription: unsubscribe before subscribe every time.

### D3. Client relay join can throw before host publishes relayCode
- File: `QuickMatchService.JoinRelay()` (line ~233)
- A client that quick-joins before the host has written `RELAY_CODE_KEY` into
  lobby data hits `currentLobby.Data[RELAY_CODE_KEY]` → KeyNotFound → falls into
  the generic catch in `FindMatch`, sets `isSearching=false`, broken state.
- Fix: after a successful quick join, poll `GetLobbyAsync` a few times (bounded
  retry, e.g. up to ~6 tries with short delay) until `RELAY_CODE_KEY` exists,
  THEN `JoinRelay`. If never appears, treat as failure (error event + cleanup).

### D4. `LeaveLobbyAndCleanup` never actually leaves (cleanup defect)
- File: `QuickMatchService.LeaveLobbyAndCleanup()` (line ~392)
- It only nulls local references; the lobby/player remain server-side. Stale
  lobbies accumulate and break re-queueing (a second `FindMatch` may quick-join
  a dead lobby).
- Fix: make it async. If host, `DeleteLobbyAsync(currentLobby.Id)`; if client,
  `RemovePlayerAsync(currentLobby.Id, localPlayerId)`. Guard with try/catch so a
  network failure does not break local reset. Then clear local fields
  (`currentLobby = null`, `relayCode = ""`, `isHost = false`, `searchTimer = 0`).
- Convert callers (`TimeoutRoutine`, `Cancel`) to await it (TimeoutRoutine can
  keep coroutine wrapping an async via `_ = LeaveAsync()`).

### D5. Non-stable roster ordering
- File: `RefreshLobby()` sort (line ~342)
- The comparator returns 0 when `IsHost` is equal, so non-host player order is
  arbitrary across devices (slots may differ between host and client).
- Fix: stable secondary sort. Primary by `IsHost` (host first), secondary by
  player `Id` (string compare) so both devices compute identical slot order.

### D6. `minPlayersToStart = 2` behaviour is correct
- No change. With one player, `CheckMatchReady` correctly stays quiet and the
  host waits. This is expected; do not lower it.

### D7. Match-ready repeatedly fires `OnMatchFound`
- File: `CheckMatchReady()` (line ~356)
- Called every poll; once count >= min it re-fires `OnMatchFound` every cycle.
- Fix: add a `matchFoundFired` bool guard; fire `OnMatchFound` + `StartOnlineGame`
  exactly once, then stop polling (set `isSearching = false`).

## Affected boundaries / data flow
- Entry: `MatchMakingStarter.Start()` (scene start, online mode) →
  `MatchmakingUIController.StartSearching()` then
  `QuickMatchService.FindMatch()`. (Already wired correctly — keep.)
- Polling: `QuickMatchService.Update()` → `RefreshLobby()` → `OnRosterChanged`
  (UI `RenderRoster`) + `CheckMatchReady()` → `OnMatchFound` (UI status) +
  `StartOnlineGame()` stub.
- Exit: timeout → `OnTimeout` (UI offline fallback) ; cancel button →
  `OnCancelClicked` → `QuickMatchService.Cancel()`.

## Expected output after this plan (Part 1–5 acceptance)

### Single host device (console)
```
Online mode detected → starting matchmaking
QuickMatchService FindMatch called
Trying Quick Join...
Creating New Lobby...
Created Lobby : <id>
Creating Relay Allocation...
Relay Code : <code>
Relay Host Started
---------------------------
Lobby Refreshed
Players : 1
Updating Matchmaking UI : 1 players
Rendering Matchmaking Slots
Slot 0 -> <PlayerName>
```
(repeats every 1.5s; UI shows `1 / 4 Players Found`, host in slot 0, others
`Waiting...`, timer counting down; no `Match Found` because count < min.)

### Second device joins (client console)
```
QuickMatchService FindMatch called
Trying Quick Join...
Joined Lobby : <id>
Relay Client Started
```
Both devices then print `Lobby Refreshed / Players : 2 / Updating Matchmaking UI
: 2 players`, and both UIs show `2 / 4 Players Found` with identical slot order
(host = slot 0, client = slot 1).

### Match ready
When count reaches `minPlayersToStart` (2), exactly once:
```
Required players found
Match Found! (UI status)
Starting Host Flow / Starting Client Flow   (stubs, Part 6 fills these)
```
and polling stops.

## Validation steps
1. Play one build as host → confirm console matches the single-host section and
   UI shows `1 / 4`, host in slot 0, timer running. Verify no exceptions.
2. Start a second build/device → confirm it quick-joins the existing lobby
   (proves D1 heartbeat + D3 relay-code wait), both UIs reach `2 / 4` with
   identical slot order (D5), and `Match Found` fires once on both (D7).
3. Cancel mid-search on host → confirm lobby is deleted (D4), client detects
   removal / returns to menu, no stale lobby remains (check Unity Dashboard >
   Lobbies).
4. Let timer expire with one player → confirm offline fallback triggers and
   lobby removed (D4) instead of being left dangling.
5. Re-queue immediately after cancel/timeout → confirm it creates a fresh lobby
   (no false quick-join into a dead lobby).

## Risks / notes
- Heartbeat and leave calls are network ops; wrap each in try/catch so they
  never break the local reset path. Keep all Debug.Log diagnostic logs added in
  Part 5 (they are intentional dev instrumentation).
- Do NOT change gameplay startup in this plan; `StartHostFlow`/`StartClientFlow`
  remain stubs (Part 6).
- The premature logs seen during matchmaking (`NetworkGameManager Spawned`,
  `Deck Created`, `Deck Shuffled`) suggest gameplay prefabs initialize during the
  host start in the matchmaking scene. This is a sequencing concern to address
  in the gameplay-transition part, NOT in this Part-5 reliability plan.

## Implementation order (for the implementing agent)
1. D1 heartbeat field + Update call (host only).
2. D4 real async leave/delete/remove; wire into Cancel and TimeoutRoutine.
3. D3 client relay-code wait loop in FindMatch/TryQuickJoin path.
4. D7 matchFoundFired guard in CheckMatchReady; stop polling after fire.
5. D5 stable roster sort (host first, then player Id).
6. D2 robust UI subscription (Start + StartSearching idempotent).
7. Run Validation steps 1–5.
