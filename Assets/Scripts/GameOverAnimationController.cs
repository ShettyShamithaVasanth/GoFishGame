using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class GameOverAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform header;

    [SerializeField] private Button playAgainButton;

    [SerializeField] private Button quitButton;

    [SerializeField] private Image playAgainImage;

    [SerializeField] private Image quitImage;

    private void OnEnable()
    {
        StartAnimations();
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
    }

    public void StartAnimations()
    {
        AnimateHeader();

        AnimateButton(playAgainButton, playAgainImage);

        AnimateButton(quitButton, quitImage);
    }

    private void AnimateHeader()
    {
        if (header == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            header.DOScale(
                1.04f,
                1.2f));

        seq.Join(
            header.DOAnchorPosY(
                header.anchoredPosition.y + 4f,
                1.2f));

        seq.SetLoops(-1, LoopType.Yoyo);

        seq.SetEase(Ease.InOutSine);

        seq.SetTarget(this);
    }

    private void AnimateButton(
        Button button,
        Image image)
    {
        if (button == null)
            return;

        RectTransform rect =
            button.GetComponent<RectTransform>();

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rect.DOScale(
                1.05f,
                0.8f));

        seq.Append(
            rect.DOScale(
                1f,
                0.8f));

        seq.SetLoops(-1);

        seq.SetTarget(this);

        if (image != null)
        {
            image
                .DOFade(
                    0.8f,
                    0.8f)
                .SetLoops(
                    -1,
                    LoopType.Yoyo)
                .SetTarget(this);
        }
    }
}