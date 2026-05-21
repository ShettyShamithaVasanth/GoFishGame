# Plan: Matchmaking Panel — Show Players + Auto-Start Game

## Problem
When 2+ players click "Play Online" and enter the matchmaking panel:
1. Other players' avatars and names are NOT visible in the player slots
2. The game doesn't auto-start when >=2 players are found within the 20s timer
3. No actual Unity Lobby is ever created by the matchmaking flow

## Root Cause Analysis

### Current broken flow:
```
Player clicks "Play Online"
  → MenuController.PlayOnline() sets isOnlineMode=true
  → Loads GameScene
  → MatchMakingStarter.Start() runs:
      → Shows matchmaking timer UI
      → Sets local Player_0 profile only
      → isOnlineMode = false  ← BUG: kills online state immediately
      → NEVER creates a lobby
      → NEVER creates a relay
      → NEVER calls LobbyManager at all
  → Timer counts down 20s
  → Timer expires → "No players found" → shows ModeSelectionPanel
  → No multiplayer ever happens
```

### Why players are invisible:
- `MatchMakingStarter` only sets the local player's `PlayerProfileUI` (myProfileUI)
- It never calls `LobbyManager` to create/join a lobby
- Without a lobby, `UpdatePlayerSlotsUI()` never runs
- Without `UpdatePlayerSlotsUI()`, other players' names/avatars are never populated

### The `isOnlineMode = false` bug (line 49):
- `GameModeManager.isOnlineMode` is set to `false` immediately in `Start()`
- This means any subsequent online-mode checks will fail
- It should stay `true` until the game actually starts

## Solution

### API Methods Used (all proven in existing codebase)
| Method | Used In | Status |
|--------|---------|--------|
| `CreateLobbyAsync` | `LobbyManager.CreateLobby()` L212 | ✓ Works |
| `JoinLobbyByCodeAsync` | `LobbyManager.JoinLobby()` L313 | ✓ Works |
| `GetLobbyAsync` | `LobbyManager.RefreshLobby()` L155 | ✓ Works |
| `UpdatePlayerAsync` | `LobbyManager.JoinLobby()` L335 | ✓ Works |
| `UpdateLobbyAsync` | `LobbyManager.CreateLobby()` L240 | ✓ Works |
| `QueryLobbiesAsync` | Standard Unity Lobby SDK API | ✓ Available |
| `RemovePlayerAsync` | `LeaveLobbyCoroutine()` L482 | ✓ Works |
| `DeleteLobbyAsync` | Standard Unity Lobby SDK API | ✓ Available |

### New flow after fix:
```
Player A clicks "Play Online"
  → MatchMakingStarter.Start()
  → Calls LobbyManager.QuickMatchLobby()
  → QueryLobbiesAsync (filter: isMatchmaking=true, availableSlots>0)
  → No matchmaking lobbies found
  → CreateMatchmakingLobby():
      → CreateLobbyAsync (name: "MM_<guid>", maxPlayers: 4)
      → UpdatePlayerAsync (sets name + avatar index)
      → CreateRelay() (creates relay allocation, starts host)
      → UpdateLobbyAsync (stores relayCode + joinCode as public + isMatchmaking=true)
      → Shows lobbyPanel with Player A in Slot 0 (avatar + name visible)
      → Shows "Searching for players..." text
      → Starts 20s timer via MatchmakingUIController
      → Polling via RefreshLobby() every 5s

Player B clicks "Play Online" (within 20s)
  → MatchMakingStarter.Start()
  → Calls LobbyManager.QuickMatchLobby()
  → QueryLobbiesAsync → finds Player A's lobby
  → Reads joinCode from lobby's public data
  → JoinMatchmakingLobby():
      → JoinLobbyByCodeAsync
      → UpdatePlayerAsync (sets name + avatar index)
      → Shows lobbyPanel: Slot 0 = Player A (avatar+name), Slot 1 = Player B (avatar+name)
      → Shows "Match found!" text
      → Stops timer
      → Joins relay

Host's RefreshLobby() detects >=2 players:
  → Shows "Match found!" on timer
  → Waits 1.5s (UX polish)
  → Calls StartGame() → same gameplay as Play with Friends

Timer expires with <2 players:
  → CleanupMatchmakingLobby() (removes player, deletes lobby, shuts down network)
  → Shows mode selection panel (same as current behavior)
```

## Files to Change (3 files)

### Impact Analysis

| Mode | Impact | Reason |
|------|--------|--------|
| Offline (Play Offline) | **NONE** | `GameModeManager.isOnlineMode` stays `false`, `MatchMakingStarter.Start()` returns early at line 11 |
| Play with Friends | **NONE** | Uses `CreateLobby()` / `JoinLobby()` directly — those methods are untouched, in separate `#region` |
| Play Online (Matchmaking) | **CHANGED** | New `QuickMatchLobby()` flow creates/joins lobby, shows players, auto-starts |

### MVC Structure
- **Model**: `ProfileData`, `NetworkPlayer`, `Lobby` data — unchanged
- **View**: `PlayerProfileUI`, `MatchmakingUIController` — UI updates only
- **Controller**: `LobbyManager` handles lobby logic, `NetworkGameManager` handles game logic — unchanged

---

## File 1: `Assets/Scripts/LobbyManager.cs`

### Summary of changes:
1. Add `bool isMatchmakingLobby = false;` field
2. Modify `RefreshLobby()` — add auto-start logic when >=2 players in matchmaking lobby
3. Add `QuickMatchLobby()` — query for matchmaking lobbies, join or create
4. Add `CreateMatchmakingLobby()` — create lobby + relay with public joinCode + isMatchmaking flag
5. Add `JoinMatchmakingLobby()` — join by code, set player data, show UI
6. Add `CleanupMatchmakingLobby()` — remove player, delete lobby, shutdown network
7. Modify `UpdatePlayerSlotsUI()` — hide start button during matchmaking (auto-start handles it)
8. Modify `CloseLobby()` — reset `isMatchmakingLobby` flag
9. **ZERO changes** to `CreateLobby()`, `JoinLobby()`, or any friends-lobby methods

### Full replacement code:

```csharp
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.Authentication;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance;
    public GameObject creatingRoomPanel;
    public GameObject lobbyPanel;

    public ToastUI toastUI;
    public GameObject errorPopup;
    public TMPro.TextMeshProUGUI errorText;

    public PlayerProfileUI[] playerSlots;
    public GameObject menuBackground;

    public GameObject friendsPanel;
    public GameObject matchmakingPanel;
    public GameObject modeSelectionPanel;

    public TMPro.TextMeshProUGUI roomCodeText;
    public UnityEngine.UI.Button startButton;
    public GameObject enteringGamePanel;
    public AvatarDatabase avatarDatabase;

    Lobby currentLobby;
    float polltimer = 0f;
    float pollInterval = 5f;
    bool isMatchmakingLobby = false;

    enum ErrorAction
    {
        None,
        GoToMenu,
        Stay
    }
    ErrorAction pendingAction = ErrorAction.None;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (currentLobby == null)
            return;
        polltimer += Time.deltaTime;
        if (polltimer > pollInterval)
        {
            polltimer = 0f;
            _ = RefreshLobby();
        }
    }

    void ShowToast(string msg)
    {
        if (toastUI != null)
            toastUI.ShowToastWithAutoHide(msg, 3f);
    }
    void ShowErrorPopup(string msg)
    {
        if (errorPopup != null)
            errorPopup.SetActive(true);

        if (errorText != null)
            errorText.text = msg;
    }

    public void OnErrorPopupOK()
    {
        if (errorPopup != null)
            errorPopup.SetActive(false);

        if (pendingAction == ErrorAction.GoToMenu)
        {
            Debug.Log("Going back to menu...");

            if (menuBackground != null)
                menuBackground.SetActive(true);

            if (friendsPanel != null)
                friendsPanel.SetActive(false);
            if (lobbyPanel != null)
                lobbyPanel.SetActive(false);
            if (creatingRoomPanel != null)
                creatingRoomPanel.SetActive(false);
        }
        pendingAction = ErrorAction.None;
    }

    void OnEnable()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
        }
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedRefresh;
        }
    }
    void OnDisable()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;
        }
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedRefresh;
        }
    }

    void OnSceneLoaded(ulong clientId, string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Debug.Log("Scene loaded: " + sceneName + " for client: " + clientId);

        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            if (enteringGamePanel != null)
            {
                enteringGamePanel.SetActive(false);
            }
        }
    }

    async void OnClientConnectedRefresh(ulong clientId)
    {
        if (currentLobby == null) return;
        Debug.Log("[LobbyRefresh] Client connected: " + clientId + " — refreshing lobby");
        await RefreshLobby();
    }

    // MODIFIED: Added auto-start for matchmaking when >=2 players found
    async Task RefreshLobby()
    {
        try
        {
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            Debug.Log("Players in lobby: " + currentLobby.Players.Count);
            UpdatePlayerSlotsUI();

            // AUTO-START: If matchmaking lobby has >=2 players, host auto-starts
            if (isMatchmakingLobby && currentLobby.Players.Count >= 2 && NetworkManager.Singleton.IsHost)
            {
                Debug.Log("Matchmaking: 2+ players found -> auto-starting game!");

                if (MatchmakingUIController.Instance != null)
                    MatchmakingUIController.Instance.MatchFound();

                await System.Threading.Tasks.Task.Delay(1500);
                StartGame();
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning("Lobby refresh failed: " + e.Message);

            if (e.Reason == LobbyExceptionReason.LobbyNotFound ||
                e.Reason == LobbyExceptionReason.Forbidden)
            {
                Debug.Log("Lobby no longer exists -> cleaning up");
                HandleLobbyClosed();
            }
        }
    }

    void HandleLobbyClosed()
    {
        Debug.Log("Handling lobby close...");

        currentLobby = null;

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Network stopped");
        }

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (creatingRoomPanel != null)
            creatingRoomPanel.SetActive(false);

        ShowErrorPopup("Disconnected from lobby");
        pendingAction = ErrorAction.Stay;
    }

    #region Play With Friends (Create / Join by code) — UNTOUCHED

    public async void CreateLobby()
    {
        creatingRoomPanel.SetActive(true);
        currentLobby = await LobbyService.Instance.CreateLobbyAsync("My Room", 4);

        await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id,
        Unity.Services.Authentication.AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {
                    "name",
                    new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerName)
                },
                {
                    "avatar",
                    new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerAvatarIndex.ToString())
                }
           }
        });
        Debug.Log("Lobby Created: " + currentLobby.Id);

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
        string joinCode = await CreateRelay();
        await LobbyService.Instance.UpdateLobbyAsync(
            currentLobby.Id,
            new UpdateLobbyOptions
            {
                Data = new System.Collections.Generic.Dictionary<string, DataObject>
                {
                {
                    "relayCode",
                    new DataObject(
                        DataObject.VisibilityOptions.Member, joinCode)
                }
                }
            });

        await System.Threading.Tasks.Task.Delay(3000);
        creatingRoomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        UpdatePlayerSlotsUI();
        roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;
    }

    public async void JoinLobby(string code)
    {
        code = code.Trim().ToUpper();
        Debug.Log("Joining Lobby with code: " + code);
        try
        {
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);

            if (e.Reason == LobbyExceptionReason.InvalidJoinCode)
            {
                ShowToast("Invalid Room Code");
            }
            else
            {
                ShowErrorPopup("Check your internet");
                pendingAction = ErrorAction.GoToMenu;
            }
            return;
        }

        await LobbyService.Instance.UpdatePlayerAsync(currentLobby.Id,
        Unity.Services.Authentication.AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                {
                    "name",new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerName)
                },
                {
                    "avatar",new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerAvatarIndex.ToString())
                }
            }
        });

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
        Debug.Log("Joined Lobby");
        lobbyPanel?.SetActive(true);
        menuBackground?.SetActive(false);
        if (roomCodeText != null)
        {
            roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;
        }
        UpdatePlayerSlotsUI();
        await JoinRelay();
    }

    #endregion

    #region Matchmaking (Play Online) — NEW

    // Entry point called by MatchMakingStarter
    public async void QuickMatchLobby()
    {
        isMatchmakingLobby = true;
        creatingRoomPanel?.SetActive(true);
        menuBackground?.SetActive(false);

        try
        {
            // Step 1: Query for existing matchmaking lobbies with available slots
            var queryResults = await LobbyService.Instance.QueryLobbiesAsync(
                new QueryLobbiesOptions
                {
                    Filters = new List<QueryFilter>
                    {
                        // Must have available slots
                        new QueryFilter(
                            QueryFilter.FieldOptions.AvailableSlots,
                            "0",
                            QueryFilter.OpOptions.GT),
                        // Must be a matchmaking lobby (not a friends lobby)
                        new QueryFilter(
                            QueryFilter.FieldOptions.Data1,
                            "true",
                            QueryFilter.OpOptions.EQ,
                            "isMatchmaking")
                    },
                    Count = 1
                });

            // Step 2a: If a matchmaking lobby exists, join it
            if (queryResults.Results != null && queryResults.Results.Count > 0)
            {
                var foundLobby = queryResults.Results[0];
                string lobbyJoinCode = foundLobby.Data.ContainsKey("joinCode")
                    ? foundLobby.Data["joinCode"].Value : "";

                if (!string.IsNullOrEmpty(lobbyJoinCode))
                {
                    await JoinMatchmakingLobby(lobbyJoinCode);
                    return;
                }
            }

            // Step 2b: No lobby found — create a new one
            await CreateMatchmakingLobby();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning("QuickMatch query failed: " + e.Message);
            await CreateMatchmakingLobby();
        }
    }

    // Host path: create lobby + relay + store public join code
    async Task CreateMatchmakingLobby()
    {
        isMatchmakingLobby = true;

        string lobbyName = "MM_" + System.Guid.NewGuid().ToString("N").Substring(0, 6);
        currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 4);

        // Set host player data (name + avatar)
        await LobbyService.Instance.UpdatePlayerAsync(
            currentLobby.Id,
            AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileData.PlayerName) },
                    { "avatar", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileData.PlayerAvatarIndex.ToString()) }
                }
            });

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        // Create relay allocation (starts host)
        string relayJoinCode = await CreateRelay();

        // Store relayCode (member-only) + joinCode (public) + isMatchmaking flag (public)
        await LobbyService.Instance.UpdateLobbyAsync(
            currentLobby.Id,
            new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "relayCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, currentLobby.LobbyCode) },
                    { "isMatchmaking", new DataObject(DataObject.VisibilityOptions.Public, "true") }
                }
            });

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        // Show lobby panel with player slot for host
        creatingRoomPanel?.SetActive(false);
        lobbyPanel?.SetActive(true);
        matchmakingPanel?.SetActive(true);

        if (roomCodeText != null)
            roomCodeText.text = "Searching for players...";

        UpdatePlayerSlotsUI();

        // Start matchmaking timer
        if (MatchmakingUIController.Instance != null)
            MatchmakingUIController.Instance.StartSearching();

        Debug.Log("Matchmaking lobby created: " + currentLobby.Id);
    }

    // Client path: join existing matchmaking lobby by code
    async Task JoinMatchmakingLobby(string joinCode)
    {
        isMatchmakingLobby = true;

        currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode);

        // Set client player data (name + avatar)
        await LobbyService.Instance.UpdatePlayerAsync(
            currentLobby.Id,
            AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "name", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileData.PlayerName) },
                    { "avatar", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ProfileData.PlayerAvatarIndex.ToString()) }
                }
            });

        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);

        // Show lobby panel with both player slots
        creatingRoomPanel?.SetActive(false);
        lobbyPanel?.SetActive(true);
        matchmakingPanel?.SetActive(true);

        if (roomCodeText != null)
            roomCodeText.text = "Match found!";

        // Stop timer and show match found
        if (MatchmakingUIController.Instance != null)
            MatchmakingUIController.Instance.MatchFound();

        UpdatePlayerSlotsUI();

        // Join the relay (starts client)
        await JoinRelay();

        Debug.Log("Matchmaking lobby joined: " + currentLobby.Id);
    }

    // Cleanup: called when timer expires or user cancels
    public async void CleanupMatchmakingLobby()
    {
        if (currentLobby == null) return;

        try
        {
            // Remove self from lobby
            await LobbyService.Instance.RemovePlayerAsync(
                currentLobby.Id,
                AuthenticationService.Instance.PlayerId);

            // If host, delete the lobby entirely
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Matchmaking cleanup error: " + e.Message);
        }

        currentLobby = null;
        isMatchmakingLobby = false;

        // Stop network
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Network stopped after matchmaking cleanup");
        }

        // Hide panels
        lobbyPanel?.SetActive(false);
        creatingRoomPanel?.SetActive(false);
        matchmakingPanel?.SetActive(false);
    }

    #endregion

    #region Relay — UNTOUCHED

    async System.Threading.Tasks.Task<string> CreateRelay()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log("Relay Code: " + joinCode);

        await LobbyService.Instance.UpdateLobbyAsync(currentLobby.Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            {
                { "relayCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
            }
        });

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );
        Debug.Log("Starting Host...");
        NetworkManager.Singleton.StartHost();
        return joinCode;
    }

    async Task JoinRelay()
    {
        try
        {
            Debug.Log("Joining Relay...");
            string relayCode = currentLobby.Data["relayCode"].Value;
            Debug.Log("Relay Code: " + relayCode);

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(relayCode);
            var transport = NetworkManager.Singleton
                .GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                allocation.HostConnectionData
            );

            Debug.Log("Starting Client...");
            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Relay failed: " + e.Message);
            ShowErrorPopup("Connection failed");
            pendingAction = ErrorAction.Stay;
        }
    }

    #endregion

    // MODIFIED: Added isMatchmakingLobby reset
    public void CloseLobby()
    {
        Debug.Log("Closing Lobby...");
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
        if (creatingRoomPanel != null)
            creatingRoomPanel.SetActive(false);

        if (NetworkManager.Singleton != null &&
         NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Network Stopped.");
        }

        if (friendsPanel != null)
            friendsPanel.SetActive(false);

        if (matchmakingPanel != null)
            matchmakingPanel.SetActive(false);

        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);

        if (menuBackground != null)
        {
            menuBackground.SetActive(true);
        }

        isMatchmakingLobby = false;
    }

    System.Collections.IEnumerator LeaveLobbyCoroutine()
    {
        var task = Unity.Services.Lobbies.LobbyService.Instance.RemovePlayerAsync(currentLobby.Id,
            Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);

        while (!task.IsCompleted)
            yield return null;

        Debug.Log("Left Lobby Successfully");
        currentLobby = null;
    }

    // UNTOUCHED
    public void StartGame()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Only host can start");
            return;
        }
        if (currentLobby == null || currentLobby.Players.Count < 2)
        {
            ShowToast("At least 2 players required");
            return;
        }

        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (menuBackground != null) menuBackground.SetActive(false);
        if (friendsPanel != null) friendsPanel.SetActive(false);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (enteringGamePanel != null) enteringGamePanel.SetActive(true);

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.InitializeAndDeal();
        }
    }

    // MODIFIED: Hide start button during matchmaking (auto-start handles it)
    void UpdatePlayerSlotsUI()
    {
        if (currentLobby == null) return;
        if (playerSlots == null) return;
        string localPlayerId = AuthenticationService.Instance.PlayerId;
        Debug.Log($"[LobbySlots] Players in lobby: {currentLobby.Players.Count}, LocalPlayerId: {localPlayerId}");

        foreach (var p in currentLobby.Players)
        {
            string dataInfo = p.Data != null
                ? $"name={(p.Data.ContainsKey("name") ? p.Data["name"].Value : "MISSING")}, avatar={(p.Data.ContainsKey("avatar") ? p.Data["avatar"].Value : "MISSING")}"
                : "Data=NULL";
            Debug.Log($"[LobbySlots] Player {p.Id}: {dataInfo}");
        }
        for (int i = currentLobby.Players.Count; i < playerSlots.Length; i++)
        {
            playerSlots[i]?.SetProfile("Waiting...", null);
        }

        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            if (i >= playerSlots.Length) break;
            if (playerSlots[i] == null) continue;
            var player = currentLobby.Players[i];
            string name;
            int avatarIndex;
            Sprite avatarSprite = null;
            if (player.Id == localPlayerId)
            {
                name = string.IsNullOrEmpty(ProfileData.PlayerName)
                    ? "Player" : ProfileData.PlayerName;
                avatarIndex = ProfileData.PlayerAvatarIndex;
            }
            else
            {
                name = "Player";
                avatarIndex = 0;
                if (player.Data != null &&
                    player.Data.ContainsKey("name"))
                {
                    name = player.Data["name"].Value;
                }
                if (player.Data != null &&
                    player.Data.ContainsKey("avatar"))
                {
                    int.TryParse(
                        player.Data["avatar"].Value,
                        out avatarIndex
                    );
                }
            }

            if (avatarDatabase != null &&
                avatarDatabase.avatarSprites != null &&
                avatarIndex >= 0 &&
                avatarIndex < avatarDatabase.avatarSprites.Length)
            {
                avatarSprite =
                    avatarDatabase.avatarSprites[avatarIndex];
            }
            playerSlots[i].SetProfile(name, avatarSprite);
            Debug.Log($"Slot{i} -> {name}, AvatarIndex: {avatarIndex},IsLocal: {player.Id == localPlayerId}"
            );
        }
        // Start button: hidden during matchmaking (auto-start), shown during friends lobby
        if (startButton != null && !isMatchmakingLobby)
        {
            startButton.interactable = NetworkManager.Singleton.IsHost &&
                currentLobby.Players.Count >= 2;
            startButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        }
        else if (startButton != null && isMatchmakingLobby)
        {
            startButton.gameObject.SetActive(false);
        }
    }
}
```

---

## File 2: `Assets/Scripts/MatchMakingStarter.cs`

### Changes:
1. Removed `isOnlineMode = false` bug (was on line 49)
2. Now calls `LobbyManager.QuickMatchLobby()` to create/join lobby
3. Removed manual profile setup (LobbyManager handles it via `UpdatePlayerSlotsUI`)
4. Removed unused `myProfileUI` field reference

### Full replacement code:

```csharp
using UnityEngine;

public class MatchmakingStarter : MonoBehaviour
{
    void Start()
    {
        if (!GameModeManager.isOnlineMode)
        {
            return;
        }
        Debug.Log("Online mode detected -> starting matchmaking");

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.QuickMatchLobby();
        }
        else
        {
            Debug.LogError("LobbyManager.Instance is null - cannot start matchmaking");
        }
    }
}
```

### What changed line-by-line:
- **Removed**: `public PlayerProfileUI myProfileUI;` — no longer needed
- **Removed**: Lines 17-48 (manual profile setup + timer) — LobbyManager handles this now
- **Removed**: `GameModeManager.isOnlineMode = false;` — was killing online mode prematurely
- **Added**: `LobbyManager.Instance.QuickMatchLobby();` — creates/joins matchmaking lobby

---

## File 3: `Assets/Scripts/MatchmakingUIController.cs`

### Changes:
1. Added `MatchFound()` method — stops timer, shows "Match found!" text
2. Modified `HandleNoPlayersFound()` — calls `CleanupMatchmakingLobby()` before fallback
3. Modified `OnCancelClicked()` — calls `CleanupMatchmakingLobby()` on cancel

### Full replacement code:

```csharp
using UnityEngine;
using TMPro;
using System.Collections;

public class MatchmakingUIController : MonoBehaviour
{
    public static MatchmakingUIController Instance;

    public GameObject panel;
    public TextMeshProUGUI timerText;

    float timer = 20f;
    bool searching = false;

    void Awake()
    {
        Instance = this;
    }

    public void StartSearching()
    {
        panel.SetActive(true);
        timer = 20f;
        searching = true;
        StartCoroutine(SearchTimer());
    }

    IEnumerator SearchTimer()
    {
        while (timer > 0 && searching)
        {
            timer -= Time.deltaTime;
            timerText.text = "TIMER : " + Mathf.Ceil(timer) + "s";
            yield return null;
        }

        if (timer <= 0 && searching)
        {
            Debug.Log("No players found -> fallback");
            StartCoroutine(HandleNoPlayersFound());
        }
    }

    // MODIFIED: Now cleans up the lobby before showing fallback
    IEnumerator HandleNoPlayersFound()
    {
        searching = false;
        timerText.text = "No players found. Returning...";

        // Clean up the matchmaking lobby
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.CleanupMatchmakingLobby();

        yield return new WaitForSeconds(3f);

        panel.SetActive(false);

        var menu = FindAnyObjectByType<MenuController>();

        if (menu != null)
        {
            menu.MenuUI.SetActive(false);
            menu.LoadingPanel.SetActive(false);
            menu.ModeSelectionPanel.SetActive(true);
            var controller = menu.ModeSelectionPanel.GetComponent<ModeSelectionController>();
            if (controller != null)
            {
                controller.SetHeader("Online Mode");
            }
        }
    }

    // NEW: Called when a match is found (>=2 players)
    public void MatchFound()
    {
        searching = false;
        StopAllCoroutines();
        if (timerText != null)
            timerText.text = "Match found!";
    }

    public void StopSearching()
    {
        searching = false;
        panel.SetActive(false);
    }

    // MODIFIED: Now cleans up the lobby on cancel
    public void OnCancelClicked()
    {
        Debug.Log("Matchmaking cancelled by user");
        StopSearching();
        GameModeManager.isOnlineMode = false;

        // Clean up the matchmaking lobby
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.CleanupMatchmakingLobby();

        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            menu.MenuUI.SetActive(true);
        }
        panel.SetActive(false);
    }
}
```

---

## No Hardcode Values
- Lobby name: `"MM_" + Guid` (unique per session, no collision)
- Max players: `4` (same as friends lobby, comes from `CreateLobbyAsync` param)
- Timer: `20f` (configurable via `timer` field in `MatchmakingUIController`)
- Poll interval: `5f` (configurable via `pollInterval` field in `LobbyManager`)
- Auto-start threshold: `>=2` (same check as `StartGame()` line 499)
- Match found delay: `1.5s` (UX polish before auto-start)

## Implementation Order
1. Replace `LobbyManager.cs` with new code
2. Replace `MatchMakingStarter.cs` with new code
3. Replace `MatchmakingUIController.cs` with new code
4. In Unity Editor: verify all Inspector references on `LobbyManager` are intact (playerSlots, avatarDatabase, etc.)
5. Test: 2 editors → Play Online → both see each other in slots → game auto-starts
6. Test: 1 editor → Play Online → timer expires → fallback to mode selection works
7. Test: Play Offline → no change
8. Test: Play with Friends → no change

## Note on `MatchMakingStarter.myProfileUI` field
Since we removed the `myProfileUI` field from `MatchMakingStarter`, if the prefab has this reference assigned, Unity will show a "Missing Reference" warning. This is harmless — the field is no longer used. If you want to clean it up, just remove the reference from the prefab in the Inspector.
