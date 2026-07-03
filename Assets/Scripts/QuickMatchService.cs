using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;


public class QuickMatchService : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int minPlayersToStart = 2;
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private float searchTimeout = 20f;
    [SerializeField] private float pollInterval = 1.5f;
    [SerializeField] private AvatarDatabase avatarDatabase;
    [SerializeField] private string gameModeKey = "GoFish";

    [Header("Events")]
    public System.Action<string, int, int> OnPlayerJoined;
    public System.Action<int> OnPlayerLeft;
    public System.Action OnMatchFound;
    public System.Action OnTimeout;
    public System.Action<string> OnError;
    public System.Action<IReadOnlyList<MatchPlayerInfo>> OnRosterChanged;

    [Header("Runtime")]
    private Lobby currentLobby;
    private bool isSearching;
    private float searchTimer;
    private float pollTimer;
    private bool isHost;
    private string relayCode;
    public bool IsSearching => isSearching;
    public AvatarDatabase AvatarDatabase => avatarDatabase;

    public float RemainingSeconds =>
        Mathf.Max(0f, searchTimeout - searchTimer);

    public static QuickMatchService Instance;
    private const string GAME_MODE_KEY = "mode";
    private const string GAME_MODE_VALUE = "GoFish";
    private const string RELAY_CODE_KEY = "relayCode";

    public struct MatchPlayerInfo
    {
        public string PlayerName;
        public int AvatarIndex;
        public bool IsHost;
        public bool IsLocal;
    }

    private void Awake()
    {
        Instance = this;
    }
    private void Update()
    {
        if (!isSearching)
            return;
        searchTimer += Time.deltaTime;
        pollTimer += Time.deltaTime;
        if (pollTimer >= pollInterval)
        {
            pollTimer = 0f;
            _ = RefreshLobby();
        }
        if (searchTimer >= searchTimeout)
        {
            HandleTimeout();
        }
    }

    private void HandleTimeout()
    {
        isSearching = false;

        StartCoroutine(TimeoutRoutine());
    }
    private System.Collections.IEnumerator TimeoutRoutine()
    {
        yield return LeaveLobbyAndCleanup();

        OnTimeout?.Invoke();
    }
    public async void FindMatch()
    {
        Debug.Log("QuickMatchService FindMatch called");
        isSearching = true;
        searchTimer = 0f;
        pollTimer = 0f;
        relayCode = string.Empty;
        currentLobby = null;
        isHost = false;

        try
        {
            await TryQuickJoin();
        }
        catch (LobbyServiceException)
        {
            await CreateLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError(e.Message);

            OnError?.Invoke(e.Message);

            isSearching = false;
        }
    }

    private async Task TryQuickJoin()
    {
        Debug.Log("Trying Quick Join...");

        currentLobby =
            await LobbyService.Instance.QuickJoinLobbyAsync(
                new QuickJoinLobbyOptions
                {
                    Filter = new List<QueryFilter>
                    {
                    new QueryFilter(
                        QueryFilter.FieldOptions.S1,
                        GAME_MODE_VALUE,
                        QueryFilter.OpOptions.EQ
                    )
                    }
                });

        isHost = false;

        Debug.Log(
            "Joined Lobby : " +
            currentLobby.Id);

        await PublishPlayerData();
        await JoinRelay();
    }

    private async Task CreateLobby()
    {
        Debug.Log("Creating New Lobby...");

        CreateLobbyOptions options =
            new CreateLobbyOptions
            {
                IsPrivate = false,

                Data =
                    new Dictionary<string, DataObject>
                    {
                    {
                        GAME_MODE_KEY,

                        new DataObject(
                            DataObject.VisibilityOptions.Public,
                            GAME_MODE_VALUE)
                    }
                    }
            };

        currentLobby =
            await LobbyService.Instance.CreateLobbyAsync(
                "Go Fish Match",
                maxPlayers,
                options);

        isHost = true;

        Debug.Log(
            "Created Lobby : " +
            currentLobby.Id);

        await PublishPlayerData();
        await CreateRelay();
    }

    private async Task CreateRelay()
    {
        Debug.Log("Creating Relay Allocation...");
        Allocation allocation =
            await RelayService.Instance.CreateAllocationAsync(
                maxPlayers - 1
            );
        relayCode =
            await RelayService.Instance.GetJoinCodeAsync(
                allocation.AllocationId
            );
        Debug.Log(
            "Relay Code : " +
            relayCode
        );

        await LobbyService.Instance.UpdateLobbyAsync(
    currentLobby.Id,
    new UpdateLobbyOptions
    {
        Data =
            new Dictionary<string, DataObject>
            {
                {
                    RELAY_CODE_KEY,

                    new DataObject(
                        DataObject.VisibilityOptions.Member,
                        relayCode
                    )
                }
            }
    });

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );
        if (!NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("Relay Host Started");
        }
    }

    private async Task JoinRelay()
    {
        relayCode = currentLobby.Data[RELAY_CODE_KEY].Value;
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayCode);
        UnityTransport transport =
        NetworkManager.Singleton
            .GetComponent<UnityTransport>();

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );
        if (!NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("Relay Client Started");
        }
    }

    private async Task PublishPlayerData()
    {
        await LobbyService.Instance.UpdatePlayerAsync(
            currentLobby.Id,
            AuthenticationService.Instance.PlayerId,
            new UpdatePlayerOptions
            {
                Data =
                    new Dictionary<string, PlayerDataObject>
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
    }

    private async Task RefreshLobby()
    {
        if (currentLobby == null)
            return;

        currentLobby =
            await LobbyService.Instance.GetLobbyAsync(
                currentLobby.Id);
        Debug.Log("---------------------------");
        Debug.Log("Lobby Refreshed");
        Debug.Log("Players : " + currentLobby.Players.Count);

        List<MatchPlayerInfo> roster =
            new List<MatchPlayerInfo>();

        string hostId = currentLobby.HostId;

        string localId =
            AuthenticationService.Instance.PlayerId;

        foreach (Unity.Services.Lobbies.Models.Player player in currentLobby.Players)
        {
            bool isHost = player.Id == hostId;
            bool isLocal = player.Id == localId;

            string playerName = "Player";
            int avatarIndex = 0;

            if (isLocal)
            {
                playerName = ProfileData.PlayerName;
                avatarIndex = ProfileData.PlayerAvatarIndex;
            }
            else
            {
                if (player.Data != null &&
                    player.Data.ContainsKey("name"))
                {
                    playerName = player.Data["name"].Value;
                }

                if (player.Data != null &&
                    player.Data.ContainsKey("avatar"))
                {
                    int.TryParse(
                        player.Data["avatar"].Value,
                        out avatarIndex);
                }
            }

            roster.Add(new MatchPlayerInfo
            {
                PlayerName = playerName,
                AvatarIndex = avatarIndex,
                IsHost = isHost,
                IsLocal = isLocal
            });
        }

        roster.Sort((a, b) =>
        {
            if (a.IsHost == b.IsHost)
                return 0;

            return a.IsHost ? -1 : 1;
        });
        Debug.Log("Updating Matchmaking UI : " + roster.Count + " players");
        OnRosterChanged?.Invoke(roster);

        CheckMatchReady();
    }


    private async void CheckMatchReady()
    {
        if (currentLobby == null)
            return;
        if (currentLobby.Players.Count < minPlayersToStart)
            return;
        if (!isSearching)
            return;
        Debug.Log("Minimun players reached, starting game...");
        isSearching = false;
        OnMatchFound?.Invoke();
        await StartOnlineGame();
    }
    private async System.Threading.Tasks.Task StartOnlineGame()
    {
        if (isHost)
        {
            await StartHostFlow();
        }
        else
        {
            await StartClientFlow();
        }
    }
    private Task StartHostFlow()
    {
        Debug.Log("Starting Host Flow");

        LobbyManager lobby = LobbyManager.Instance;

        if (lobby != null && lobby.enteringGamePanel != null)
        {
            lobby.enteringGamePanel.SetActive(true);
        }

        if (NetworkGameManager.Instance == null)
        {
            Debug.LogError("NetworkGameManager Instance is NULL");

            return Task.CompletedTask;
        }

        Debug.Log("Initializing Multiplayer Game...");

        NetworkGameManager.Instance.InitializeAndDeal();

        return Task.CompletedTask;
    }
    private Task StartClientFlow()
    {
        Debug.Log("Client waiting for host to start the game");
        return Task.CompletedTask;
    }
    private IEnumerator LeaveLobbyAndCleanup()
    {
        Debug.Log("Cleaning Matchmaking...");

        if (currentLobby != null)
        {
            Task task;

            if (isHost)
            {
                Debug.Log("Deleting Lobby...");

                task =
                    LobbyService.Instance.DeleteLobbyAsync(
                        currentLobby.Id
                    );
            }
            else
            {
                Debug.Log("Leaving Lobby...");

                task =
                    LobbyService.Instance.RemovePlayerAsync(
                        currentLobby.Id,
                        AuthenticationService.Instance.PlayerId
                    );
            }

            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Shutting Down Network...");

            NetworkManager.Singleton.Shutdown();
        }

        currentLobby = null;

        relayCode = string.Empty;

        isHost = false;

        isSearching = false;

        searchTimer = 0f;

        pollTimer = 0f;

        Debug.Log("Cleanup Complete");
    }
    
    public void Cancel()
    {
        isSearching = false;

        StartCoroutine(LeaveLobbyAndCleanup());
    }
}

