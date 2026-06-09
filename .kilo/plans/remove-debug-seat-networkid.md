# Remove Debug `seat_id` / `network_id` from Player Display

## Goal
Remove all temporary debug fields and UI text (`SeatId`, `NetworkId`, `NetworkClientId`, `[seat:X net:Y]` suffixes) so player names display cleanly — just `"Alex"`, not `"Alex [seat:0 net:1]"`. Keep console Debug.Log useful but remove all UI-visible debug text.

---

## What Exists Today (to remove)

| File | Lines | Debug Artifact |
|------|-------|---------------|
| `Player.cs` | 12-14 | `public int? SeatId`, `public string NetworkId`, `public ulong NetworkClientId` |
| `UIPlayer.cs` | 48-53 | `[seat:X net:Y]` appended to `nameLabel.text` |
| `GameManager.cs` | 67-76 | `Dbg()` helper reading `SeatId`/`NetworkId` |
| `GameManager.cs` | 1961-1963 | `p.SeatId = ...`, `p.NetworkId = ...`, `p.NetworkClientId = ...` |
| `NetworkPlayer.cs` | 17-21 | Debug log `[Debug] Player spawned — seat_id:X network_id:Y` |
| `LobbyManager.cs` | 665-673 | `shortId` + `debugName` constructing `"Name [seat:X net:ABCD1234]"` |
| `PlayerProfileUI.cs` | 9, 18-23 | `public TextMeshProUGUI debugText` field + `SetDebugInfo()` method |

## What is NOT Touched
- `playerIdToClientId` dictionary — the real network routing map
- `FindPlayerByNetworkId()` / `FindUIPlayerByNetworkId()` — use the dictionary, not debug fields
- `PlayerSeatMapper` — functional seat mapping logic
- All offline mode code (`SetupGame`, `ResolveAsk`, etc.)
- Any RPC / network protocol logic
- Any game logic in `NetworkGameManager`, `NetworkDeckManager`, etc.

---

## Changes (7 edits across 6 files)

### 1. `Assets/Scripts/core/Player.cs` — Remove 3 debug fields

**Remove lines 12-14:**
```csharp
// DELETE:
public int? SeatId;
public string NetworkId;
public ulong NetworkClientId { get; set; } = ulong.MaxValue;
```

`PlayerID` is the existing identifier. `playerIdToClientId` in `GameManager` handles all network routing. Nothing reads these fields for game logic.

Also remove the `Data` property on line 19 if it was debug-only — but it's not referenced elsewhere, so leave it (dead code, not debug-specific).

### 2. `Assets/Scripts/UIPlayer.cs` — Show clean name only

**Replace lines 47-54** in `Initialize()`:

From:
```csharp
playerInstance = player;
//HUMAN PLAYER USES PROFILE DATA
string displayName = player.PlayerName;
if (player.SeatId.HasValue && !string.IsNullOrEmpty(player.NetworkId))
{
    displayName += $" [seat:{player.SeatId.Value} net:{player.NetworkId}]";
}

nameLabel.text = displayName;
```

To:
```csharp
playerInstance = player;
//HUMAN PLAYER USES PROFILE DATA
nameLabel.text = player.PlayerName;
```

### 3. `Assets/Scripts/GameManager.cs` — Simplify `Dbg()` helper

**Replace lines 67-76:**

From:
```csharp
string Dbg(Player p)
{
    if (p == null) return "[null]";

    string seat = p.SeatId.HasValue
            ? p.SeatId.Value.ToString() : "-";
    string net = string.IsNullOrEmpty(p.NetworkId)
            ? "-" : p.NetworkId;
    return $"{p.PlayerName}[seat:{seat} net:{net}]";
}
```

To:
```csharp
string Dbg(Player p)
{
    if (p == null) return "[null]";
    return $"{p.PlayerName}[id:{p.PlayerID}]";
}
```

Keeps console logging useful with `PlayerName[id:X]` format — no `SeatId`/`NetworkId` needed.

### 4. `Assets/Scripts/GameManager.cs` — Remove debug field assignments in `InitializeMultiplayer()`

**Remove lines 1961-1963:**

From:
```csharp
p.SeatId = seatIndex;
p.NetworkId = netPlayer.OwnerClientId.ToString();
p.NetworkClientId = netPlayer.OwnerClientId;
```

The `playerIdToClientId[seatIndex] = netPlayer.OwnerClientId` on line 1970 is the real mapping — that stays untouched.

### 5. `Assets/Scripts/NetworkPlayer.cs` — Remove debug spawn logging

**Remove lines 17-21:**

From:
```csharp
if (PlayerSeatMapper.Instance != null)
{
    int seat =PlayerSeatMapper.Instance.GetSeatIndex(OwnerClientId);
    Debug.Log( $"[Debug] Player spawned — seat_id:{seat} network_id:{OwnerClientId}" );
}
```

The main `Debug.Log` on line 16 already logs `OwnerClientId` — that's sufficient for debugging.

### 6. `Assets/Scripts/LobbyManager.cs` — Show clean name in lobby slots

**Replace lines 664-673** in `UpdatePlayerSlotsUI()`:

From:
```csharp
// APPLY UI
string shortId =
    player.Id.Substring(
        0,
        System.Math.Min(player.Id.Length, 8)
    );

string debugName =
    $"{name} [seat:{i} net:{shortId}]";
playerSlots[i].SetProfile(debugName, avatarSprite);
```

To:
```csharp
// APPLY UI
playerSlots[i].SetProfile(name, avatarSprite);
```

Lobby slots will now show just `"Alex"` instead of `"Alex [seat:0 net:ABCD1234]"`.

### 7. `Assets/Scripts/PlayerProfileUI.cs` — Remove debug field and method

**Replace the entire file** to remove `debugText` and `SetDebugInfo()`:

From:
```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfileUI : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI debugText;

    public void SetProfile(string playerName, Sprite avatar)
    {
        if (nameText != null)
            nameText.text = playerName;
        if (avatar != null && avatarImage != null)
            avatarImage.sprite = avatar;
    }
    public void SetDebugInfo(string seatId, string networkId)
    {
        if (debugText == null)
            return;
        debugText.text = $"seat:{seatId} net:{networkId}";
    }
}
```

To:
```csharp
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfileUI : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;

    public void SetProfile(string playerName, Sprite avatar)
    {
        if (nameText != null)
            nameText.text = playerName;
        if (avatar != null && avatarImage != null)
            avatarImage.sprite = avatar;
    }
}
```

**Note:** The `debugText` field being removed from the class means if a `TextMeshProUGUI` was assigned to it in a Unity scene prefab, Unity will show a "missing reference" warning in Inspector but it won't cause any runtime error. The field should be unassigned in the prefab Inspector before or after this change.

---

## Impact Summary

| Area | Impact |
|------|--------|
| Offline gameplay | Zero — `SeatId`/`NetworkId` never set in `SetupGame()` |
| Lobby display | Clean — just player name, no debug suffix |
| Multiplayer gameplay | Zero — network routing uses `playerIdToClientId` dictionary |
| In-game player labels | Clean — just `"Alex"` not `"Alex [seat:0 net:1]"` |
| Console debug logs | Still useful — `Dbg()` shows `PlayerName[id:X]` |
| MVC structure | Unchanged — no cross-layer violations |

## Execution Order
1. `Player.cs` — remove 3 debug fields (lines 12-14)
2. `GameManager.cs` — simplify `Dbg()` (lines 67-76)
3. `GameManager.cs` — remove field assignments in `InitializeMultiplayer()` (lines 1961-1963)
4. `UIPlayer.cs` — remove debug name suffix (lines 47-54)
5. `LobbyManager.cs` — remove debug name construction (lines 664-673)
6. `PlayerProfileUI.cs` — remove `debugText` field and `SetDebugInfo()` method
7. `NetworkPlayer.cs` — remove debug spawn logging (lines 17-21)

## Risk
- **Zero risk to offline gameplay** — all offline code paths are untouched
- **Zero risk to network routing** — `playerIdToClientId` dictionary is untouched
- **Zero risk to lobby protocol** — no lobby service data changes
- **Easy to verify** — launch offline game, check names are clean; launch multiplayer, check names are clean and gameplay works
