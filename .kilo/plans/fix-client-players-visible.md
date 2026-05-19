# Fix: Client-Side Players Not Visible

## Root Cause Analysis

The offline flow in `MenuController.ContinueGame()` does THREE things that the multiplayer flow never does:

1. **`GameManager.SetActive(true)`** — activates the GameManager GameObject
2. **`BottomPlayer.SetActive(true)` / `TopPlayer.SetActive(true)`** — activates position GameObjects
3. **`DeckPosition.SetActive(true)`** — activates deck position

The multiplayer flow relies on `ActivatePlayerPosition()` in `NetworkGameManager`, which uses `FindAnyObjectByType<MenuController>()`. If this returns null (e.g., MenuController became inactive after `HideAllLobbyPanels` hid its parent), NO positions are activated.

The `InitializeMultiplayer()` method sets `uiPlayers[i].gameObject.SetActive(true)`, but this only activates the UIPlayer component's own GameObject — NOT its parent (BottomPlayer/TopPlayer). If UIPlayer is a child of an inactive parent, it remains invisible.

## Fix Strategy

Make `InitializeMultiplayer()` self-sufficient — it should activate everything it needs directly, without depending on `MenuController` or `ActivatePlayerPosition`.

---

## CHANGE 1: GameManager.cs — InitializeMultiplayer (replace lines 1786-1820)

### OLD (lines 1786-1820):
```csharp
        for (int i = 0; i < uiPlayers.Length; i++)
        {
            if (uiPlayers[i] != null)
            {
                uiPlayers[i].gameObject.SetActive(i < netPlayers.Count);
            }
        }

        activePlayers = new int[NetworkPlayerManager.Instance.players.Count];

        for (int i = 0; i < activePlayers.Length; i++)
        {
            activePlayers[i] = i;
        }
        // deck = new Deck(this);

        // for (int i = 0; i < netPlayers.Count; i++)
        // {
        //     StartCoroutine(SyncHands(netPlayers));
        // }
        // StartCoroutine(SyncHands(netPlayers));
        //Refresh all hands visually
        RefreshAllHands();

        if (cardBackPrefab != null && deckPosition != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                CreateDeckVisual(); // host already has deck data
            }
            else
            {
                StartCoroutine(WaitForDeckThenCreate()); // client waits for sync
            }
        }
```

### NEW:
```csharp
        // Activate only needed UIPlayers and their parent GameObjects
        for (int i = 0; i < uiPlayers.Length; i++)
        {
            if (uiPlayers[i] != null)
            {
                bool shouldBeActive = i < netPlayers.Count;
                if (shouldBeActive)
                {
                    uiPlayers[i].gameObject.SetActive(true);
                    Transform parent = uiPlayers[i].transform.parent;
                    if (parent != null)
                        parent.gameObject.SetActive(true);
                }
                else
                {
                    uiPlayers[i].gameObject.SetActive(false);
                }
            }
        }

        activePlayers = new int[NetworkPlayerManager.Instance.players.Count];

        for (int i = 0; i < activePlayers.Length; i++)
        {
            activePlayers[i] = i;
        }

        // Activate deck position
        if (deckPosition != null)
            deckPosition.gameObject.SetActive(true);

        // Ensure game scene UI is shown
        GameSceneUI gsUI = FindAnyObjectByType<GameSceneUI>();
        if (gsUI != null)
            gsUI.ShowPanel();

        RefreshAllHands();

        if (cardBackPrefab != null && deckPosition != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                CreateDeckVisual();
            }
            else
            {
                StartCoroutine(WaitForDeckThenCreate());
            }
        }
```

---

## CHANGE 2: NetworkGameManager.cs — WaitForPlayersThenInit (line 192)

Remove the `ActivatePlayerPosition` call since `InitializeMultiplayer` now handles it internally.

### OLD (line 192):
```csharp
        ActivatePlayerPosition(NetworkPlayerManager.Instance.players.Count);
```

### NEW:
```csharp
        // Player positions are now activated inside InitializeMultiplayer
```

---

## Summary

| # | File | Change |
|---|------|--------|
| 1 | GameManager.cs | `InitializeMultiplayer` activates UIPlayers + parents + deckPosition + GameSceneUI |
| 2 | NetworkGameManager.cs | Remove redundant `ActivatePlayerPosition` call |

This makes `InitializeMultiplayer` self-contained — it activates everything it needs, regardless of what `MenuController` or `NetworkGameManager` did or didn't do.
