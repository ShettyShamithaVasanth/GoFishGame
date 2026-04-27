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

    Lobby currentLobby;

    void Awake()
    {
        Instance = this;
    }

    // / CREATE ROOM (HOST)
    public async void CreateLobby()
    {
        //Show loading panel
        creatingRoomPanel.SetActive(true);
        //Create lobby
        currentLobby = await LobbyService.Instance.CreateLobbyAsync("My Room", 4);
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

        //Show room code
        roomCodeText.text = "Room Code: " + currentLobby.Id.Substring(0, 4);
        //Set YOUR PLAYER UI
        SetMyPlayerUI();
        //Start Host
        // NetworkManager.Singleton.StartHost();
    }

    void SetMyPlayerUI()
    {
        string playerName = ProfileData.PlayerName;
        Sprite avatar = ProfileData.PlayerAvatar;
        if (string.IsNullOrEmpty(playerName))
            playerName = "Player";

        if (playerSlots != null && playerSlots.Length > 0)
        {
            playerSlots[0].SetProfile(playerName, avatar);
        }
    }

    //JOIN ROOM WITH CODE
    public async void JoinLobby(string code)
    {
        Debug.Log("Joining Lobby with code: " + code);
        currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
        Debug.Log("Joined Lobby");
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
        // if not host->do nothing
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.Log("Only host can start the game.");
            return;
        }
        Debug.Log("Host starting the game...");
        // hide lobby UI
        lobbyPanel.SetActive(false);
        // enable game UI (TODO)
        var menu = FindFirstObjectByType<MenuController>();
        if (menu != null)
        {
            // hide menu completely
            menu.MenuUI.SetActive(false);
            menu.LoadingPanel.SetActive(false);
            // start gameplay directly(like offline mode)
            menu.ContinueGame();
        }
        else
        {
            Debug.LogError("MenuController not found in the scene.");
        }
    }

    void Start()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            // disable start game button for clients
            var Startbtn = FindFirstObjectByType<UnityEngine.UI.Button>();
            if (Startbtn != null)
            {
                Startbtn.interactable = false;
            }
        }
    }
}