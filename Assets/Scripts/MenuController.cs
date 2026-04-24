using UnityEngine;
using System.Collections;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    public GameObject MenuUI;
    public GameObject LoadingPanel;
    public GameObject ModeSelectionPanel;
    public GameObject GameManager; // your gameplay system
    public UIPlayer[] uiPlayers;
    public GameSceneUI gameSceneUI;

    public GameObject TopPlayer;
    public GameObject BottomPlayer;
    public GameObject LeftPlayer;
    public GameObject RightPlayer;
    public GameObject DeckPosition;

    void Start()
    {
        ModeSelectionPanel.SetActive(false); // ⭐ hide initially

        if (GameOverUI.skipMenuOnReload)
        {
            GameOverUI.skipMenuOnReload = false;

            MenuUI.SetActive(false);
            LoadingPanel.SetActive(false);

            GameManager.SetActive(true);

            if (gameSceneUI != null)
            {
                gameSceneUI.ShowPanel();
            }
        }
    }
    public void PlayOffline()
    {
        MenuUI.SetActive(false);
        LoadingPanel.SetActive(true);

        StartCoroutine(ShowModeSelectionAfterLoading("Offline"));
    }

    IEnumerator ShowModeSelectionAfterLoading(string mode)
    {
        yield return new WaitForSeconds(3f);

        LoadingPanel.SetActive(false);

        // ⭐ show ModeSelectionPanel instead of starting game
        ModeSelectionPanel.SetActive(true);

        // ⭐ change header text
        ModeSelectionController controller =
            ModeSelectionPanel.GetComponent<ModeSelectionController>();

        if (controller != null)
            controller.SetHeader(mode);
    }

    // ⭐ not implemented modes
    public void PlayOnline()
    {
        Debug.Log("play Online mode.");
        // set mode
        GameModeManager.isOnlineMode = true;
        // load matchmaking scene
        SceneManager.LoadScene("GameScene");
    }

    public void PlayWithFriends()
    {
        Debug.Log("Play with Friends not implemented yet.");
    }
    public void ContinueGame()
    {
        ModeSelectionPanel.SetActive(false);

        int players = ModeSelectionController.selectedPlayers;

        // Hide all first
        TopPlayer.SetActive(false);
        BottomPlayer.SetActive(false);
        LeftPlayer.SetActive(false);
        RightPlayer.SetActive(false);

        // ⭐ Enable players based on selection

        if (players == 2)
        {
            // Human vs Top AI
            BottomPlayer.SetActive(true);
            TopPlayer.SetActive(true);
        }
        else if (players == 3)
        {
            // Human + Left + Right
            BottomPlayer.SetActive(true);
            LeftPlayer.SetActive(true);
            RightPlayer.SetActive(true);
        }
        else if (players == 4)
        {
            // All players
            BottomPlayer.SetActive(true);
            TopPlayer.SetActive(true);
            LeftPlayer.SetActive(true);
            RightPlayer.SetActive(true);
        }

        DeckPosition.SetActive(true);

        GameManager.SetActive(true);

        foreach (UIPlayer p in uiPlayers)
        {
            p.canInteract = true;
        }

        if (gameSceneUI != null)
        {
            gameSceneUI.ShowPanel();
        }
    }


}