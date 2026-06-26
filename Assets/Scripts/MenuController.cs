using UnityEngine;
using System.Collections;
using Unity.VectorGraphics;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor;
using PlayFab.MultiplayerModels;

public class MenuController : MonoBehaviour
{
    public GameObject MenuUI;
    public GameObject LoadingPanel;
    public GameObject ModeSelectionPanel;
    public GameObject MenuBackground;
    public GameObject GameManager; // your gameplay system
    public UIPlayer[] uiPlayers;
    public GameSceneUI gameSceneUI;

    public GameObject TopPlayer;
    public GameObject BottomPlayer;
    public GameObject LeftPlayer;
    public GameObject RightPlayer;
    public GameObject DeckPosition;
    public GameObject FriendsPanel;
    public GameObject GameRulesPanel;
    public GameObject EnteringGamePanel;
    public TMP_InputField roomCodeInput;
    [SerializeField] private MenuAnimationController menuAnimationController;

    void Start()
    {
        ModeSelectionPanel.SetActive(false); // ⭐ hide initially
        EnteringGamePanel.SetActive(false);
        if (MenuBackground != null)
        {
            MenuBackground.SetActive(true);
        }
        if (menuAnimationController != null)
        {
            menuAnimationController.StartAnimations();
        }
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
        menuAnimationController?.StopAnimations();
        MenuUI.SetActive(false);
        LoadingPanel.SetActive(true);

        StartCoroutine(ShowModeSelectionAfterLoading("Offline"));
    }

    IEnumerator ShowModeSelectionAfterLoading(string mode)
    {
        yield return new WaitForSeconds(3f);

        ShowModeSelection(mode);
    }

    // ⭐ not implemented modes
    public void PlayOnline()
    {
        menuAnimationController?.StopAnimations();
        Debug.Log("play Online mode.");
        // set mode
        GameModeManager.isOnlineMode = true;
        StartCoroutine(PlayOnlineRoutine());
    }

    private IEnumerator PlayOnlineRoutine()
    {
        MenuUI.SetActive(false);
        EnteringGamePanel?.SetActive(true);

        yield return new WaitForSeconds(2.5f);

        SceneManager.LoadScene("GameScene");
    }

    public void PlayWithFriends()
    {
        menuAnimationController?.StopAnimations();
        Debug.Log("Play with Friends clicked.");
        MenuUI.SetActive(false);
        FriendsPanel.SetActive(true);
    }

    public void CloseFriendsPanel()
    {
        FriendsPanel.SetActive(false);
        MenuUI.SetActive(true);
        menuAnimationController?.StartAnimations();
    }

    public void ShowModeSelection(string mode)
    {
        if (MenuUI != null)
            MenuUI.SetActive(false);

        if (LoadingPanel != null)
            LoadingPanel.SetActive(false);

        if (MenuBackground != null)
            MenuBackground.SetActive(false);

        if (ModeSelectionPanel != null)
        {
            ModeSelectionPanel.SetActive(true);

            ModeSelectionController controller =
                ModeSelectionPanel.GetComponent<ModeSelectionController>();

            if (controller != null)
            {
                controller.SetHeader(mode);
            }
        }
    }
    public void ContinueGame()
    {
        if (MenuUI != null)
            MenuUI.SetActive(false);

        if (LoadingPanel != null)
            LoadingPanel.SetActive(false);
        if (MenuBackground != null)
            MenuBackground.SetActive(false);

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
        GameManager.GetComponent<GameManager>().SetupGame();
        if (gameSceneUI != null)
        {
            gameSceneUI.ShowPanel();
        }
    }

    public void JoinRoom()
    {
        string code = roomCodeInput.text;
        if (string.IsNullOrEmpty(code))
        {
            Debug.Log("Please enter a room code to join.");
            return;
        }
        LobbyManager.Instance.JoinLobby(code);
    }

    public void OpenGameRules()
    {
        GameRulesPanel.SetActive(true);
    }
    public void CloseGameRules()
    {
        GameRulesPanel.SetActive(false);
    }


}