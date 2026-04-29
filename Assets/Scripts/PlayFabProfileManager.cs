using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;

public class PlayFabProfileManager : MonoBehaviour
{
    public static PlayFabProfileManager Instance;

    void Awake()
    {
        Instance = this;
    }

    // 🔥 SAVE PROFILE TO CLOUD
    public void SaveProfile(string name, int avatarIndex)
    {
        var data = new Dictionary<string, string>()
{
    { "PlayerName", name },
    { "AvatarIndex", avatarIndex.ToString() },

    // ⭐ NEW STATS
    { "GamesPlayed", ProfileData.GamesPlayed.ToString() },
    { "GamesWon", ProfileData.GamesWon.ToString() }
};

        var request = new UpdateUserDataRequest
        {
            Data = data
        };

        PlayFabClientAPI.UpdateUserData(request,
            result => Debug.Log("✅ Cloud Save SUCCESS"),
            error => Debug.LogError("❌ Cloud Save FAILED"));
    }

    // 🔥 LOAD PROFILE FROM CLOUD
    public void LoadProfile()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn())
        {
            Debug.LogWarning("⚠️ Not logged in yet. Skipping LoadProfile.");
            return;
        }
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
        result =>
        {
            if (result.Data != null && result.Data.ContainsKey("PlayerName"))
            {
                // ⭐ LOAD STATS (SAFE CHECK)
                if (result.Data.ContainsKey("GamesPlayed"))
                    ProfileData.GamesPlayed = int.Parse(result.Data["GamesPlayed"].Value);

                if (result.Data.ContainsKey("GamesWon"))
                    ProfileData.GamesWon = int.Parse(result.Data["GamesWon"].Value);
                string name = result.Data["PlayerName"].Value;
                int avatarIndex = int.Parse(result.Data["AvatarIndex"].Value);

                Debug.Log("✅ Cloud Data Loaded");

                // 🔥 UPDATE GLOBAL DATA
                ProfileData.PlayerName = name;

                // 🔥 APPLY AVATAR FROM MENU LIST
                var menu = FindFirstObjectByType<MainMenuProfileController>();

                if (menu != null && menu.avatarImages.Count > avatarIndex)
                {
                    ProfileData.PlayerAvatarIndex = avatarIndex;
                }

                // 🔥 UPDATE UI
                if (menu != null)
                {
                    menu.UpdateUI(); // ⭐ SINGLE SOURCE UPDATE
                }
            }
            else
            {
                Debug.Log("No cloud data found (first time user)");
            }
        },
        error => Debug.LogError("❌ Cloud Load FAILED"));

    }
}