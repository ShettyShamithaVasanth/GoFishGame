using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    public GameObject gameScenePanel;
    public GameManager gameManager;

    public void ShowPanel()
    {
        gameScenePanel.SetActive(true);
    }

    public void PauseGame()
    {
        Debug.Log("Pause button clicked");
    }

    public void NextTurn()
{
    Debug.Log("Next Turn button clicked");

    gameManager.StartNextTurnFromButton();
}
}