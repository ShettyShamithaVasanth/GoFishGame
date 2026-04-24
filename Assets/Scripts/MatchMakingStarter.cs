using UnityEngine;

public class MatchmakingStarter : MonoBehaviour
{
    public PlayerProfileUI myProfileUI;

    void Start()
    {
        // if not online mode, do nothing
        if (!GameModeManager.isOnlineMode)
        {
             return;
        }
        Debug.Log("Online mode detected → starting matchmaking");
           
        //Show matchmaking UI
        if (MatchmakingUIController.Instance != null)
        {
            MatchmakingUIController.Instance.StartSearching();
        }

        //Get saved profile data
        string playerName = ProfileData.PlayerName;
        Sprite avatar = ProfileData.PlayerAvatar;

        //Safety check
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }

        //Apply to Player_0 UI
        if (myProfileUI != null)
        {
            myProfileUI.SetProfile(playerName, avatar);
        }
        GameModeManager.isOnlineMode = false;
    }
}