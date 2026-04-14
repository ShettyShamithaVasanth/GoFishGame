using UnityEngine;

public static class ProfileSaveSystem
{
    private const string NAME_KEY = "PLAYER_NAME";
    private const string AVATAR_KEY = "PLAYER_AVATAR_INDEX";

    // 💾 SAVE DATA
    public static void SaveProfile(string name, int avatarIndex)
    {
        PlayerPrefs.SetString(NAME_KEY, name);
        PlayerPrefs.SetInt(AVATAR_KEY, avatarIndex);
        PlayerPrefs.Save();

        Debug.Log("Profile Saved");
    }

    // 📥 LOAD NAME
    public static string LoadName()
    {
        return PlayerPrefs.GetString(NAME_KEY, "You");
    }

    // 📥 LOAD AVATAR INDEX
    public static int LoadAvatarIndex()
    {
        return PlayerPrefs.GetInt(AVATAR_KEY, 0);
    }
}