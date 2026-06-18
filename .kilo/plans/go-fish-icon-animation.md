# Plan: Animate the Go Fish Icon on Friends & Lobby Panels

## Goal
Add a subtle, professional "gentle float + sway" idle animation to the **Go Fish icon** that lives on the **Friends panel** and the **Lobby panel**. The animation must play automatically **the moment either panel appears** (becomes active) and stop cleanly when the panel is hidden — with **no hardcoding** of positions/timings and **no changes** to `MenuController` / `LobbyManager` logic.

## Approach (why this is optimal & professional)
- The project already has an established DOTween pattern: a self-contained `MonoBehaviour` that auto-starts in `OnEnable()`, kills tweens in `OnDisable()` via `DOTween.Kill(this)`, and uses serialized `[SerializeField]` fields (no hardcoding). See `EditProfileAnimationController.cs` and `MenuAnimationController.cs`.
- Because the Friends panel and Lobby panel are shown/hidden with `SetActive(true/false)`, Unity fires `OnEnable` every time the panel becomes active. So a single reusable component attached to the icon (or panel) will **trigger automatically** whenever the panel appears in gameplay — zero wiring in the gameplay managers.
- One reusable script handles **both** panels (DRY). Each instance is configured independently in the Inspector, so timings/amplitudes are tuned per panel without code changes.

---

## Step 1 — Create the reusable script
**New file:** `Assets/Scripts/GoFishIconAnimator.cs`

This mirrors `EditProfileAnimationController.cs` exactly in structure (namespaces, `OnEnable`/`OnDisable`, `Sequence`, `SetTarget(this)`, `SetLoops`).

```csharp
using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class GoFishIconAnimator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Go Fish icon RectTransform to animate. Leave empty to auto-use this GameObject's RectTransform.")]
    [SerializeField] private RectTransform icon;

    [Header("Float (vertical bob)")]
    [SerializeField] private float floatDistance = 8f;   // pixels
    [SerializeField] private float floatDuration = 1.6f; // seconds each way

    [Header("Sway (rotation)")]
    [SerializeField] private float swayAngle = 6f;       // degrees
    [SerializeField] private float swayDuration = 1.6f;  // seconds each way

    [Header("Breathing scale (subtle)")]
    [SerializeField] private float scalePulse = 1.04f;
    [SerializeField] private float scaleDuration = 1.6f;

    private Sequence _sequence;

    private void Reset()
    {
        // Auto-assign in editor so nothing is ever left null.
        icon = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (icon == null)
            icon = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        StartAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private void StartAnimation()
    {
        if (icon == null) return;

        StopAnimation(); // safety: never stack tweens

        Vector2 basePos = icon.anchoredPosition;
        Vector3 baseScale = icon.localScale;
        // rotation is animated around 0 (sway), pivot assumed centered

        _sequence = DOTween.Sequence().SetTarget(this);

        // Vertical float (yoyo so it bobs up and down)
        _sequence.Join(
            icon.DOAnchorPosY(basePos.y + floatDistance, floatDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

        // Sway left/right (yoyo)
        _sequence.Join(
            icon.DORotate(new Vector3(0, 0, swayAngle), swayDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

        // Gentle breathing scale (yoyo)
        _sequence.Join(
            icon.DOScale(baseScale * scalePulse, scaleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));
    }

    private void StopAnimation()
    {
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }
        DOTween.Kill(this); // kill any tween tagged to this component
    }
}
```

### Notes / why each piece exists
- `[DisallowMultipleComponent]` — prevents accidental duplicates on one object.
- `Reset()` + `Awake()` fallback — guarantees `icon` is never null; zero hardcoding of references.
- All amplitudes/timings are `[SerializeField]` fields with sensible defaults → tunable in Inspector per panel.
- `OnEnable`/`OnDisable` lifecycle — **this is the key**: it makes the icon animate exactly when the panel becomes active (via `SetActive(true)` in `MenuController.PlayWithFriends()` and `LobbyManager.CreateLobby/JoinLobby`), and stop when hidden.
- `baseScale * scalePulse` (not a hardcoded vector) — respects whatever scale the icon was set to in the prefab.
- `SetTarget(this)` + `DOTween.Kill(this)` — matches the existing controllers exactly, prevents orphaned tweens / memory leaks.
- `StopAnimation()` called before building the sequence — prevents tween stacking if `OnEnable` fires twice.

---

## Step 2 — Unity Hierarchy & Inspector wiring

### 2A. Friends panel
Hierarchy (example, match your actual names):
```
FriendsPanel            (GameObject toggled by MenuController.FriendsPanel)
 └─ GoFishIcon          (Image = Go Fish logo)
```
**Actions:**
1. Select the **GoFishIcon** object (the Image that shows the Go Fish logo on the Friends panel).
2. **Add Component → GoFishIconAnimator** (or add it to `FriendsPanel` root and drag the icon Image into the `Icon` field — either works; attaching to the icon itself is simplest).
3. Leave `Icon` empty (it auto-assigns from the same GameObject) OR drag the icon RectTransform in explicitly.
4. Tune: `Float Distance ~8`, `Sway Angle ~6`, `Scale Pulse ~1.04`, durations ~1.6s. Adjust to taste.

### 2B. Lobby panel
Hierarchy (example):
```
LobbyPanel             (GameObject toggled by LobbyManager.lobbyPanel)
 └─ GoFishIcon         (Image = Go Fish logo)
```
**Actions:** identical to 2A — add `GoFishIconAnimator` to the lobby's Go Fish icon and tune the values (you may want slightly smaller amplitude here).

> The exact object names come from your scene. The only requirement: the animated object **must be a child of (or be) the panel that gets `SetActive`'d**, so its `OnEnable` fires when the panel appears.

---

## Step 3 — What to verify / NOT change
- **Do NOT modify** `MenuController.cs`, `LobbyManager.cs`, or `NetworkGameManager.cs`. They already call `SetActive(true/false)` on these panels, which is what drives the animation. Changing them risks breaking the gameplay flow shown in `NetworkGameManager.HideAllLobbyPanels()`.
- **Do NOT add** the animator to the icon on the *main menu* unless you also want it animated there (the menu already has its own `MenuAnimationController`).
- **Remove** any old/placeholder animation component on those icons if one exists (e.g., a stray DOTween Animation component or Animator) to avoid conflicts. If none exists, nothing to remove.
- Ensure the icon's **Pivot is centered (0.5, 0.5)** so rotation sways cleanly around its middle.
- Ensure **DOTween is imported** — it already is (used in `MenuAnimationController.cs`, `EditProfileAnimationController.cs`).

---

## Step 4 — How it will look after the change
- When you tap **Play With Friends** → `FriendsPanel.SetActive(true)` → icon begins a soft continuous bob: it rises ~8px and back, tilts ~6° left/right, and gently scales 1.0↔1.04, all eased with `InOutSine` and looping forever (yoyo). It feels alive but not distracting.
- When you tap **Close** → `FriendsPanel.SetActive(false)` → `OnDisable` fires → all tweens killed instantly, icon snaps back to its base transform (no leftover drift).
- When a lobby is created/joined → `lobbyPanel.SetActive(true)` → same gentle float+sway on the lobby's Go Fish icon.
- When the game starts or lobby closes → panel hidden → animation stops cleanly.
- Performance: 3 lightweight looping tweens per visible icon, killed on hide — negligible overhead, same approach as the existing menu animations.

---

## Step 5 — Verification checklist
- [ ] `GoFishIconAnimator.cs` compiles with no errors (uses existing `DG.Tweening`).
- [ ] Component added to the Friends-panel Go Fish icon; animates when panel opens, stops when closed.
- [ ] Component added to the Lobby-panel Go Fish icon; animates when lobby panel opens, stops when closed.
- [ ] No errors in Console on play/stop.
- [ ] No changes broke `MenuController`/`LobbyManager` gameplay flow (none were modified).
- [ ] Icon pivot centered; values tuned to taste.

## Step 6 — Architecture fit (MVC) & non-regression guarantees

### This codebase's de-facto pattern
The project is **not strict MVC**; it uses self-contained UI presentation components that are decoupled from logic. There are already **6 sibling `*AnimationController` classes** following the identical DOTween/`OnEnable`/`OnDisable` pattern:
`MenuAnimationController`, `EditProfileAnimationController`, `MatchmakingAnimationController`, `StatsPanelAnimationController`, `ConfirmQuitAnimationController`, `GameOverAnimationController`.

`GoFishIconAnimator` is a **pure presentation/View component**. It:
- contains **no business logic** (no network calls, no game state),
- references only its own UI `RectTransform`,
- is driven entirely by Unity's activation lifecycle, not by any Manager.

So it slots cleanly into the existing architecture without coupling to any Controller/Manager.

### Non-regression: why nothing else is affected
1. **Offline gameplay** — Offline mode (`MenuController.PlayOffline` → `ModeSelectionPanel` → `GameManager`) never opens the Friends or Lobby panel. The animator lives on those panels only, so offline flow is **untouched**. Zero script edits to offline paths.
2. **Play With Friends / Lobby** — these panels are already toggled by existing code:
   - Open: `MenuController.PlayWithFriends()` → `FriendsPanel.SetActive(true)`; `LobbyManager.CreateLobby()`/`JoinLobby()` → `lobbyPanel.SetActive(true)`.
   - Close: `LobbyManager.CloseLobby()`, `MenuController.CloseFriendsPanel()`, and **`NetworkGameManager.HideAllLobbyPanels()` (line 153)** all call `SetActive(false)`.
   - Every one of those `SetActive(false)` calls fires `OnDisable` → `StopAnimation()` → tweens killed. The animation is purely additive visual polish; it changes **no** gameplay, network, or lobby state.
3. **Multiplayer gameplay** — `GameManager`, `NetworkGameManager`, `NetworkDeckManager`, `NetworkPlayerManager`, `QuickMatchService` are **not modified at all**. Deal logic, turns, Go Fish, win conditions, relay/lobby services — all unchanged.
4. **No hardcode** — every value (amplitude, duration, angle, scale) is a serialized Inspector field with defaults; positions come from `anchoredPosition`/`localScale` at runtime, never literal constants.

### Net change summary
| File | Change |
|------|--------|
| `Assets/Scripts/GoFishIconAnimator.cs` | **NEW** (reusable, ~90 lines) |
| `MenuController.cs` | none |
| `LobbyManager.cs` | none |
| `NetworkGameManager.cs` | none |
| `GameManager.cs` | none |
| Scene/prefab | Add `GoFishIconAnimator` component to the Go Fish icon under `FriendsPanel` and `LobbyPanel` (Inspector only) |

## Risk / rollback
If anything misbehaves, disable the `GoFishIconAnimator` component in the Inspector — the icon simply returns to static. Since no other scripts were touched, rollback is trivial.
