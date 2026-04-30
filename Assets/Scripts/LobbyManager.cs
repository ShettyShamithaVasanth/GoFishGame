using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Collections.Generic;

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
    float pollInterval = 2f;

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
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete += OnSceneLoaded;
        }
    }
    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= OnSceneLoaded;
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

    async Task RefreshLobby()
    {
        try
        {
            //Ask server for latest lobby data
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            //Print how many players are inside
            Debug.Log("Players in lobby: " + currentLobby.Players.Count);
            //Update UI (we will build this next step)
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

        //STEP 4 — Show main menu again
        // if (menuBackground != null)
        //     menuBackground.SetActive(true);

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

        //Set YOUR PLAYER UI
        // SetMyPlayerUI();
        //Show room code
        roomCodeText.text = "Room Code: " + currentLobby.LobbyCode;
        //Start Host
        // NetworkManager.Singleton.StartHost();
    }

    // void SetMyPlayerUI()
    // {
    //     Debug.Log("Setting MY Player UI...");
    //     string playerName = ProfileData.PlayerName;
    //     Sprite avatar = ProfileData.PlayerAvatar;
    //     Debug.Log("Name:" + ProfileData.PlayerName + ", Avatar:" + ProfileData.PlayerAvatar);

    //     //SAFETY FIXES
    //     if (string.IsNullOrEmpty(playerName))
    //         playerName = "Player";
    //     if (avatar == null)
    //         Debug.LogWarning("Avatar is NULL → check ProfileData");

    //     //APPLY TO SLOT 0
    //     if (playerSlots != null && playerSlots.Length > 0 && playerSlots[0] != null)
    //     {
    //         playerSlots[0].SetProfile(playerName, avatar);
    //     }
    //     else
    //     {
    //         Debug.LogError("PlayerSlots[0] not assigned!");
    //     }
    // }

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

        Debug.Log("Joined Lobby");
        //SHOW "ENTERING GAME" PANEL
        if (enteringGamePanel != null)
        {
            enteringGamePanel.SetActive(true);
        }

        if (menuBackground != null)
        {
            menuBackground.SetActive(false);
        }
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

    // System.Collections.IEnumerator FallbackHideEnteringPanel()
    // {
    //     yield return new WaitForSeconds(10f);

    //     // if still visible, force hide
    //     if (enteringGamePanel != null && enteringGamePanel.activeSelf)
    //     {
    //         Debug.LogWarning("Force hiding entering panel (fallback)");
    //         enteringGamePanel.SetActive(false);
    //     }
    // }

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
            Debug.Log("Only host can start the game.");
            return;
        }
        // Minimum 2 players to start
        if (currentLobby == null || currentLobby.Players.Count < 2)
        {
            Debug.Log("Not enough players to start the game.");
            ShowToast("At least 2 players required");
            return;
        }

        // Debug.Log("Starting Game...");
        Debug.Log("starting game,loadidng scene...");
        // hide lobby UI
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
        // disable menu background
        if (menuBackground != null)
            menuBackground.SetActive(false);

        // hide all menu panels
        if (friendsPanel != null)
            friendsPanel.SetActive(false);
        if (matchmakingPanel != null)
            matchmakingPanel.SetActive(false);
        if (modeSelectionPanel != null)
            modeSelectionPanel.SetActive(false);

        //show loading panel (for clients)
        if (enteringGamePanel != null)
            enteringGamePanel.SetActive(true);
        // load game scene using Netcode SceneManager
//        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // [Unity.Netcode.ClientRpc]
    // void StartGameClientRpc()
    // {
    //     Debug.Log("Game starting on all clients...");
    //     //hide entering panel (for clients)
    //     if (enteringGamePanel != null)
    //     {
    //         enteringGamePanel.SetActive(false);
    //     }

    //     //find MenuController
    //     var menu = FindFirstObjectByType<MenuController>();
    //     if (menu != null)
    //     {
    //         //hide menu UI
    //         menu.MenuUI.SetActive(false);
    //         menu.LoadingPanel.SetActive(false);

    //         //START GAME (same as offline)
    //         // menu.ContinueGame();
    //     }
    //     else
    //     {
    //         Debug.LogError("MenuController not found!");
    //     }
    // }

    // void Start()
    // {
    //     if (!NetworkManager.Singleton.IsHost)
    //     {
    //         // disable start game button for clients
    //         var Startbtn = FindFirstObjectByType<UnityEngine.UI.Button>();
    //         if (Startbtn != null)
    //         {
    //             Startbtn.interactable = false;
    //         }
    //     }
    // }

    void UpdatePlayerSlotsUI()
    {
        if (currentLobby == null)
            return;
        //Safety check (important)
        if (playerSlots == null) return;
        //STEP A — Clear all slots except Player_0
        for (int i = 0; i < playerSlots.Length; i++)
        {
            playerSlots[i].SetProfile("Waiting...", null);
        }

        //STEP B — Fill slots with real players
        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            if (i >= playerSlots.Length) break;

            var player = currentLobby.Players[i];

            string name = "Player";
            int avatarIndex = 0;
            Sprite avatarSprite = null;

            //GET NAME
            if (player.Data != null && player.Data.ContainsKey("name"))
            {
                name = player.Data["name"].Value;
            }

            //GET AVATAR INDEX
            if (player.Data != null && player.Data.ContainsKey("avatar"))
            {
                int.TryParse(player.Data["avatar"].Value, out avatarIndex);
            }

            // GET SPRITE FROM DATABASE
            if (avatarDatabase != null &&
                avatarDatabase.avatarSprites != null &&
                avatarIndex < avatarDatabase.avatarSprites.Length)
            {
                avatarSprite = avatarDatabase.avatarSprites[avatarIndex];
            }

            // GET SPRITE FROM DATABASE
            playerSlots[i].SetProfile(name, avatarSprite);

            Debug.Log($"Slot{i} → {name}, AvatarIndex: {avatarIndex}");
        }

        // enable start button only if host band player>=2
        if (startButton != null)
        {
            if (NetworkManager.Singleton.IsHost && currentLobby.Players.Count >= 2)
            {
                startButton.interactable = true;
            }
            else
            {
                startButton.interactable = false;
            }
        }
    }




}