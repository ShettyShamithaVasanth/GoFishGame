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

        int activePlayers;

        if (GameModeManager.isOnlineMode)
        {
            activePlayers = 0;

            foreach (var p in players)
            {
                if (p != null)
                    activePlayers++;
            }
        }
        else
        {
            activePlayers =
                ModeSelectionController.selectedPlayers;
        }

        // sort players by score (highest first)
        var sortedPlayers = players
    .OrderByDescending(p => p.Score)
    .ThenBy(p => p.LastBookTurn)
    .Take(activePlayers)
    .ToArray();

        // first hide all rows
        for (int i = 0; i < playerNames.Length; i++)
        {
            playerNames[i].transform.parent.gameObject.SetActive(false);
        }
        Debug.Log("=== GAME OVER UI SORTED PLAYERS ===");

        // now show only required rows
        for (int i = 0; i < sortedPlayers.Length; i++)
        {
            playerNames[i].transform.parent.gameObject.SetActive(true);

            playerNames[i].text = sortedPlayers[i].PlayerName;
            playerScores[i].text = "Score: " + sortedPlayers[i].Score;
            Debug.Log("UI Slot: " + i +
          " | PlayerID: " + sortedPlayers[i].PlayerID +
          " | Name: " + sortedPlayers[i].PlayerName +
          " | AvatarIndex: " + sortedPlayers[i].AvatarIndex +
          " | Score: " + sortedPlayers[i].Score);

            int avatarIndex = sortedPlayers[i].AvatarIndex;

            if (avatarIndex >= 0 && avatarIndex < avatarSprites.Length)
            {
                playerAvatarImages[i].sprite = avatarSprites[avatarIndex];
            }

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

    // [System.Obsolete]
    public void HidePanel()
    {
        gameOverPanel.SetActive(false);
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}