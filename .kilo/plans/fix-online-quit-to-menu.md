# Fix Plan: Online Quit Should Go to Menu Background, Not Matchmaking

## Problem

In online multiplayer (Play with Friends), when the player clicks:
**Pause → Quit → Confirm Quit** → the scene reloads via `SceneManager.LoadScene()`.

On reload, `GameModeManager.isOnlineMode` is still `true` (it's static), and network components reinitialize, causing the matchmaking/friends panel to appear instead of the menu background panel.

## Root Cause

`ConfirmQuitManager.ConfirmQuit()` and `PauseManager.QuitGame()` both use `SceneManager.LoadScene()` unconditionally — they don't differentiate between online and offline mode. In online mode, the scene reload doesn't properly clean up:
- Network connection (still active via DontDestroyOnLoad)
- Lobby membership (still joined)
- `GameModeManager.isOnlineMode` (static, persists across reload)
- Game scene UI panels (recreated but not properly shown/hidden)

## Fix 1 — Add `QuitFromGame()` to LobbyManager.cs

### File: `Assets/Scripts/LobbyManager.cs`
### Location: After `CloseLobby()` method (around line 478)

Add a new public method:

```csharp
public void QuitFromGame()
{
    Debug.Log("Quitting online game to menu...");

    // 1. Leave the lobby service
    if (currentLobby != null)
    {
        StartCoroutine(LeaveLobbyCoroutine());
    }

    // 2. Hide all game-related panels
    if (enteringGamePanel != null)
        enteringGamePanel.SetActive(false);
    if (lobbyPanel != null)
        lobbyPanel.SetActive(false);
    if (creatingRoomPanel != null)
        creatingRoomPanel.SetActive(false);
    if (friendsPanel != null)
        friendsPanel.SetActive(false);
    if (matchmakingPanel != null)
        matchmakingPanel.SetActive(false);
    if (modeSelectionPanel != null)
        modeSelectionPanel.SetActive(false);

    // 3. Hide game scene UI
    GameSceneUI gameSceneUI = FindAnyObjectByType<GameSceneUI>();
    if (gameSceneUI != null && gameSceneUI.gameScenePanel != null)
        gameSceneUI.gameScenePanel.SetActive(false);

    // 4. Shutdown network
    if (NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsListening)
    {
        NetworkManager.Singleton.Shutdown();
        Debug.Log("Network Stopped.");
    }

    // 5. Reset online mode flag
    GameModeManager.isOnlineMode = false;

    // 6. Show menu background
    if (menuBackground != null)
        menuBackground.SetActive(true);

    // 7. Show main menu UI
    MenuController menu = FindAnyObjectByType<MenuController>();
    if (menu != null && menu.MenuUI != null)
        menu.MenuUI.SetActive(true);
}
```

## Fix 2 — Modify `ConfirmQuitManager.ConfirmQuit()`

### File: `Assets/Scripts/ConfirmQuitManager.cs`
### Location: `ConfirmQuit()` method, lines 43-50

BEFORE:
```csharp
public void ConfirmQuit()
{
    Time.timeScale = 1f;

    GameOverUI.skipMenuOnReload = false;

    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
```

AFTER:
```csharp
public void ConfirmQuit()
{
    Time.timeScale = 1f;

    if (GameModeManager.isOnlineMode)
    {
        confirmPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.QuitFromGame();
        }
    }
    else
    {
        GameOverUI.skipMenuOnReload = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
```

## Fix 3 — Modify `PauseManager.QuitGame()`

### File: `Assets/Scripts/PauseManager.cs`
### Location: `QuitGame()` method, lines 20-27

BEFORE:
```csharp
public void QuitGame()
{
    Time.timeScale = 1f;

    GameOverUI.skipMenuOnReload = false;

    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
```

AFTER:
```csharp
public void QuitGame()
{
    Time.timeScale = 1f;

    if (GameModeManager.isOnlineMode)
    {
        pausePanel.SetActive(false);

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.QuitFromGame();
        }
    }
    else
    {
        GameOverUI.skipMenuOnReload = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
```

## Summary

| File | Change |
|------|--------|
| `LobbyManager.cs` | Add `QuitFromGame()` method (new) |
| `ConfirmQuitManager.cs` | Add online mode check in `ConfirmQuit()` |
| `PauseManager.cs` | Add online mode check in `QuitGame()` |

## Impact

| Area | Affected? |
|------|-----------|
| Offline quit flow | No change (still reloads scene) |
| Online quit flow | **Fixed** — goes to menu background |
| Lobby/Friends | No change |
| MVC structure | No change (LobbyManager handles network+UI cleanup) |
| Host vs Client | Same behavior for both (no mixture) |

## Logic

- **Offline**: Scene reload (original behavior) — works perfectly, no network to clean up
- **Online**: Clean shutdown — leave lobby, stop network, reset flag, hide all game panels, show menu
