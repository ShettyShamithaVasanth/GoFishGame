using UnityEngine;
using UnityEngine.SceneManagement;

public class ConfirmQuitManager : MonoBehaviour
{
    public GameObject confirmPanel;

    public GameObject pausePanel;
    public GameObject gameOverPanel;

    private string sourcePanel = "";

    public void OpenFromPause()
    {
        sourcePanel = "pause";

        pausePanel.SetActive(false);
        confirmPanel.SetActive(true);
    }

    public void OpenFromGameOver()
    {
        sourcePanel = "gameover";

        gameOverPanel.SetActive(false);
        confirmPanel.SetActive(true);
    }

    public void CancelQuit()
    {
        confirmPanel.SetActive(false);

        if (sourcePanel == "pause")
        {
            pausePanel.SetActive(true);
        }
        else if (sourcePanel == "gameover")
        {
            gameOverPanel.SetActive(true);
        }
    }

    public void ConfirmQuit()
    {
        Time.timeScale = 1f;

        if (GameModeManager.isOnlineMode)
        {
            confirmPanel.SetActive(false);
            if (pausePanel != null)
                pausePanel.SetActive(false);
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.QuitFromGame();
            }
        }
        else
        {
            GameOverUI.skipMenuOnReload = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}