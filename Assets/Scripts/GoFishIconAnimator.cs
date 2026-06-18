using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class GoFishIconAnimator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Go Fish icon RectTransform to animate. Leave empty to auto-use this GameObject's RectTransform.")]
    [SerializeField] private RectTransform icon;

    [Header("Float (vertical bob)")]
    [SerializeField] private float floatDistance = 8f;
    [SerializeField] private float floatDuration = 1.6f;

    [Header("Sway (rotation)")]
    [SerializeField] private float swayAngle = 6f;
    [SerializeField] private float swayDuration = 1.6f;

    [Header("Breathing scale (subtle)")]
    [SerializeField] private float scalePulse = 1.04f;
    [SerializeField] private float scaleDuration = 1.6f;

    private Sequence _sequence;

    private void Reset()
    {
        icon = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (icon == null)
            icon = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        StartAnimation();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private void StartAnimation()
    {
        if (icon == null)
            return;

        StopAnimation();

        Vector2 basePos = icon.anchoredPosition;
        Vector3 baseScale = icon.localScale;

        _sequence = DOTween.Sequence()
            .SetTarget(this);

        _sequence.Join(
            icon.DOAnchorPosY(
                basePos.y + floatDistance,
                floatDuration
            )
            .SetEase(Ease.InOutSine));

        _sequence.Join(
            icon.DORotate(
                new Vector3(0f, 0f, swayAngle),
                swayDuration
            )
            .SetEase(Ease.InOutSine));

        _sequence.Join(
            icon.DOScale(
                baseScale * scalePulse,
                scaleDuration
            )
            .SetEase(Ease.InOutSine));

        _sequence.SetLoops(-1, LoopType.Yoyo);
    }

    private void StopAnimation()
    {
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }

        DOTween.Kill(this);
    }
}