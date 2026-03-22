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

        GameOverUI.skipMenuOnReload = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}