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

    void Start()
    {
        LoadProfile();
    }
    void LoadProfile()
    {
        // 🔥 GET SAVED DATA
        string savedName = ProfileSaveSystem.LoadName();
        int avatarIndex = ProfileSaveSystem.LoadAvatarIndex();

        // 🔥 STORE INTO GLOBAL DATA
        ProfileData.PlayerName = savedName;

        // 🔥 GET AVATAR FROM LIST
        if (avatarImages != null && avatarImages.Count > avatarIndex)
        {
            ProfileData.PlayerAvatar = avatarImages[avatarIndex].sprite;
        }

        // 🔥 UPDATE UI (TOP BAR)
        if (topProfileName != null)
            topProfileName.text = savedName;

        if (topProfileAvatar != null && ProfileData.PlayerAvatar != null)
            topProfileAvatar.sprite = ProfileData.PlayerAvatar;

        Debug.Log("Profile Loaded Successfully");
    }

    // 🔥 OPEN STATS PANEL
    public void OpenProfilePanel()
    {
        Debug.Log("Profile Button Clicked");

        if (statsPanel != null)
            statsPanel.SetActive(true);

        // 🔥 HIDE MENU BACKGROUND
        if (menuBackground != null)
            menuBackground.SetActive(false);
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