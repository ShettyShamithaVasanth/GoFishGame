using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Linq;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public GameObject gameOverPanel;
    public TextMeshProUGUI[] playerNames;   // size = 4
    public TextMeshProUGUI[] playerScores;  // size = 4
    public Image[] playerAvatarImages;
    public Sprite[] avatarSprites;
    public Image[] playerRowImages;
    public static bool skipMenuOnReload = false;

    public void ShowGameOver(Player[] players)
    {
        gameOverPanel.SetActive(true);

        int activePlayers = ModeSelectionController.selectedPlayers;

        // sort players by score (highest first)
        var sortedPlayers = players
            .OrderByDescending(p => p.Score)
            .Take(activePlayers)   // ⭐ only keep active players
            .ToArray();

        // first hide all rows
        for (int i = 0; i < playerNames.Length; i++)
        {
            playerNames[i].transform.parent.gameObject.SetActive(false);
        }

        // now show only required rows
        for (int i = 0; i < sortedPlayers.Length; i++)
        {
            playerNames[i].transform.parent.gameObject.SetActive(true);

            playerNames[i].text = sortedPlayers[i].PlayerName;
            playerScores[i].text = "Score: " + sortedPlayers[i].Score;

            int id = sortedPlayers[i].PlayerID;
            playerAvatarImages[i].sprite = avatarSprites[id];

            if (sortedPlayers[i].IsHuman)
            {
                playerRowImages[i].color = new Color(0.75f, 0.75f, 0.75f, 1f);
            }
            else
            {
                playerRowImages[i].color = Color.white;
            }
        }
    }

    public void PlayAgain()
    {
        Debug.Log("PLAY AGAIN BUTTON PRESSED");

        skipMenuOnReload = true;

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}