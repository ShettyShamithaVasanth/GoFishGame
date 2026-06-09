using UnityEngine;
using DG.Tweening;

public class MatchmakingAnimationController : MonoBehaviour
{
    [SerializeField]
    private RectTransform centerCircle;

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
        if (centerCircle == null)
            return;

        centerCircle
            .DORotate(
                new Vector3(0, 0, -360),
                12f,
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetTarget(this);
    }
}