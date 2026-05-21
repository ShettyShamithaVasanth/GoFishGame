using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerProfileUI : MonoBehaviour
{
    public Image avatarImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI debugText;

    public void SetProfile(string playerName, Sprite avatar)
    {
        if (nameText != null)
            nameText.text = playerName;
        if (avatar != null && avatarImage != null)
            avatarImage.sprite = avatar;
    }
    public void SetDebugInfo(string seatId, string networkId)
    {
        if (debugText == null)
            return;
        debugText.text = $"seat:{seatId} net:{networkId}";
    }
}