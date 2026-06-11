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

    public PlayerProfileUI[] playerSlots; // 0 = YOU
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
        // only run if lobby exists
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
        // hide popup
        if (errorPopup != null)
            errorPopup.SetActive(false);

        //CHECK WHAT TO DO NEXT
        if (pendingAction == ErrorAction.GoToMenu)
        {
            Debug.Log("Going back to menu...");

            // show menu background
            if (menuBackground != null)
                menuBackground.SetActive(true);

            // hide other panels
            if (friendsPanel != null)
                friendsPanel.SetActive(false);
            if (lobbyPanel != null)
                lobbyPanel.SetActive(false);
            if (creatingRoomPanel != null)
                creatingRoomPanel.SetActive(false);
        }
        // reset action
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

        // Only affect THIS client
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Hide entering panel
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

    async Task RefreshLobby()
    {
        try
        {
            //Ask server for latest lobby data
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            UpdatePlayerSlotsUI();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning("Lobby refresh failed: " + e.Message);

            //If lobby deleted or player removed
            if (e.Reason == LobbyExceptionReason.LobbyNotFound ||
                e.Reason == LobbyExceptionReason.Forbidden)
            {
                Debug.Log("Lobby no longer exists → cleaning up");
                HandleLobbyClosed();
            }
        }
    }

    void HandleLobbyClosed()
    {
        Debug.Log("Handling lobby close...");

        // STEP 1 — Remove lobby reference
        currentLobby = null;

        // STEP 2 — Stop multiplayer connection
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            // NetworkManager.Singleton.Shutdown();
            Debug.Log("Network stopped");
        }

        // STEP 3 — Hide lobby UI
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (creatingRoomPanel != null)
            creatingRoomPanel.SetActive(false);
        ShowErrorPopup("Disconnected from lobby");
        pendingAction = ErrorAction.Stay;
    }


    // / CREATE ROOM (HOST)
    public async void CreateLobby()
    {
        //Show loading panel
        creatingRoomPanel.SetActive(true);
        //Create lobby
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

        //Refresh local lobby to get our own updated data
        currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
        //Create Relay
        string joinCode = await CreateRelay();
        //Store relay code inside lobby
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

        //Small delay (UX smooth)
        await System.Threading.Tasks.Task.Delay(3000);
        //Switch UI
        creatingRoomPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        UpdatePlayerSlotsUI();
        //Show room code
        roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;
        //Start Host
        // NetworkManager.Singleton.StartHost();
    }

    //JOIN ROOM WITH CODE
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
                // CASE 1 handled here (toast)
                ShowToast("Invalid Room Code");
            }
            else
            {
                //THIS IS STEP 4 (NO INTERNET)
                ShowErrorPopup("Check your internet");

                //tell system → go to menu after OK
                pendingAction = ErrorAction.GoToMenu;
            }
            return; // STOP EXECUTION
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

        //Refresh local lobby to get our own updated data
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

    //CREATE RELAY (HOST SIDE)
    async System.Threading.Tasks.Task<string> CreateRelay()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        Debug.Log("Relay Code: " + joinCode);
        //SAVE RELAY CODE IN LOBBY (IMPORTANT)
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

    //JOIN RELAY (CLIENT SIDE)
    async Task JoinRelay()
    {
        try
        {
            Debug.Log("Joining Relay...");
            //Get relay code from lobby
            string relayCode = currentLobby.Data["relayCode"].Value;
            Debug.Log("Relay Code: " + relayCode);

            //Join relay allocation
            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(relayCode);
            //Configure transport
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

            //Start client
            Debug.Log("Starting Client...");
            NetworkManager.Singleton.StartClient();
            // StartCoroutine(FallbackHideEnteringPanel());
        }
        catch (System.Exception e)
        {
            Debug.LogError("Relay failed: " + e.Message);
            //SHOW ERROR POPUP
            ShowErrorPopup("Connection failed");
            //STAY IN SAME SCREEN
            pendingAction = ErrorAction.Stay;
        }
    }
    public void CloseLobby()
    {
        Debug.Log("Closing Lobby...");
        // hide UI Panels
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
        if (creatingRoomPanel != null)
            creatingRoomPanel.SetActive(false);

        // stop network
        if (NetworkManager.Singleton != null &&
         NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Network Stopped.");
        }

        // leave Lobby
        if (friendsPanel != null)
            friendsPanel.SetActive(false);

        if (matchmakingPanel != null)
            matchmakingPanel.SetActive(false);

        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);
        // enable only the menu background
        if (menuBackground != null)
        {
            menuBackground.SetActive(true);
        }
    }

    public void QuitFromGame()
    {
        Debug.Log("Quitting online game to menu...");
        // 1. Leave the lobby service
        if (currentLobby != null)
        {
            StartCoroutine(LeaveLobbyCoroutine());
        }
        // 2. Hide all game-related panels
        enteringGamePanel?.SetActive(false);
        lobbyPanel?.SetActive(false);
        creatingRoomPanel?.SetActive(false);
        friendsPanel?.SetActive(false);
        matchmakingPanel?.SetActive(false);
        modeSelectionPanel?.SetActive(false);

        // 3. Hide game scene UI
        GameSceneUI gameSceneUI = FindAnyObjectByType<GameSceneUI>();
        if (gameSceneUI != null && gameSceneUI.gameScenePanel != null)
        {
            gameSceneUI.gameScenePanel.SetActive(false);
        }
        // 4. Shutdown network
        if (NetworkManager.Singleton != null &&  NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Network Stopped.");
        }

        // 5. Reset online mode flag
        GameModeManager.isOnlineMode = false;
        // 6. Show menu background
        menuBackground?.SetActive(true);
        // 7. Show main menu UI
        MenuController menu = FindAnyObjectByType<MenuController>();
        if (menu != null && menu.MenuUI != null)
            menu.MenuUI.SetActive(true);
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

        // Hide lobby UI
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (menuBackground != null) menuBackground.SetActive(false);
        if (friendsPanel != null) friendsPanel.SetActive(false);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (enteringGamePanel != null) enteringGamePanel.SetActive(true);

        // TRIGGER game start on NetworkGameManager
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.InitializeAndDeal();
        }
    }

    void UpdatePlayerSlotsUI()
    {
        if (currentLobby == null) return;
        if (playerSlots == null) return;
        string localPlayerId = AuthenticationService.Instance.PlayerId;
        // Debug.Log($"[LobbySlots] Players in lobby: {currentLobby.Players.Count}, LocalPlayerId: {localPlayerId}");

        foreach (var p in currentLobby.Players)
        {
            string dataInfo = p.Data != null
                ? $"name={(p.Data.ContainsKey("name") ? p.Data["name"].Value : "MISSING")}, avatar={(p.Data.ContainsKey("avatar") ? p.Data["avatar"].Value : "MISSING")}"
                : "Data=NULL";
            // Debug.Log($"[LobbySlots] Player {p.Id}: {dataInfo}");
        }
        // Clear empty slots
        for (int i = currentLobby.Players.Count; i < playerSlots.Length; i++)
        {
            playerSlots[i]?.SetProfile("Waiting...", null);
        }

        // Fill player slots
        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            if (i >= playerSlots.Length) break;
            if (playerSlots[i] == null) continue;
            var player = currentLobby.Players[i];
            string name;
            int avatarIndex;
            Sprite avatarSprite = null;
            // LOCAL PLAYER
            if (player.Id == localPlayerId)
            {
                name = string.IsNullOrEmpty(ProfileData.PlayerName)
                    ? "Player" : ProfileData.PlayerName;
                avatarIndex = ProfileData.PlayerAvatarIndex;
            }
            else
            {
                // OTHER PLAYERS
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

            // GET AVATAR SPRITE
            if (avatarDatabase != null &&
                avatarDatabase.avatarSprites != null &&
                avatarIndex >= 0 &&
                avatarIndex < avatarDatabase.avatarSprites.Length)
            {
                avatarSprite =
                    avatarDatabase.avatarSprites[avatarIndex];
            }
            // APPLY UI
            playerSlots[i].SetProfile(name, avatarSprite);
            // Debug.Log($"Slot{i} → {name}, AvatarIndex: {avatarIndex},IsLocal: {player.Id == localPlayerId}"
            // );
        }
        // START BUTTON
        if (startButton != null)
        {
            startButton.interactable = NetworkManager.Singleton.IsHost &&
                currentLobby.Players.Count >= 2;
            startButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
        }
    }
}