# Plan: Fix Quit Going to Matchmaking Instead of Main Menu

## Root Cause

`GameModeManager.isOnlineMode` is a `static bool` that persists across scene reloads. When the user quits:

1. `ConfirmQuit()` reloads the scene via `SceneManager.LoadScene`
2. `MatchmakingStarter.Start()` runs on scene load
3. It checks `isOnlineMode` → still `true` (static, not reset)
4. Calls `MatchmakingUIController.Instance.StartSearching()` → matchmaking panel appears

Neither `ConfirmQuitManager.ConfirmQuit()` nor `PauseManager.QuitGame()` reset the static flag.

## Changes (2 files)

### File 1: `ConfirmQuitManager.cs` — `ConfirmQuit()` (line 43-50)

```csharp
// BEFORE
public void ConfirmQuit()
{
    Time.timeScale = 1f;
    GameOverUI.skipMenuOnReload = false;
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}

// AFTER
public void ConfirmQuit()
{
    Time.timeScale = 1f;
    GameModeManager.isOnlineMode = false;
    GameOverUI.skipMenuOnReload = false;

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        NetworkManager.Singleton.Shutdown();

    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
```

### File 2: `PauseManager.cs` — `QuitGame()` (line 20-27)

```csharp
// BEFORE
public void QuitGame()
{
    Time.timeScale = 1f;
    GameOverUI.skipMenuOnReload = false;
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}

// AFTER
public void QuitGame()
{
    Time.timeScale = 1f;
    GameModeManager.isOnlineMode = false;
    GameOverUI.skipMenuOnReload = false;

    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        NetworkManager.Singleton.Shutdown();

    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
```

## Why This Fixes It

| Step | Before | After |
|------|--------|-------|
| Quit → scene reloads | `isOnlineMode = true` (static persists) | `isOnlineMode = false` (reset before reload) |
| `MatchmakingStarter.Start()` | Shows matchmaking panel | Returns immediately (line 11-14) |
| `MenuController.Start()` | Shows wrong state | Shows main menu (default scene state) |
| NetworkManager | Still running, may interfere | Properly shut down |

## Offline Impact: NONE
- `GameModeManager.isOnlineMode = false` is the default value — no effect on offline flow
- `NetworkManager.Singleton.IsListening` check ensures shutdown only runs when network is active
- Both methods already reload the scene; only the cleanup before reload is added
