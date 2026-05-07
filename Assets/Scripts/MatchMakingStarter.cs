using UnityEngine;

public class MatchmakingStarter : MonoBehaviour
{
    public PlayerProfileUI myProfileUI;

    // [System.Obsolete]
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
        int index = ProfileData.PlayerAvatarIndex;
        Sprite avatar = null;

        // get AvatarDatabase from LobbyManager
        var db = FindAnyObjectByType<LobbyManager>()?.avatarDatabase;

        if (db != null &&
            db.avatarSprites != null &&
            index >= 0 &&
            index < db.avatarSprites.Length)
        {
            avatar = db.avatarSprites[index];
        }
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