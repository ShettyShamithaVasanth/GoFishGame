using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModeSelectionController : MonoBehaviour
{
    public TextMeshProUGUI headerText;

    public Image btn2;
    public Image btn3;
    public Image btn4;

    public Slider coinSlider;
    public TextMeshProUGUI coinText;

    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    public GameObject GameManager;
    public GameSceneUI gameSceneUI;
    public UIPlayer[] uiPlayers;

    public static int selectedPlayers = 4;
    void Start()
    {
        UpdateCoinText();
    }
    void UpdateCoinText()
    {
        int coinValue = Mathf.RoundToInt(coinSlider.value);
        coinText.text = coinValue.ToString();
    }

    public void OnSliderValueChanged()
    {
        UpdateCoinText();
    }
    public void SetHeader(string mode)
    {
        headerText.text = mode;
    }

    public void Select2()
    {
        selectedPlayers = 2;
        UpdateButtons(btn2);
    }

    public void Select3()
    {
        selectedPlayers = 3;
        UpdateButtons(btn3);
    }

    public void Select4()
    {
        selectedPlayers = 4;
        UpdateButtons(btn4);
    }

    void UpdateButtons(Image selected)
    {
        btn2.color = normalColor;
        btn3.color = normalColor;
        btn4.color = normalColor;

        selected.color = selectedColor;
    }

    public void PlayGame()
    {
        Debug.Log("Play clicked. Starting game.");

        gameObject.SetActive(false); // hide mode panel

        GameManager.SetActive(true);

        foreach (UIPlayer p in uiPlayers)
            p.canInteract = true;

        gameSceneUI.ShowPanel();
    }
}