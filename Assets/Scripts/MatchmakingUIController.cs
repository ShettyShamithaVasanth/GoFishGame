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
        if (QuickMatchService.Instance != null)
        {
            QuickMatchService.Instance.OnTimeout += HandleTimeout;
        }
    }

    private void OnDisable()
    {
        if (QuickMatchService.Instance != null)
        {
            QuickMatchService.Instance.OnTimeout -= HandleTimeout;
        }
    }

    // [System.Obsolete]
    public void StartSearching()
    {
        panel.SetActive(true);
    }

    // [System.Obsolete]
    // IEnumerator SearchTimer()
    // {
    //     while (timer > 0 && searching)
    //     {
    //         timer -= Time.deltaTime;
    //         timerText.text = "TIMER : " + Mathf.Ceil(timer) + "s";
    //         yield return null;
    //     }

    //     if (timer <= 0)
    //     {
    //         searching = false;
    //     }
    // }

    // [System.Obsolete]
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

    public void StopSearching()
    {
        // searching = false;
        panel.SetActive(false);
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