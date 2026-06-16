# Plan: Fix Play Online Crash (NRE) + Finish the Host/Relay Flow

## Note
This model cannot read images, so the screenshot wasn't viewable — but the stack trace pinpoints the bug exactly.

## Status of the code
The previous plan was **partially applied**:
- ✅ `LobbyPlayerInfo` DTO + `GetLobbyPlayers()` added.
- ✅ Typed `AvatarDatabase` accessor + Awake fallback.
- ✅ `OnLobbyUpdated` event + fired in `RefreshLobby`.
- ❌ **Host create path still broken** — bare `CreateLobbyAsync` (no `mode` data, no `CreateRelay`, no `StartHost`).
- ❌ **No lobby heartbeat** added.
- ❌ **No `lobby.Data` null-check** → this is the crash.

---

## THE CRASH (current error)
```
NullReferenceException at QuickMatchService.FindOrCreateLobby() line 97
```
Line 97 is:
```csharp
if (!lobby.Data.ContainsKey(LOBBY_MODE_KEY))   // lobby.Data is NULL
```
`QueryLobbiesAsync` returns ALL public lobbies. A lobby created with **no data dictionary** has `lobby.Data == null`, so `.ContainsKey(...)` throws. Since Quick Match lists every public lobby (it doesn't filter server-side), any data-less lobby (including stray/Friends/public lobbies) crashes the loop the moment it appears.

## Fix 1 — Null-safe search loop (`FindOrCreateLobby`)
Guard both the result list and each lobby's data dictionary:
```csharp
if (result == null || result.Results == null)   // nothing to search → go create
{
    // fall through to create branch
}
else
{
    foreach (Lobby lobby in result.Results)
    {
        if (lobby == null) continue;
        if (lobby.Data == null || !lobby.Data.ContainsKey(LOBBY_MODE_KEY)) continue;  // <-- THE FIX
        if (lobby.Data[LOBBY_MODE_KEY].Value != QUICKMATCH_MODE) continue;
        if (lobby.AvailableSlots <= 0) continue;
        foundLobby = lobby;
        break;
    }
}
```
This alone removes the NRE. But Play Online still won't connect until Fix 2 + 3.

## Fix 2 — Host create path (make the lobby discoverable + relay-ready)
The current `else` branch is broken:
```csharp
currentLobby = await LobbyService.Instance.CreateLobbyAsync("QuickMatch", requiredPlayers); // no mode data, no relay, no host
```
Replace the **create branch body only** with:
```csharp
await CreateQuickMatchLobby();   // public lobby with mode=quickmatch data (already exists)
isHost = true;
await CreateRelay();             // allocation + relayCode stored in lobby + StartHost()
```
- `CreateQuickMatchLobby()` (line 189) already sets `mode=quickmatch` as **Public** visibility → the lobby is now returned by `QueryLobbies` and matched by the loop (Fix 1). Dead code becomes live.
- `CreateRelay()` (line 316) already stores `relayCode` in lobby data and calls `StartHost()`. Clients then read `relayCode` in `JoinRelay()`.
- Keep the shared `UpdatePlayerAsync(name/avatar)` after the branch; keep the **join** branch unchanged (`JoinLobbyByIdAsync`, `isHost = false`).

This makes P1 create a real, discoverable, relay-backed host lobby.

## Fix 3 — Lobby heartbeat (host) in `Update`
Without it Unity deletes the lobby ~30s after creation, so joiners never find it. In the poll block (`if (pollTimer >= pollInterval)`), host pings (failures ignored):
```csharp
if (isHost && currentLobby != null)
{
    try { _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id); } catch { }
}
```

## Fix 4 — Avoid spurious self-join on first refresh
`previousPlayerCount` starts at 0, so the host's own presence fires a fake `OnPlayerJoined`. Add `private bool firstRefresh;` and in `RefreshLobby()`, on the first successful fetch set `oldCount = newCount` before the comparison.

---

## Why this makes Play Online work (full flow)
1. **P1** → `FindMatch` → search finds nothing → creates public `mode=quickmatch` lobby → `CreateRelay` stores relayCode + `StartHost()` → heartbeat keeps it alive. Slot 0 = local player.
2. **P2–P4** → search now **finds** P1's lobby (it has `mode` data + free slots, no NRE) → join → `UpdatePlayerAsync(name/avatar)`. `RefreshLobby` re-renders slots via `OnLobbyUpdated`.
3. **At 4** → `CheckMatchReady` → host `StartHostFlow` → `InitializeAndDeal()` (valid: host running) deals; `isGameStarted` fires → all clients enter gameplay via the **existing unchanged** `NetworkGameManager.OnGameStartedChanged → HideAllLobbyPanels → ActivateGameScene` path.
4. **Timeout <4** → `LeaveLobbyAndCleanup` → offline fallback (unchanged).

## Files changed
- `Assets/Scripts/QuickMatchService.cs` only:
  - Fix 1: null-safe `result`/`lobby.Data` in search loop.
  - Fix 2: create branch → `CreateQuickMatchLobby()` + `CreateRelay()`.
  - Fix 3: host heartbeat in poll.
  - Fix 4: `firstRefresh` guard.

## NOT touched (verified safe)
- **Offline** (`MenuController.PlayOffline → GameManager.SetupGame`) — different path.
- **Friends** (`LobbyManager` code rooms) — Quick Match matches only `mode=quickmatch` lobbies; Friends use `LobbyCode`. No collision.
- **Gameplay** — `NetworkGameManager`, `GameManager`, `NetworkPlayer`, `NetworkDeckManager` reused unchanged.
- `MatchmakingUIController.cs` — already correct (compiles, renders via DTO + `OnLobbyUpdated`).

## Verification
- Clicking Play Online **no longer throws NRE** (null-safe loop).
- P1 creates a discoverable `mode=quickmatch` lobby + relay + host; lobby stays alive via heartbeat.
- P2–P4 find & join it; slots fill with real names/avatars.
- At 4 players the game deals and all clients enter online gameplay (unchanged path).
- Offline and Friends flows behave exactly as before.
