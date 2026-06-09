using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EditProfileAnimationController : MonoBehaviour
{
    [SerializeField] private RectTransform gameLogo;
    [SerializeField] private Button saveButton;

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
        AnimateLogo();

        AnimateSaveButton();
    }

    private void AnimateLogo()
    {
        if (gameLogo == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            gameLogo.DOScale(
                1.04f,
                1f));

        seq.Append(
            gameLogo.DOScale(
                1f,
                1f));

        seq.SetLoops(-1);

        seq.SetEase(Ease.InOutSine);

        seq.SetTarget(this);
    }

    private void AnimateSaveButton()
    {
        if (saveButton == null)
            return;

        RectTransform rect =
            saveButton.GetComponent<RectTransform>();

        Sequence seq = DOTween.Sequence();

        seq.Append(
            rect.DOScale(
                1.08f,
                0.6f));

        seq.Append(
            rect.DOScale(
                1f,
                0.6f));

        seq.SetLoops(-1);

        seq.SetTarget(this);
    }
}