using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StatsPanelAnimationController : MonoBehaviour
{
    [SerializeField]
    private Button saveButton;

    private void OnEnable()
    {
        StartAnimation();
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
    }

    private void StartAnimation()
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