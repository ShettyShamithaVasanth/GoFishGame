using UnityEngine;
using DG.Tweening;

public class LoadingSpinner : MonoBehaviour
{
    private Tween rotateTween;

    void OnEnable()
    {
        StartRotation();
    }

    void OnDisable()
    {
        StopRotation();
    }

    void StartRotation()
    {
        // 🔥 continuous rotation
        rotateTween = transform
            .DORotate(new Vector3(0, 0, -360), 2f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1);
    }

    void StopRotation()
    {
        rotateTween?.Kill();
        transform.rotation = Quaternion.identity; // reset if needed
    }
}