using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfileUI : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;

    public void SetProfile(string playerName, Sprite avatar)
    {
        nameText.text = playerName;

        if (avatar != null)
            avatarImage.sprite = avatar;
    }
}