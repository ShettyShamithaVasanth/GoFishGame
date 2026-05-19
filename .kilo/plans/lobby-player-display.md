# Plan: Fix Both Players Seeing Each Other's Name/Avatar in Lobby

## Root Cause
`UpdatePlayerAsync()` updates the **server-side** lobby but does NOT update the local `currentLobby` object. `UpdatePlayerSlotsUI()` reads from the stale local object, so:
- Host's `currentLobby.Players[0].Data` is null → host shows as "Player"
- Client's `currentLobby.Players[1].Data` is null → client shows as "Player"

## Changes (1 file: `LobbyManager.cs`)

### Change 1: Refresh lobby after setting data in `CreateLobby()` (after line 216)

```csharp
// BEFORE (lines 216-253)
        });
        Debug.Log("Lobby Created: " + currentLobby.Id);

        //Create Relay
        string joinCode = await CreateRelay();
        ...
        lobbyPanel.SetActive(true);

        if (playerSlots != null && playerSlots.Length > 0)
        {
            ...
            playerSlots[0].SetProfile(hostName, hostAvatar);
            UpdatePlayerSlotsUI();
        }

        roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;

// AFTER
        });
        Debug.Log("Lobby Created: " + currentLobby.Id);

        //Refresh local lobby to get our own updated data
        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        //Create Relay
        string joinCode = await CreateRelay();
        ...
        lobbyPanel.SetActive(true);

        UpdatePlayerSlotsUI();

        roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;
```

Key changes:
- Added `GetLobbyAsync` after `UpdatePlayerAsync` to refresh `currentLobby` with the host's name/avatar data
- Removed the manual `playerSlots[0].SetProfile()` block (lines 242-254) — `UpdatePlayerSlotsUI()` now handles it from fresh data

### Change 2: Refresh lobby after setting data in `JoinLobby()` (after line 332)

```csharp
// BEFORE (lines 332-341)
        });

        Debug.Log("Joined Lobby");
        lobbyPanel?.SetActive(true);
        menuBackground?.SetActive(false);
        ...
        UpdatePlayerSlotsUI();
        await JoinRelay();

// AFTER
        });

        //Refresh local lobby to get our own updated data
        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        Debug.Log("Joined Lobby");
        lobbyPanel?.SetActive(true);
        menuBackground?.SetActive(false);
        ...
        UpdatePlayerSlotsUI();
        await JoinRelay();
```

## How It Works After Fix

| Step | Host Lobby Shows | Client Lobby Shows |
|------|-----------------|-------------------|
| Host creates room | Slot 0: Host name+avatar ✓, Slots 1-3: "Waiting..." | — |
| Client joins room | Polling (2s) → Slot 1: Client name+avatar ✓ | Slot 0: Host name+avatar ✓, Slot 1: Client name+avatar ✓ |
| 2nd client joins | Polling → Slot 2: Player2 name+avatar ✓ | Same via polling |

**Why the refresh works:**
- `GetLobbyAsync` fetches the latest lobby state from the server
- After `UpdatePlayerAsync`, the server has the player's name/avatar
- `GetLobbyAsync` returns the updated `Players` array with `Data` containing "name" and "avatar" keys
- `UpdatePlayerSlotsUI()` reads from this fresh data → all players show correctly

## Offline Impact: NONE
Only `CreateLobby()` and `JoinLobby()` are modified — online-only methods.
