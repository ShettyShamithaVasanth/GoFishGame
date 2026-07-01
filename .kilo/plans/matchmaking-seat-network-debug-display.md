# Plan: Show Seat ID + Network ID in Matchmaking Panel (Test Display)

## Context
- The host-first sort fix (Changes 1–5 from the previous task) is **already applied** in `QuickMatchService.cs`. Confirmed: `LobbyPlayerInfo.Id`, `hostId` capture, `Id = player.Id`, and the host-first `list.Sort` are all present.
- New goal: in the **Quick Match matchmaking panel** only, show each player's **seat_id** and **network_id** next to their name, so ordering can be verified during testing.
- Scope is **Quick Match only**. Friends Lobby (`LobbyManager`), Offline mode, and gameplay are untouched.

## What the IDs mean at matchmaking time
- **seat_id** = the slot index (0..3) of the player in the ordered list returned by `GetLobbyPlayers()` (host = 0). Netcode isn't connected yet during the lobby panel, so this is the seat position we control.
- **network_id** = the lobby player's stable identifier. At matchmaking time the Netcode `ClientId` (ulong) is **not yet assigned** (Relay/netcode connects only when the match is found). So we display the Unity Authentication `player.Id` (already stored in `LobbyPlayerInfo.Id`) as the network identity, truncated for readability.

> No hardcoding of IDs. Both values come from live lobby data.

## Files to modify
1. `Assets/Scripts/QuickMatchService.cs` — add `SeatIndex` to the struct + assign it after sort.
2. `Assets/Scripts/MatchmakingUIController.cs` — render seat/network id next to the name, gated behind a debug toggle.

## Change A — `QuickMatchService.cs`

### A1. Extend `LobbyPlayerInfo` (add SeatIndex)
```csharp
public struct LobbyPlayerInfo
{
    public string Id;
    public int SeatIndex;
    public string PlayerName;
    public int AvatarIndex;
    public bool IsLocal;
}
```

### A2. Assign SeatIndex after the host-first sort
After the existing `list.Sort(...)` block and before `return list;`, stamp the index:
```csharp
for (int i = 0; i < list.Count; i++)
{
    var current = list[i];
    current.SeatIndex = i;
    list[i] = current;
}
```
This makes `SeatIndex` reflect the actual rendered order (host = 0).

## Change B — `MatchmakingUIController.cs`

### B1. Add a debug toggle field (default ON for testing, easy to flip OFF for production)
```csharp
[Header("Debug")]
[SerializeField] private bool showDebugInfo = true;
```

### B2. Update `RenderSlots()` fill loop
Change the `SetProfile` call to include seat + id when debugging:
```csharp
string display = players[i].PlayerName;

if (showDebugInfo)
{
    string shortId = players[i].Id.Length > 6
        ? players[i].Id.Substring(0, 6)
        : players[i].Id;

    display += $"\n[Seat {players[i].SeatIndex} | id:{shortId}]";
}

playerSlots[i].SetProfile(display, avatarSprite);
```
`PlayerProfileUI.SetProfile` already writes the full string to `nameText`, so no change needed there. The `\n` renders on two lines in the TMP slot.

## Why this is safe / non-invasive
- **Offline mode**: `RenderSlots()` early-returns when `QuickMatchService.Instance` is null; offline flow never calls it.
- **Friends Lobby**: uses `LobbyManager` + its own `RenderLobby`, completely separate; not modified.
- **Gameplay / Netcode seat mapping** (`PlayerSeatMapper`, `NetworkGameManager`): untouched. This display is informational only.
- **MVC**: `QuickMatchService` = model/service supplies data; `MatchmakingUIController` = view consumes it. No logic placed in the view.
- To disable for production builds: set `showDebugInfo = false` in the inspector (no code edit).

## Verification steps
1. Host (Alex) opens Quick Match → panel shows:
   - Slot 0: `Alex\n[Seat 0 | id:<hostPrefix>]`
2. Bob joins → on Bob's screen:
   - Slot 0: `Alex\n[Seat 0 | id:<hostPrefix>]` (host stays at Seat 0 on every screen)
   - Slot 1: `Bob\n[Seat 1 | id:<bobPrefix>]`
3. Fill to 4 players → all clients show identical Seat 0..3 ordering with matching names + id prefixes.
4. Toggle `showDebugInfo` off → panel reverts to plain names only (clean production look).
