using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
        QuickMatchService.Instance.OnRosterChanged += RenderRoster;
        QuickMatchService.Instance.OnMatchFound += HandleMatchFound;
    }
    private void OnDisable()
    {
        if (QuickMatchService.Instance == null)
            return;
        QuickMatchService.Instance.OnTimeout -= HandleTimeout;
        QuickMatchService.Instance.OnRosterChanged -= RenderRoster;
        QuickMatchService.Instance.OnMatchFound -= HandleMatchFound;
    }

    public void StartSearching()
    {
        panel.SetActive(true);
        if (statusText != null)
        {
            statusText.text = "Searching for Players...";
        }
    }

    IEnumerator HandleNoPlayersFound()
    {
        statusText.text = "Matchmaking timed out";

        timerText.text = "Switching to Offline Mode...";

        yield return new WaitForSeconds(1.5f);

        panel.SetActive(false);

        GameModeManager.isOnlineMode = false;

        OfflineFallback.Enter();
    }

    private void HandleTimeout()
    {
        StartCoroutine(HandleNoPlayersFound());
    }
    private void HandleMatchFound()
    {
        if (statusText != null)
        {
            statusText.text = "Match Found!\nStarting Game...";
        }
    }

    private void RenderRoster(IReadOnlyList<QuickMatchService.MatchPlayerInfo> roster)
    {
        if (roster == null)
            return;
        // Clear all slots first
        for (int i = 0; i < playerSlots.Length; i++)
        {
            playerSlots[i].SetProfile(
                "Waiting...",
                null);
        }
        // Fill slots
        for (int i = 0; i < roster.Count; i++)
        {
            if (i >= playerSlots.Length)
                break;
            Sprite avatar = null;
            if (
                QuickMatchService.Instance != null &&
                QuickMatchService.Instance.AvatarDatabase != null &&
                roster[i].AvatarIndex >= 0 &&
                roster[i].AvatarIndex <
                QuickMatchService.Instance
                    .AvatarDatabase
                    .avatarSprites.Length)
            {
                avatar =
                    QuickMatchService.Instance
                        .AvatarDatabase
                        .avatarSprites[
                            roster[i].AvatarIndex];
            }

            playerSlots[i].SetProfile(
                roster[i].PlayerName,
                avatar);
            Debug.Log("Slot " + i + " -> " + roster[i].PlayerName);
        }
        if (statusText != null)
        {
            statusText.text =
                roster.Count +
                "/" +
                playerSlots.Length +
                " Players Found";
        }
    }

    public void StopSearching()
    {
        // searching = false;
        panel.SetActive(false);
    }
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