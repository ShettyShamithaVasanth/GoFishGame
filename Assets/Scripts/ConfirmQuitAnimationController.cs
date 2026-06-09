using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ConfirmQuitAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform header;

    [SerializeField] private Button cancelButton;
    [SerializeField] private Button quitButton;

    private void OnEnable()
    {
        StartAnimations();
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
    }

    private void StartAnimations()
    {
        AnimateHeader();

        AnimateButton(cancelButton);

        AnimateButton(quitButton);
    }

    private void AnimateHeader()
    {
        if (header == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            header.DOScale(
                1.03f,
                1f));

        seq.Join(
            header.DOAnchorPosY(
                header.anchoredPosition.y + 3f,
                1f));

        seq.SetEase(Ease.InOutSine);

        seq.SetLoops(-1, LoopType.Yoyo);

        seq.SetTarget(this);
    }

    private void AnimateButton(Button button)
    {
        if (button == null)
            return;

        RectTransform rect =
            button.GetComponent<RectTransform>();

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rect.DOScale(
                1.05f,
                0.7f));

        seq.Append(
            rect.DOScale(
                1f,
                0.7f));

        seq.SetLoops(-1);

        seq.SetTarget(this);
    }
}