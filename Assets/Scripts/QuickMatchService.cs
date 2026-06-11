using UnityEngine;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;

public class QuickMatchService : MonoBehaviour
{
    public static QuickMatchService Instance;
    private void Awake()
    {
        Instance = this;
    }
    private void Update()
    {
        if (!isSearching)
            return;

        searchTimer += Time.deltaTime;

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
    public void FindMatch()
    {
        Debug.Log("QuickMatchService FindMatch called");

        isSearching = true;
        searchTimer = 0f;
    }

    [Header("Config")]
    [SerializeField] private int requiredPlayers = 4;
    [SerializeField] private float searchTimeout = 20f;
    // [SerializeField] private float pollInterval = 2.5f;
    [SerializeField] private AvatarDatabase avatarDatabase;
    [Header("Events")]
    public System.Action<string, int, int> OnPlayerJoined;
    public System.Action<int> OnPlayerLeft;
    public System.Action OnMatchFound;
    public System.Action OnTimeout;
    public System.Action<string> OnError;

    [Header("Runtime")]
    private Lobby currentLobby;
    private bool isSearching;
    private float searchTimer;
    // [SerializeField] private float pollTimer;
    private bool isHost;
    private string relayCode;
    public bool IsSearching => isSearching;

    public float RemainingSeconds =>
        Mathf.Max(0f, searchTimeout - searchTimer);


    private async void CheckMatchReady()
    {
        if (currentLobby == null)
            return;

        if (currentLobby.Players.Count < requiredPlayers)
            return;

        Debug.Log("Required players found");

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

        return Task.CompletedTask;
    }
    private Task StartClientFlow()
    {
        Debug.Log("Starting Client Flow");
        return Task.CompletedTask;
    }
    private System.Collections.IEnumerator LeaveLobbyAndCleanup()
    {
        Debug.Log("Leaving matchmaking lobby");

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

