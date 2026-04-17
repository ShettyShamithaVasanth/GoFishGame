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
            ProfileData.PlayerAvatar = avatarImages[avatarIndex].sprite;
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

        if (topProfileAvatar != null && ProfileData.PlayerAvatar != null)
            topProfileAvatar.sprite = ProfileData.PlayerAvatar;

        // ⭐ ALSO UPDATE STATS PANEL
        if (statsNameText != null)
            statsNameText.text = ProfileData.PlayerName;

        if (statsAvatarImage != null && ProfileData.PlayerAvatar != null)
            statsAvatarImage.sprite = ProfileData.PlayerAvatar;
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