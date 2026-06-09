using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfileUI : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;

    public void SetProfile(string playerName, Sprite avatar)
    {
        if (nameText != null)
            nameText.text = playerName;
        if (avatar != null && avatarImage != null)
            avatarImage.sprite = avatar;
    }
}