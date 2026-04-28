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
        catch (System.Exception e)
        {
            Debug.LogWarning("Lobby refresh failed: " + e.Message);
        }
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
        currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);

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
        string relayCode = currentLobby.Data["relayCode"].Value;
        Debug.Log("Joining Relay with code: " + relayCode);
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

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

        Debug.Log("Host starting game with scene sync...");

        //Hide lobby UI
        lobbyPanel.SetActive(false);
        //LOAD GAME SCENE (THIS IS THE REAL FIX)
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene",
            UnityEngine.SceneManagement.LoadSceneMode.Single);
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