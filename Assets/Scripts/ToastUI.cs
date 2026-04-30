using UnityEngine;
using TMPro;
using DG.Tweening;

public class ToastUI : MonoBehaviour
{
    public TextMeshProUGUI toastText;
    public CanvasGroup canvasGroup;

    private Tween currentTween;

    void Awake()
    {
        // Always start hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0;
        }

        gameObject.SetActive(false); 
    }
    //SHOW (NO AUTO HIDE)
    public void ShowToast(string message)
    {
        gameObject.SetActive(true);
        if (currentTween != null)
            currentTween.Kill();

        toastText.text = message;

        canvasGroup.alpha = 0;
        currentTween = canvasGroup.DOFade(1, 0.25f).SetLink(gameObject);
    }

    // ⭐ HIDE MANUALLY
    public void HideToast()
    {
        if (canvasGroup == null)
            return;

        if (currentTween != null)
            currentTween.Kill();

        currentTween = canvasGroup.DOFade(0, 0.25f).OnComplete(() =>
        {
            gameObject.SetActive(false);
        }).SetLink(gameObject);
    }

    public void ShowToastWithAutoHide(string message, float duration)
    {
        gameObject.SetActive(true);
        if (canvasGroup == null)
            return;

        if (currentTween != null)
            currentTween.Kill();

        toastText.text = message;

        canvasGroup.alpha = 0;

        currentTween = canvasGroup
            .DOFade(1, 0.25f)
            .SetLink(gameObject);

        // ⭐ SAFE delayed call
        DOVirtual.DelayedCall(duration, () =>
        {
            if (this != null && canvasGroup != null)
            {
                HideToast();
            }
        }).SetLink(gameObject);
    }

    void OnDestroy()
    {
        if (currentTween != null)
            currentTween.Kill();

        DOTween.Kill(gameObject);
    }
}