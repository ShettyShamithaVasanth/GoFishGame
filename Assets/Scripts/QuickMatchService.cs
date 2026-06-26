using UnityEngine;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using System.Collections.Generic;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class QuickMatchService : MonoBehaviour
{
    public static QuickMatchService Instance;
    private const string LOBBY_MODE_KEY = "mode";

    private const string QUICKMATCH_MODE = "quickmatch";
    private const string RELAY_CODE_KEY = "relayCode";
    public struct LobbyPlayerInfo
    {
        public string PlayerName;
        public int AvatarIndex;
        public bool IsLocal;
    }
    private void Awake()
    {
        Instance = this;

        if (avatarDatabase == null)
        {
            avatarDatabase =
                FindAnyObjectByType<LobbyManager>()
                    ?.avatarDatabase;
        }
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

            if (isHost && currentLobby != null)
            {
                try
                {
                    _ = LobbyService.Instance
                        .SendHeartbeatPingAsync(
                            currentLobby.Id
                        );
                }
                catch
                {
                }
            }

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
        isSearching = true;

        searchTimer = 0f;
        firstRefresh = true;
        await FindOrCreateLobby();
    }

    private async Task FindOrCreateLobby()
    {
        try
        {
            Debug.Log("Searching for Quick Match lobby...");

            QueryResponse result =
                await LobbyService.Instance.QueryLobbiesAsync(
                    new QueryLobbiesOptions()
                );

            Lobby foundLobby = null;

            if (result != null && result.Results != null)
            {
                foreach (Lobby lobby in result.Results)
                {
                    if (lobby == null)
                        continue;

                    if (lobby.Data == null)
                        continue;

                    if (!lobby.Data.ContainsKey(LOBBY_MODE_KEY))
                        continue;

                    if (lobby.Data[LOBBY_MODE_KEY].Value != QUICKMATCH_MODE)
                        continue;

                    if (lobby.AvailableSlots <= 0)
                        continue;

                    foundLobby = lobby;
                    break;
                }
            }
            if (foundLobby != null)
            {
                Debug.Log(
                    "Found existing quick match lobby. Joining..."
                );

                currentLobby =
                    await LobbyService.Instance.JoinLobbyByIdAsync(
                        foundLobby.Id
                    );

                isHost = false;

                Debug.Log(
                    "Joined lobby: " +
                    currentLobby.Id
                );
            }
            else
            {
                Debug.Log(
                    "No quick match lobby found. Creating new lobby..."
                );
                await CreateQuickMatchLobby();

                isHost = true;

                await CreateRelay();

                Debug.Log(
                    "Created lobby: " +
                    currentLobby.Id
                );
            }

            await LobbyService.Instance.UpdatePlayerAsync(
    currentLobby.Id,
    Unity.Services.Authentication.AuthenticationService.Instance.PlayerId,
    new UpdatePlayerOptions
    {
        Data = new Dictionary<string, PlayerDataObject>
        {
            {
                "name",
                new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerName
                )
            },
            {
                "avatar",
                new PlayerDataObject(
                    PlayerDataObject.VisibilityOptions.Member,
                    ProfileData.PlayerAvatarIndex.ToString()
                )
            }
        }
    }
);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(
                "Quick Match failed: " +
                e.Message
            );

            OnError?.Invoke(e.Message);
        }
    }

    private async Task CreateQuickMatchLobby()
    {
        CreateLobbyOptions options =
            new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>()
                {
                {
                    LOBBY_MODE_KEY,
                    new DataObject(
                        DataObject.VisibilityOptions.Public,
                        QUICKMATCH_MODE
                    )
                }
                }
            };

        currentLobby =
            await LobbyService.Instance.CreateLobbyAsync(
                "QuickMatch",
                requiredPlayers,
                options
            );

        Debug.Log(
            "Quick Match Lobby Created: " +
            currentLobby.Id
        );
    }

    [Header("Config")]
    [SerializeField] private int requiredPlayers = 4;
    [SerializeField] private float searchTimeout = 20f;
    [SerializeField] private float pollInterval = 3f;
    [SerializeField] private AvatarDatabase avatarDatabase;
    [Header("Events")]
    public System.Action<string, int, int> OnPlayerJoined;
    public System.Action<int> OnPlayerLeft;
    public System.Action OnMatchFound;
    public System.Action OnTimeout;
    public System.Action<string> OnError;
    public System.Action OnLobbyUpdated;

    [Header("Runtime")]
    private Lobby currentLobby;
    private int previousPlayerCount = 0;
    private bool firstRefresh;
    private bool isSearching;
    private float searchTimer;
    private float pollTimer;
    private bool isHost;
    private string relayCode;
    public bool IsSearching => isSearching;

    public float RemainingSeconds =>
        Mathf.Max(0f, searchTimeout - searchTimer);

    public AvatarDatabase AvatarDatabase => avatarDatabase;
    public List<LobbyPlayerInfo> GetLobbyPlayers()
    {
        List<LobbyPlayerInfo> list =
            new List<LobbyPlayerInfo>();

        if (currentLobby == null)
            return list;

        string localId =
            Unity.Services.Authentication
                .AuthenticationService.Instance.PlayerId;

        foreach (var player in currentLobby.Players)
        {
            bool isLocal =
                player.Id == localId;

            string playerName = "Player";
            int avatarIndex = 0;

            if (isLocal)
            {
                playerName =
                    string.IsNullOrEmpty(ProfileData.PlayerName)
                    ? "Player"
                    : ProfileData.PlayerName;

                avatarIndex =
                    ProfileData.PlayerAvatarIndex;
            }
            else
            {
                if (player.Data != null &&
                    player.Data.ContainsKey("name"))
                {
                    playerName =
                        player.Data["name"].Value;
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

            list.Add(
    new LobbyPlayerInfo
    {
        PlayerName = playerName,
        AvatarIndex = avatarIndex,
        IsLocal = isLocal
    }
);
        }

        list.Sort((a, b) =>
{
    if (a.IsLocal == b.IsLocal)
        return 0;

    return a.IsLocal ? -1 : 1;
});

        return list;
    }

    private async Task CreateRelay()
    {
        try
        {
            Debug.Log("Creating Relay Allocation...");

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(
                    requiredPlayers
                );

            relayCode =
                await RelayService.Instance.GetJoinCodeAsync(
                    allocation.AllocationId
                );

            Debug.Log(
                "Relay Join Code: " +
                relayCode
            );

            // Save relay code inside lobby
            await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                    {
    RELAY_CODE_KEY,
    new DataObject(
        DataObject.VisibilityOptions.Member,
        relayCode
    )
}
                    }
                }
            );

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if (!NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Starting Host...");

                NetworkManager.Singleton.StartHost();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "CreateRelay Failed: " +
                e.Message
            );

            OnError?.Invoke(e.Message);
        }
    }

    private async Task RefreshLobby()
    {
        try
        {
            int oldCount = previousPlayerCount;
            if (currentLobby == null)
                return;

            currentLobby =
                await LobbyService.Instance.GetLobbyAsync(
                    currentLobby.Id
                );
            int newCount = currentLobby.Players.Count;
            if (firstRefresh)
            {
                oldCount = newCount;
                firstRefresh = false;
            }
            if (newCount > oldCount)
            {
                OnPlayerJoined?.Invoke(
                    "Player Joined",
                    newCount,
                    requiredPlayers
                );
            }
            previousPlayerCount = newCount;
            OnLobbyUpdated?.Invoke();
            CheckMatchReady();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(
                "Lobby Refresh Failed: " +
                e.Message
            );

            OnError?.Invoke(e.Message);
        }
    }

    private async void CheckMatchReady()
    {
        if (currentLobby == null)
            return;

        if (currentLobby.Players.Count < requiredPlayers)
            return;

        Debug.Log("Required players found");
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

        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.InitializeAndDeal();
        }
        else
        {
            Debug.LogError(
                "NetworkGameManager Instance is NULL"
            );
        }

        return Task.CompletedTask;
    }
    private async Task StartClientFlow()
    {
        Debug.Log("Starting Client Flow");
        await JoinRelay();
    }

    private async Task JoinRelay()
    {
        try
        {
            Debug.Log("Joining Relay...");

            if (currentLobby == null)
            {
                Debug.LogError("Current Lobby is NULL");
                return;
            }

            if (!currentLobby.Data.ContainsKey("relayCode"))
            {
                Debug.LogError("Relay code missing in lobby data");
                return;
            }

            relayCode =
                currentLobby.Data["relayCode"].Value;

            Debug.Log(
                "Relay Code: " +
                relayCode
            );

            JoinAllocation allocation =
                await RelayService.Instance
                    .JoinAllocationAsync(relayCode);

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
                Debug.Log("Starting Client...");

                NetworkManager.Singleton.StartClient();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "Join Relay Failed: " +
                e.Message
            );

            OnError?.Invoke(
                "Failed to join relay"
            );
        }
    }

    private System.Collections.IEnumerator LeaveLobbyAndCleanup()
    {
        Debug.Log("Leaving matchmaking lobby");

        if (currentLobby != null)
        {
            var task =
                LobbyService.Instance.RemovePlayerAsync(
                    currentLobby.Id,
                    Unity.Services.Authentication
                        .AuthenticationService.Instance.PlayerId
                );

            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        currentLobby = null;

        relayCode = string.Empty;

        isHost = false;

        searchTimer = 0f;

        yield return null;
    }

    public void Cancel()
    {
        isSearching = false;

        StartCoroutine(LeaveLobbyAndCleanup());
    }
}

