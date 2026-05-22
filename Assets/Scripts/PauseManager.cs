using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    public GameObject pausePanel;

    public void PauseGame()
    {
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        if (GameModeManager.isOnlineMode)
        {
            pausePanel.SetActive(false);
            LobbyManager.Instance?.QuitFromGame();
        }
        else
        {
            GameOverUI.skipMenuOnReload = false;
            SceneManager.LoadScene( SceneManager.GetActiveScene().name);
        }
    }
}