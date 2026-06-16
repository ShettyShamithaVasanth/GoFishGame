# Plan: Fix Play Online (Quick Match) — 3 Compile Errors + Make It Actually Work

## Status
The source files were **never edited** — they still contain the original errors. This plan is the single, complete, executable fix. It resolves the 3 blocking errors and corrects the runtime flow so Play Online (Quick Match) works as a proper 4-player Go Fish game, with **no hardcoding**, **correct MVC**, and **Offline + Friends untouched**.

---

## The 3 Compile Errors (all in `MatchmakingUIController.cs`)
All three come from `RenderSlots()` referencing things the service doesn't expose:

| # | Location | Cause |
|---|----------|-------|
| 1 | UI `:117` `QuickMatchService.Instance.GetLobbyPlayers()` | Method does not exist |
| 2 | UI `:145` `QuickMatchService.Instance.AvatarDatabase.avatarSprites.Length` | `AvatarDatabase` property typed `object` (QuickMatchService `:231`), no `.avatarSprites` |
| 3 | UI `:150` `QuickMatchService.Instance.AvatarDatabase.avatarSprites[i]` | same |

**Root cause:** `QuickMatchService.cs:231` has a bogus property that shadows the real serialized field:
```csharp
public object AvatarDatabase { get; internal set; }   // DELETE THIS
```

---

## Fix — `Assets/Scripts/QuickMatchService.cs` (Service / Model layer)

### 1) Delete the bogus property (fixes errors 2 & 3)
Remove `public object AvatarDatabase { get; internal set; }` (line 231).
Add a proper typed accessor:
```csharp
public AvatarDatabase AvatarDatabase => avatarDatabase;
```

### 2) No-hardcode avatar wiring (defensive, no inspector dependency)
In `Awake()`, reuse the existing `AvatarDatabase` asset already on `LobbyManager` if not assigned:
```csharp
private void Awake()
{
    Instance = this;
    if (avatarDatabase == null)
        avatarDatabase = FindAnyObjectByType<LobbyManager>()?.avatarDatabase;
}
```

### 3) Add DTO + `GetLobbyPlayers()` (fixes error 1; keeps Lobby API out of the View)
```csharp
public struct LobbyPlayerInfo
{
    public string PlayerName;
    public int AvatarIndex;
    public bool IsLocal;
}

public List<LobbyPlayerInfo> GetLobbyPlayers()
{
    var list = new List<LobbyPlayerInfo>();
    if (currentLobby == null) return list;

    string localId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;

    foreach (var p in currentLobby.Players)
    {
        bool isLocal = (p.Id == localId);
        string name = isLocal
            ? (string.IsNullOrEmpty(ProfileData.PlayerName) ? "Player" : ProfileData.PlayerName)
            : (p.Data != null && p.Data.ContainsKey("name") ? p.Data["name"].Value : "Player");
        int avatarIndex = isLocal
            ? ProfileData.PlayerAvatarIndex
            : (p.Data != null && p.Data.ContainsKey("avatar") && int.TryParse(p.Data["avatar"].Value, out var av) ? av : 0);
        list.Add(new LobbyPlayerInfo { PlayerName = name, AvatarIndex = avatarIndex, IsLocal = isLocal });
    }
    // Local player first (matches slot-0 = YOU convention)
    list.Sort((a, b) => a.IsLocal == b.IsLocal ? 0 : (a.IsLocal ? -1 : 1));
    return list;
}
```

### 4) Fix host create path (runtime: lobby discoverable + relay + host running)
In `FindOrCreateLobby()`, the `else` branch currently uses a bare `CreateLobbyAsync` with no `mode` data and never creates a relay/host. Replace the create branch body with:
```csharp
await CreateQuickMatchLobby();   // public lobby + mode=quickmatch data
isHost = true;
await CreateRelay();             // allocation, relayCode stored in lobby, StartHost()
```
Keep the shared `UpdatePlayerAsync` (name+avatar) after the branch; keep the join branch (`JoinLobbyByIdAsync`, `isHost = false`) unchanged.

### 5) Add `OnLobbyUpdated` event + fire it
- Declare `public System.Action OnLobbyUpdated;`
- At end of successful `RefreshLobby()`: `OnLobbyUpdated?.Invoke();`

### 6) Heartbeat (runtime: lobby not deleted after ~30s)
In the `Update()` poll block, host pings every poll (failures ignored):
```csharp
if (isHost && currentLobby != null)
{
    try { _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id); } catch { }
}
```

### 7) Fix spurious self-join
Add a `private bool firstRefresh;` and in `RefreshLobby()`, on first successful fetch set `oldCount = newCount` before the comparison (so host's own presence doesn't fire a fake `OnPlayerJoined`).

No change to: `CreateRelay`, `JoinRelay`, `CheckMatchReady`, `StartHostFlow` (`InitializeAndDeal` — valid now since host is running), `StartClientFlow`→`JoinRelay` (relayCode exists), `LeaveLobbyAndCleanup`, `Cancel`.

---

## Fix — `Assets/Scripts/MatchmakingUIController.cs` (View layer)
- Subscribe/unsubscribe `QuickMatchService.Instance.OnLobbyUpdated += RenderSlots` in `OnEnable`/`OnDisable` (plus existing events).
- `RenderSlots()` already written — now compiles. Add status text update inside it:
  ```csharp
  if (statusText != null)
      statusText.text = players.Count + "/" + playerSlots.Length + " players found";
  ```
- `HandlePlayerJoined` / `HandlePlayerLeft` → call `RenderSlots()`.
- `HandleMatchFound()` → `StopSearching()` (hide panel). Game-scene activation stays handled by the existing `NetworkGameManager.OnGameStartedChanged → HideAllLobbyPanels → ActivateGameScene` path (already hides `LobbyManager.matchmakingPanel`).
- `HandleTimeout` / `OnCancelClicked` unchanged (offline fallback).

---

## No change to (verified safe)
- **Offline**: `MenuController.PlayOffline → GameManager.SetupGame` — not on QuickMatch path.
- **Friends**: `LobbyManager` code rooms (`CreateLobby`/`JoinLobby`) — QuickMatch found only via `mode=quickmatch` filter; no collision.
- **Gameplay**: `NetworkGameManager`, `GameManager`, `NetworkPlayer`, `NetworkDeckManager` — reused unchanged.

Files changed: `QuickMatchService.cs`, `MatchmakingUIController.cs`.

---

## Verification
- 0 compile errors (typed `AvatarDatabase` + `GetLobbyPlayers()` resolve all 3).
- P1 → Play Online → creates discoverable `mode=quickmatch` lobby + relay + starts host; slot 0 = local name/avatar.
- P2–P4 → Play Online → join same lobby; slots 1–3 fill with real names/avatars; lobby stays alive (heartbeat).
- At 4 → host `InitializeAndDeal()` deals; `isGameStarted` fires → all clients enter online gameplay (unchanged path).
- Timeout <4 → lobby left → offline mode selection (unchanged).
- Offline + Friends behave exactly as before.
