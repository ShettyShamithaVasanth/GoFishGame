  # Plan: Host-First Player Slots in Play Online (Quick Match) Matchmaking Panel

## Goal
In the **Play Online** matchmaking panel, the player slots must always render in a fixed, professional order:
- **Slot 0 = Host** (lobby creator)
- **Slots 1-3 = Clients** (joined players)

This must match the behavior of the Play With Friends lobby, use a clean MVC structure, be data-driven (no hardcoding), and must **not** affect offline mode or the Play With Friends flow.

## Root Cause
`QuickMatchService.GetLobbyPlayers()` (`Assets/Scripts/QuickMatchService.cs:316-323`) sorts players with the **local player first**:

```csharp
list.Sort((a, b) =>
{
    if (a.IsLocal == b.IsLocal)
        return 0;
    return a.IsLocal ? -1 : 1;
});
```

This means whoever is searching locally always appears in slot 0, regardless of whether they are the actual host. This is why the Play Online panel does not consistently show the host first.

By contrast, `LobbyManager.UpdatePlayerSlotsUI()` (`Assets/Scripts/LobbyManager.cs:519-567`) iterates `currentLobby.Players` in server order — and Unity Lobby keeps the host (creator) as the first entry, so the Friends lobby naturally shows host first.

## Fix (single, clean change in the model/service layer)

**File:** `Assets/Scripts/QuickMatchService.cs` — method `GetLobbyPlayers()` (lines 257-325).

1. Capture the lobby's `HostId` (`currentLobby.HostId`) — this is the authoritative host identity provided by the Unity Lobby SDK (no hardcoding).
2. Replace the `IsLocal`-first sort with a **host-first** sort:
   - Host (`player.Id == HostId`) → comes first.
   - All other players keep their natural lobby join order (stable relative ordering).
3. Keep `IsLocal` only for UI highlighting logic (not for ordering), preserving the existing `LobbyPlayerInfo.IsLocal` field contract so the rest of the codebase is unaffected.

Pseudo-code:
```csharp
string hostId = currentLobby.HostId;
string localId = AuthenticationService.Instance.PlayerId;

// ... existing loop building LobbyPlayerInfo (unchanged) ...

// Host always in slot 0; clients keep lobby join order after that.
list.Sort((a, b) =>
{
    bool aHost = a.Id == hostId;
    bool bHost = b.Id == hostId;
    if (aHost != bHost) return aHost ? -1 : 1;
    return 0; // stable: preserves join order among non-hosts
});
```

This requires `LobbyPlayerInfo` to also carry the player's `Id` (add a `string Id` field, populated from `player.Id` during the loop). `IsLocal` is retained for any UI highlight use.

## Why this is correct & safe
- **Data-driven / no hardcode:** Uses `currentLobby.HostId` from the SDK.
- **Matches Friends lobby behavior:** Friends already relies on server join order; now Quick Match explicitly pins host to slot 0 for consistency even under reordering.
- **MVC respected:** Ordering lives in the service/model layer (`QuickMatchService`); `MatchmakingUIController.RenderSlots()` simply consumes the ordered list — no UI logic change needed.
- **Offline mode untouched:** `QuickMatchService` is online-only; offline gameplay never touches it.
- **Friends lobby untouched:** `LobbyManager` is not modified.

## Files Changed
- `Assets/Scripts/QuickMatchService.cs`
  - `LobbyPlayerInfo` struct: add `string Id`.
  - `GetLobbyPlayers()`: populate `Id`, change sort to host-first.

## Optional / Out of Scope (verify only, no code change)
- Confirm the matchmaking panel's `MatchmakingUIController.playerSlots` array in the Unity scene has **4 slots** assigned (slot 0 = host, slots 1-3 = clients). This is a scene-inspector setting, not a code change.
- Optionally tag slot 0 with a "Host" label in UI for professionalism — only if requested.

## Verification
- Host a Quick Match: slot 0 shows the host's name/avatar, remaining slots fill as clients join in order.
- Join an existing Quick Match as a client: the original host remains in slot 0; local client appears in a later slot.
- Verify Play With Friends lobby still shows host first (unchanged).
- Verify offline gameplay still works (QuickMatchService not involved).
