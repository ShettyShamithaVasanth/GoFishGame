# Plan — Fix MenuBackground staying visible when ModeSelectionPanel opens after Play-Online timeout

## 0. Status of previous round (read this first)

What you applied from the previous plan:
- ✅ Added `MenuController.ShowModeSelection(string mode)` (lines 89–112 of `MenuController.cs`).
- ✅ Refactored `ShowModeSelectionAfterLoading` coroutine to call it.
- ✅ `OfflineFallback.Enter()` now calls `menu.ShowModeSelection("Offline")`.

What you **did NOT apply** — and these are the two pieces that actually fix the bug:
- ❌ `OfflineFallback.Enter()` still calls `Object.FindAnyObjectByType<MenuController>()` on line 12. **This is the live bug.** It is non-deterministic, which is exactly why you see the issue "sometimes" — sometimes Unity returns the correct controller, sometimes the broken duplicate.
- ❌ The duplicate `MenuController` component is still attached to the `MenuBackground` GameObject in `GameScene.unity` (verified: `Assembly-CSharp::MenuController` appears at lines 8215 AND 20500 in the YAML).
- ❌ The `LobbyManager.menuBackground` defense-in-depth disable was not added.

So the MVC refactor alone was not enough. The two changes in §3 below are required.

## 1. Root cause (what is actually wrong)

You said "the MenuBackground is still enabled when ModeSelectionPanel appears after the Play Online timer runs out." I traced this end-to-end. The bug is **not** in `MatchmakingUIController`, `QuickMatchService`, the timer, or the new `ShowModeSelection` method — those all work. The bug is in `OfflineFallback.Enter()` choosing the wrong `MenuController` instance, plus a scene-level duplicate component.

### Evidence

Inside `Assets/Scenes/GameScene.unity` there are **two `MenuController` MonoBehaviours** (verified just now — both still present):

| fileID | GameObject | MenuUI | LoadingPanel | ModeSelectionPanel | MenuBackground |
|--------|------------|--------|--------------|--------------------|----------------|
| `656783933` (YAML line ~8215) | the proper Menu UI root | ✅ wired | ✅ wired | ✅ wired | ✅ wired |
| `1473132316` (YAML line ~20500) | **the `MenuBackground` GameObject itself** | ❌ null | ❌ null | ✅ wired | ❌ **null** |

A stray second `MenuController` component is sitting on the `MenuBackground` GameObject, but its `MenuBackground` field was never assigned.

Additional finding (this is what makes the bug *visible* even when only briefly): the `ModeSelectionPanel` GameObject itself has an `Image` component using the **same sprite** as `MenuBackground` at alpha ≈ 0.43 (line ~27883: `m_Color: {r: 1, g: 1, b: 1, a: 0.43137255}`, same sprite guid `cec7a4edc7e06574aae665c359706c91`). So when both are on screen you literally see the bright full-alpha `MenuBackground` *behind* the dimmed `ModeSelectionPanel` overlay — that is what you are calling "menubackground is still there in the background of mode selection panel."

### Why this breaks the online→offline fallback flow (and why it is "sometimes")

`OfflineFallback.Enter()` currently does:

```csharp
MenuController menu = Object.FindAnyObjectByType<MenuController>();
...
menu.ShowModeSelection("Offline");
```

Inside `ShowModeSelection`:
```csharp
if (MenuBackground != null)
    MenuBackground.SetActive(false);
```

`FindAnyObjectByType` is **non-deterministic** — Unity returns whichever instance happens to be first in the internal scene graph. When it returns the **duplicate** (the one on the `MenuBackground` GameObject, YAML line ~20500), `this.MenuBackground` is `null`, so the null-guard skips the disable call. Meanwhile `ModeSelectionPanel` is wired on both instances, so the panel still opens. Result: panel visible + background still visible behind it — exactly what you see. "Sometimes" = the times Unity returned the broken duplicate.

The offline path (`PlayOffline` → `ShowModeSelectionAfterLoading`) never had this issue because it is an instance method on the **correct** `MenuController` and uses `this.MenuBackground` directly — it never calls `FindAnyObjectByType`.

## 2. Files that will change (and only these)

Only TWO files need code/scene edits. `MenuController.cs` is already correct from the previous round and does **not** need to be touched again.

1. `Assets/Scripts/core/OfflineFallback.cs` — replace the single `FindAnyObjectByType` call with a deterministic `FindObjectsByType` + validity filter, and add the `LobbyManager.menuBackground` defense-in-depth disable.
2. `Assets/Scenes/GameScene.unity` — remove the stray duplicate `MenuController` component (fileID `1473132316`) from the `MenuBackground` GameObject. **This is the root-cause fix; the code change above is the defensive backup that makes the bug impossible even if the scene ever regresses.**

No other scripts change. `MenuController.cs`, `MatchmakingUIController`, `QuickMatchService`, `ModeSelectionController`, `LobbyManager`, `NetworkGameManager`, `GameManager`, `MatchMakingStarter` are untouched → offline gameplay, Play-with-Friends lobby, and multiplayer gameplay are not affected.

## 3. Detailed changes

> `MenuController.cs` is **already done** from the previous round — skip to §3.2.

### 3.1 `Assets/Scripts/MenuController.cs`  *(no changes needed this round)*

For reference, the method you already added is exactly what we want:

```csharp
public void ShowModeSelection(string mode)
{
    if (MenuUI != null)            MenuUI.SetActive(false);
    if (LoadingPanel != null)      LoadingPanel.SetActive(false);
    if (MenuBackground != null)    MenuBackground.SetActive(false);

    if (ModeSelectionPanel != null)
    {
        ModeSelectionPanel.SetActive(true);
        var controller = ModeSelectionPanel.GetComponent<ModeSelectionController>();
        if (controller != null) controller.SetHeader(mode);
    }
}
```

Leave it as-is.

### 3.2 `Assets/Scripts/core/OfflineFallback.cs`  *(the actual fix)*

Replace the entire file with this. The two essential changes vs. the current code are (a) `FindObjectsByType` + filter, and (b) the `LobbyManager.menuBackground` safety-net disable:

```csharp
using UnityEngine;

public static class OfflineFallback
{
    public static void Enter()
    {
        GameModeManager.isOnlineMode = false;
        QuickMatchService.Instance?.Cancel();

        MenuController menu = FindPrimaryMenuController();

        if (menu == null)
        {
            Debug.LogError(
                "OfflineFallback: no MenuController with a valid MenuBackground reference found");
            return;
        }

        // Defense-in-depth: LobbyManager.menuBackground references the same
        // visible GameObject. Disabling it here guarantees the background is
        // hidden even if some future scene edit re-introduces a stray
        // MenuController duplicate. This path is ONLY entered on matchmaking
        // timeout, so it never affects Play-with-Friends or offline flows.
        var lobby = Object.FindAnyObjectByType<LobbyManager>();
        if (lobby != null && lobby.menuBackground != null)
            lobby.menuBackground.SetActive(false);

        menu.ShowModeSelection("Offline");
    }

    // Picks the MenuController whose MenuBackground slot is actually wired.
    // This is the determinism fix: FindAnyObjectByType<MenuController>()
    // returns whatever instance is first in the scene graph and can hand back
    // the broken duplicate sitting on the MenuBackground GameObject itself.
    static MenuController FindPrimaryMenuController()
    {
        var all = Object.FindObjectsByType<MenuController>(
                      FindObjectsSortMode.None);

        if (all == null || all.Length == 0)
            return null;

        foreach (var m in all)
        {
            if (m != null && m.MenuBackground != null)
                return m;
        }

        // Fallback: return the first one even if its slot is unset,
        // so we at least still get the ModeSelectionPanel up.
        return all[0];
    }
}
```

Why this is the minimum sufficient change:
- Replaces one non-deterministic call with a deterministic filter → "sometimes" goes away.
- The `LobbyManager.menuBackground` line is a one-liner safety net that costs nothing and is gated on the timeout-only call path (so it cannot fire during friends-lobby or offline flows).
- Nothing else in the codebase is touched.

### 3.3 `Assets/Scenes/GameScene.unity` — remove the stray duplicate component (root-cause cleanup)

Do this in the Unity Editor (do not hand-edit the YAML):

1. Open `GameScene`.
2. In Hierarchy, select the **`MenuBackground`** GameObject (the one whose Inspector shows an `Image` component with the menu sprite and *also* a `MenuController` component whose `MenuBackground` field says `None`).
3. In Inspector, on that `MenuController` component, click the ⋮ icon → **Remove Component**.
4. Save the scene.

After this, only one `MenuController` remains in the scene (the proper one on the Menu UI root), and `FindAnyObjectByType<MenuController>()` could no longer hand back a broken duplicate even if we still used it. The §3.2 code change is kept as defense-in-depth.

> If you would rather not touch the scene right now, §3.2 alone is sufficient to fix the visible bug. But removing the stray component is the root-cause fix and is recommended for a "professional" codebase — otherwise the dead component continues to run `Start()` once per scene load and silently calls `ModeSelectionPanel.SetActive(false)` on the shared ModeSelectionPanel reference, which is itself a latent bug.

## 4. Why this is MVC-clean and professional

- **Single responsibility**: `MenuController` owns all transitions of its own UI (`MenuUI`, `LoadingPanel`, `MenuBackground`, `ModeSelectionPanel`). The only outside reach-in is the documented `LobbyManager.menuBackground` defense-in-depth line, which is gated to the timeout-only path.
- **Single source of truth**: Both the offline path (`ShowModeSelectionAfterLoading`) and the online-fallback path (`OfflineFallback.Enter`) now go through `MenuController.ShowModeSelection(string)`. No duplicated "hide these three panels and show that one" logic.
- **No hardcoding**: no hardcoded GameObject names, no `Transform.Find` calls, no magic strings for panel names. Only the `"Offline"` header label is passed in, which is data, not a hardcoded reference.
- **Deterministic lookup**: replacing `FindAnyObjectByType` with `FindObjectsByType` + a validity filter makes the result deterministic instead of relying on scene-graph order. The "sometimes" symptom disappears.
- **Backward compatible**: `PlayOffline`, `ContinueGame`, `PlayWithFriends`, `JoinRoom`, lobby create/join/start, `NetworkGameManager` flow, `GameManager.SetupGame`, and `GameOverUI` skip-menu reload are all unmodified.

## 5. Scope of impact (safety checklist)

| Flow | Affected? | Why |
|------|-----------|-----|
| Offline gameplay (Play Offline → loading → mode select → Continue Game) | **No behavior change.** | `ShowModeSelectionAfterLoading` now delegates to `ShowModeSelection`, which performs the exact same SetActive calls in the exact same order. Verified by reading both methods. |
| Play Online → match found → multiplayer game | **No change.** | `QuickMatchService.OnMatchFound` path is untouched. `NetworkGameManager` is untouched. |
| Play Online → timer out → fallback to offline mode select | **Fixed.** | This is the bug. |
| Play with Friends → Create/Join lobby | **No change.** | `LobbyManager.CreateLobby` / `JoinLobby` / `CloseLobby` / `StartGame` are untouched. The only addition is the safety-net `lobby.menuBackground.SetActive(false)` in `OfflineFallback.Enter`, which is **only** called from the matchmaking timeout handler — never from friends-lobby code. |
| Play with Friends → gameplay | **No change.** | `NetworkGameManager` and `NetworkPlayer*` scripts are untouched. |
| Game Over → reload → skip menu | **No change.** | `GameOverUI.skipMenuOnReload` branch in `MenuController.Start` is untouched. |
| Cancel matchmaking (back button) | **No change.** | `MatchmakingUIController.OnCancelClicked` is untouched; it does not call `OfflineFallback`. |

## 6. Validation steps after applying

1. Open `GameScene` and confirm only **one** `MenuController` component exists in the scene (on the proper Menu UI root), and the `MenuBackground` GameObject has only `RectTransform`, `CanvasRenderer`, and `Image`.
2. Play → Play Offline → loading → ModeSelectionPanel appears → MenuBackground hidden. (regression check)
3. Play → Play Online → wait 20 s → ModeSelectionPanel appears → **MenuBackground hidden. Repeat this 5–10 times** to confirm the "sometimes" is gone — that is the whole point of the determinism fix. (bug fix check)
4. Play → Play Offline → pick 2/3/4 players → Continue → game starts normally. (regression check)
5. Play → Play With Friends → create room / join by code → lobby works → start game (host) → multiplayer gameplay works. (regression check)
6. After online timeout fallback, pick a player count and Continue → offline game starts normally. (continuation of fixed path)

## 7. Out of scope (intentionally not touched)

- `MatchmakingUIController.HandleNoPlayersFound` delay / wording
- `QuickMatchService` timer value or event plumbing
- `ModeSelectionController` coin slider / button colors
- `LobbyManager` relay/lobby logic
- `NetworkGameManager` initialization sequence
- The duplicate component's origin (likely an accidental "Add Component" in the Editor at some point; not a code defect)
