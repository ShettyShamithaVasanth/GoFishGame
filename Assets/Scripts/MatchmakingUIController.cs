using UnityEngine;
using TMPro;
using System.Collections;

public class MatchmakingUIController : MonoBehaviour
{
    public static MatchmakingUIController Instance;

    public GameObject panel;
    public PlayerProfileUI[] playerSlots;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI timerText;

    // float timer = 20f;
    // bool searching = false;

    void Awake()
    {
        Instance = this;
    }
    private void Update()
    {
        if (QuickMatchService.Instance == null)
            return;

        if (!QuickMatchService.Instance.IsSearching)
            return;

        timerText.text =
            "TIMER : " +
            Mathf.Ceil(
                QuickMatchService.Instance.RemainingSeconds
            ) +
            "s";
    }

    private void OnEnable()
    {
        if (QuickMatchService.Instance == null)
            return;

        QuickMatchService.Instance.OnTimeout += HandleTimeout;

        QuickMatchService.Instance.OnPlayerJoined += HandlePlayerJoined;

        QuickMatchService.Instance.OnPlayerLeft += HandlePlayerLeft;

        QuickMatchService.Instance.OnMatchFound += HandleMatchFound;
        QuickMatchService.Instance.OnLobbyUpdated += RenderSlots;
    }

    private void OnDisable()
    {
        if (QuickMatchService.Instance == null)
            return;

        QuickMatchService.Instance.OnTimeout -= HandleTimeout;

        QuickMatchService.Instance.OnPlayerJoined -= HandlePlayerJoined;

        QuickMatchService.Instance.OnPlayerLeft -= HandlePlayerLeft;

        QuickMatchService.Instance.OnMatchFound -= HandleMatchFound;
        QuickMatchService.Instance.OnLobbyUpdated -= RenderSlots;
    }

    // [System.Obsolete]
    public void StartSearching()
    {
        panel.SetActive(true);
    }

    IEnumerator HandleNoPlayersFound()
    {
        timerText.text =
            "No players found. Switching to offline...";

        yield return new WaitForSeconds(1.5f);

        panel.SetActive(false);

        GameModeManager.isOnlineMode = false;

        OfflineFallback.Enter();
    }

    private void HandleTimeout()
    {
        StartCoroutine(HandleNoPlayersFound());
    }

    private void HandlePlayerJoined(
    string playerName,
    int avatarIndex,
    int playerCount)
    {
        RenderSlots();
    }

    private void HandlePlayerLeft(
        int playerCount)
    {
        RenderSlots();
    }

    private void HandleMatchFound()
    {
        StopSearching();
    }

    public void StopSearching()
    {
        // searching = false;
        panel.SetActive(false);
    }

    private void RenderSlots()
    {
        if (QuickMatchService.Instance == null)
            return;

        var players = QuickMatchService.Instance.GetLobbyPlayers();

        if (players == null)
            return;

        // Clear all slots first
        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] != null)
            {
                playerSlots[i].SetProfile("Waiting...", null);
            }
        }

        // Fill available players
        for (int i = 0; i < players.Count; i++)
        {
            if (i >= playerSlots.Length)
                break;

            if (playerSlots[i] == null)
                continue;

            Sprite avatarSprite = null;

            if (players[i].AvatarIndex >= 0 &&
                QuickMatchService.Instance.AvatarDatabase != null &&
                players[i].AvatarIndex <
                QuickMatchService.Instance.AvatarDatabase.avatarSprites.Length)
            {
                avatarSprite =
                    QuickMatchService.Instance
                        .AvatarDatabase
                        .avatarSprites[players[i].AvatarIndex];
            }

            playerSlots[i].SetProfile(
                players[i].PlayerName,
                avatarSprite
            );
        }
        if (statusText != null)
        {
            statusText.text =
                players.Count +
                "/" +
                playerSlots.Length +
                " players found";
        }
    }

    // [System.Obsolete]
    public void OnCancelClicked()
    {
        Debug.Log("Matchmaking cancelled by user");
        if (QuickMatchService.Instance != null)
        {
            QuickMatchService.Instance.Cancel();
        }
        StopSearching();
        // Resst mode
        GameModeManager.isOnlineMode = false;
        // go back to menu scene
        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            if (menu.MenuUI != null)
            {
                menu.MenuUI.SetActive(true);
            }
        }
        // hide matchmaking UI
        panel.SetActive(false);
    }


}