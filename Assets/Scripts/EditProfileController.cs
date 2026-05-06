using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class EditProfileController : MonoBehaviour
{
    public GameObject editProfilePanel;
    public GameObject statsPanel;

    public TMP_InputField nameInput;

    public Image profileAvatarImage; // avatar in stats panel
    public TMPro.TextMeshProUGUI profileNameText; // name in stats panel

    // private Sprite selectedAvatar;
    public List<Image> avatarImages;   // all avatars
    private Image currentSelectedAvatar;

    public GameObject menuBackground;

    public Image topProfileAvatar;      // top bar avatar
    public TMPro.TextMeshProUGUI topProfileName; // top bar name
    private int selectedAvatarIndex = 0;

    private string tempName;
    private int tempAvatarIndex = 0;
    public AvatarDatabase avatarDatabase;   // drag in inspector
    public UnityEngine.UI.Image avatarPreviewImage; // preview image in UI

    // 🔓 OPEN EDIT PANEL
    public void OpenEditProfile()
    {
        editProfilePanel.SetActive(true);
        statsPanel.SetActive(false);
        // 🔥 LOAD CURRENT AVATAR
        selectedAvatarIndex = ProfileData.PlayerAvatarIndex;
        // 🔥 LOAD CURRENT NAME INTO INPUT
        if (nameInput != null)
            nameInput.text = ProfileData.PlayerName;

        int index = ProfileData.PlayerAvatarIndex;

        if (avatarDatabase != null &&
            avatarDatabase.avatarSprites != null &&
            index >= 0 &&
            index < avatarDatabase.avatarSprites.Length)
        {
            avatarPreviewImage.sprite = avatarDatabase.avatarSprites[index];
        }
    }

    // ❌ CLOSE EDIT PANEL
    public void CloseEditProfile()
    {
        editProfilePanel.SetActive(false);
        statsPanel.SetActive(true);
    }

    // 🧑 SELECT AVATAR
    public void SelectAvatar(Image clickedImage)
    {
        // 🔥 FIND INDEX
        selectedAvatarIndex = avatarImages.IndexOf(clickedImage);

        if (currentSelectedAvatar != null)
            currentSelectedAvatar.transform.localScale = Vector3.one;

        currentSelectedAvatar = clickedImage;
        clickedImage.transform.localScale = Vector3.one * 1.2f;

        Debug.Log("Avatar Selected Index: " + selectedAvatarIndex);
    }
    // 💾 SAVE PROFILE
    public void SaveProfile()
    {
        string newName = nameInput.text;

        if (newName.Length < 3)
        {
            Debug.Log("Name must be at least 3 characters");
            return;
        }

        // 🔥 STORE TEMP (NOT FINAL YET)
        tempName = newName;
        tempAvatarIndex = selectedAvatarIndex;

        // 🔥 UPDATE STATSPANEL (PREVIEW)
        profileNameText.text = tempName;

        if (avatarImages != null && avatarImages.Count > tempAvatarIndex)
            profileAvatarImage.sprite = avatarImages[tempAvatarIndex].sprite;

        Debug.Log("Preview Updated");

        CloseEditProfile(); // go back to stats panel
    }

    [System.Obsolete]
    public void ConfirmFinalSave()
    {
        Debug.Log("Final Save Confirmed");
        ProfileSaveSystem.SaveProfile(tempName, selectedAvatarIndex);
        // 🔥 STORE GLOBALLY
        ProfileData.PlayerName = tempName;
        ProfileData.PlayerAvatarIndex = tempAvatarIndex;
        PlayFabProfileManager.Instance.SaveProfile(tempName, selectedAvatarIndex);
        // 🔥 UPDATE TOP BAR PROFILE
        if (topProfileName != null)
            topProfileName.text = tempName;

        if (topProfileAvatar != null && avatarImages != null && avatarImages.Count > tempAvatarIndex)
            topProfileAvatar.sprite = avatarImages[tempAvatarIndex].sprite;

        // 🔥 GO BACK TO MENU
        statsPanel.SetActive(false);
        menuBackground.SetActive(true);

        // ⭐ FORCE UI REFRESH AFTER SAVE
        var menu = FindAnyObjectByType<MainMenuProfileController>();
        if (menu != null)
        {
            menu.UpdateUI();
        }
    }
}