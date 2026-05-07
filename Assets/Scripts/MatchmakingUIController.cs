using UnityEngine;
using TMPro;
using System.Collections;

public class MatchmakingUIController : MonoBehaviour
{
    public static MatchmakingUIController Instance;

    public GameObject panel;
    public TextMeshProUGUI timerText;

    float timer = 20f;
    bool searching = false;

    void Awake()
    {
        Instance = this;
    }

    // [System.Obsolete]
    public void StartSearching()
    {
        panel.SetActive(true);
        timer = 20f;
        searching = true;
        StartCoroutine(SearchTimer());
    }

    // [System.Obsolete]
    IEnumerator SearchTimer()
    {
        while (timer > 0 && searching)
        {
            timer -= Time.deltaTime;
            timerText.text = "TIMER : " + Mathf.Ceil(timer) + "s";
            yield return null;
        }

        if (timer <= 0)
        {
            Debug.Log("No players found → fallback");
            StartCoroutine(HandleNoPlayersFound());
        }
    }

    // [System.Obsolete]
    IEnumerator HandleNoPlayersFound()
    {
        searching = false;
        timerText.text = "No players found. Starting offline game...";
        yield return new WaitForSeconds(5f);
        // hide matchmaking panel
        panel.SetActive(false);
        // open mode selection panel
        var menu = FindAnyObjectByType<MenuController>();

        if (menu != null)
        {
            menu.MenuUI.SetActive(false);
            menu.LoadingPanel.SetActive(false);
            menu.ModeSelectionPanel.SetActive(true);
            //set header text online
            var controller = menu.ModeSelectionPanel.GetComponent<ModeSelectionController>();
            if (controller != null)
            {
                controller.SetHeader("Online Mode");
            }
        }
    }

    public void StopSearching()
    {
        searching = false;
        panel.SetActive(false);
    }

    // [System.Obsolete]
    public void OnCancelClicked()
    {
        Debug.Log("Matchmaking cancelled by user");
        StopSearching();
        // Resst mode
        GameModeManager.isOnlineMode = false;
        // go back to menu scene
        var menu = FindAnyObjectByType<MenuController>();
        if (menu != null)
        {
            menu.MenuUI.SetActive(true);
        }
        // hide matchmaking UI
        panel.SetActive(false);
    }

    
}