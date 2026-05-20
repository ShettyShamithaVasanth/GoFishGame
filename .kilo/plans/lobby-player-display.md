# Plan: Fix Other Players Not Showing in Lobby (Definitive)

## Root Cause Analysis

The previous `UpdatePlayerSlotsUI()` with `ProfileData` for local player is correct. The issue is **why `currentLobby.Players` doesn't contain the other player's data**.

Two problems:
1. **Unity Lobby caching**: `GetLobbyAsync` returns cached data (updates every 1-5s). Host's poll might keep getting stale data.
2. **Rate limiting**: Both host AND client poll every 2s = 2 requests/2s per lobby. Unity's limit is 5 requests/5s per lobby. We're at the edge.
3. **No event-driven refresh**: Host only discovers new players via polling — no immediate notification when a client connects.

## Changes (1 file: `LobbyManager.cs`)

### Change 1: Use `OnClientConnectedCallback` to trigger immediate lobby refresh

This is the key fix. When a client connects to the Netcode relay, the host immediately refreshes the lobby data. This bypasses the polling delay entirely.

**Add to `OnEnable()` (after line 109):**
```csharp
if (NetworkManager.Singleton != null)
{
    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedRefresh;
}
```

**Add to `OnDisable()` (after line 116):**
```csharp
if (NetworkManager.Singleton != null)
{
    NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedRefresh;
}
```

**Add new method:**
```csharp
async void OnClientConnectedRefresh(ulong clientId)
{
    if (currentLobby == null) return;
    Debug.Log("[LobbyRefresh] Client connected: " + clientId + " — refreshing lobby");
    await RefreshLobby();
}
```

### Change 2: Increase polling interval from 2s to 5s

Reduce polling since we now have event-driven refresh. This avoids rate limiting.

```csharp
// BEFORE (line 36)
float pollInterval = 2f;

// AFTER
float pollInterval = 5f;
```

### Change 3: Add diagnostic logging to `UpdatePlayerSlotsUI()`

Add a single summary log to see exactly what data is available:

```csharp
// After line 549 (before the loops), add:
Debug.Log($"[LobbySlots] Players in lobby: {currentLobby.Players.Count}, LocalPlayerId: {localPlayerId}");
foreach (var p in currentLobby.Players)
{
    string dataInfo = p.Data != null
        ? $"name={(p.Data.ContainsKey("name") ? p.Data["name"].Value : "MISSING")}, avatar={(p.Data.ContainsKey("avatar") ? p.Data["avatar"].Value : "MISSING")}"
        : "Data=NULL";
    Debug.Log($"[LobbySlots] Player {p.Id}: {dataInfo}");
}
```

This will show in the Unity Console exactly what data each player has, so we can confirm whether:
- The lobby has 2 players ✓
- The other player's Data contains "name" and "avatar" ✓ or is null ✗

## Summary of All Changes

| Change | Why |
|--------|-----|
| `OnClientConnectedCallback` → immediate `RefreshLobby()` | Host sees client within 1s of connection, not 2-5s |
| Poll interval 2s → 5s | Avoids rate limiting when both host+client poll |
| Diagnostic logging | Shows exact lobby data to confirm the fix works |

## Expected Console Output After Fix

When client connects, host console should show:
```
[LobbyRefresh] Client connected: 1 — refreshing lobby
Players in lobby: 2
[LobbySlots] Players in lobby: 2, LocalPlayerId: ABC123
[LobbySlots] Player ABC123: name=Maya, avatar=2
[LobbySlots] Player XYZ789: name=Riya, avatar=1
Slot0 → Maya, AvatarIndex: 2, IsLocal: True
Slot1 → Riya, AvatarIndex: 1, IsLocal: False
```

## Offline Impact: NONE
Only lobby polling and event handlers — online-only code paths.
