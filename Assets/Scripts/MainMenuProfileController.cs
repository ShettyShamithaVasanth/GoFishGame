using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuProfileController : MonoBehaviour
{
    public TMPro.TextMeshProUGUI statsNameText;
    public UnityEngine.UI.Image statsAvatarImage;
    public GameObject statsPanel;
    public GameObject menuBackground; // 🔥 ADD THIS
    public List<Image> avatarImages; // all avatars (same list as edit panel)
    public Image topProfileAvatar;
    public AvatarDatabase avatarDatabase;
    public TextMeshProUGUI topProfileName;
    public TextMeshProUGUI profileIDText;
    public TextMeshProUGUI gamesPlayedText;
    public TextMeshProUGUI gamesWonText;


    void Start()
    {
        LoadProfile();
    }
    void LoadProfile()
    {
        // 🔥 LOAD LOCAL FIRST (FAST)
        string savedName = ProfileSaveSystem.LoadName();
        int avatarIndex = ProfileSaveSystem.LoadAvatarIndex();

        ProfileData.PlayerName = savedName;

        if (avatarImages != null && avatarImages.Count > avatarIndex)
        {
            ProfileData.PlayerAvatarIndex = avatarIndex;
        }

        // 🔥 UPDATE UI IMMEDIATELY
        UpdateUI();

        // 🔥 THEN LOAD CLOUD (IMPORTANT)
        // PlayFabProfileManager.Instance.LoadProfile();
    }

    public void UpdateUI()
    {
        if (topProfileName != null)
            topProfileName.text = ProfileData.PlayerName;

        if (topProfileAvatar != null && avatarImages != null && avatarImages.Count > ProfileData.PlayerAvatarIndex)
            topProfileAvatar.sprite = avatarImages[ProfileData.PlayerAvatarIndex].sprite;

        // ⭐ ALSO UPDATE STATS PANEL
        if (statsNameText != null)
            statsNameText.text = ProfileData.PlayerName;

        if (statsAvatarImage != null && avatarImages != null && avatarImages.Count > ProfileData.PlayerAvatarIndex)
            statsAvatarImage.sprite = avatarImages[ProfileData.PlayerAvatarIndex].sprite;
    }

    // 🔥 OPEN STATS PANEL
    public void OpenProfilePanel()
    {
        Debug.Log("Profile Button Clicked");

        if (statsPanel != null)
            statsPanel.SetActive(true);

        if (menuBackground != null)
            menuBackground.SetActive(false);

        // ⭐ SET PROFILE ID
        if (profileIDText != null)
            profileIDText.text = "Profile ID: " + ProfileData.PlayFabID;

        // ⭐ UPDATE STATS UI
        if (gamesPlayedText != null)
            gamesPlayedText.text = ProfileData.GamesPlayed.ToString();

        if (gamesWonText != null)
            gamesWonText.text = ProfileData.GamesWon.ToString();
    }

    // 🔥 CLOSE STATS PANEL
    public void CloseProfilePanel()
    {
        Debug.Log("Close Button Clicked");

        if (statsPanel != null)
            statsPanel.SetActive(false);

        if (menuBackground != null)
            menuBackground.SetActive(true);
    }
}