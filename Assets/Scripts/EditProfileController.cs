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

    private Sprite selectedAvatar;
    public List<Image> avatarImages;   // all avatars
    private Image currentSelectedAvatar;

    public GameObject menuBackground;

    public Image topProfileAvatar;      // top bar avatar
    public TMPro.TextMeshProUGUI topProfileName; // top bar name
    private int selectedAvatarIndex = 0;

    private string tempName;
    private Sprite tempAvatar;

    // 🔓 OPEN EDIT PANEL
    public void OpenEditProfile()
    {
        editProfilePanel.SetActive(true);
        statsPanel.SetActive(false);
        // 🔥 LOAD CURRENT AVATAR
        selectedAvatar = ProfileData.PlayerAvatar;
        // 🔥 LOAD CURRENT NAME INTO INPUT
        if (nameInput != null)
            nameInput.text = ProfileData.PlayerName;
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
        selectedAvatar = clickedImage.sprite;

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
        tempAvatar = selectedAvatar;

        // 🔥 UPDATE STATSPANEL (PREVIEW)
        profileNameText.text = tempName;

        if (tempAvatar != null)
            profileAvatarImage.sprite = tempAvatar;

        Debug.Log("Preview Updated");

        CloseEditProfile(); // go back to stats panel
    }

    public void ConfirmFinalSave()
    {
        Debug.Log("Final Save Confirmed");
        ProfileSaveSystem.SaveProfile(tempName, selectedAvatarIndex);
        // 🔥 STORE GLOBALLY
        ProfileData.PlayerName = tempName;
        ProfileData.PlayerAvatar = tempAvatar;
        PlayFabProfileManager.Instance.SaveProfile(tempName, selectedAvatarIndex);
        // 🔥 UPDATE TOP BAR PROFILE
        if (topProfileName != null)
            topProfileName.text = tempName;

        if (topProfileAvatar != null && tempAvatar != null)
            topProfileAvatar.sprite = tempAvatar;

        // 🔥 GO BACK TO MENU
        statsPanel.SetActive(false);
        menuBackground.SetActive(true);
    }
}