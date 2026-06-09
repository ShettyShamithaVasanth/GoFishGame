using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class MenuAnimationController : MonoBehaviour
{
    [Header("Play Offline")]
    [SerializeField] RectTransform wingLeft;
    [SerializeField] RectTransform wingRight;

    [SerializeField] RectTransform card1;
    [SerializeField] RectTransform card2;

    [Header("Play Online")]
    [SerializeField] RectTransform fish1;
    [SerializeField] RectTransform fish2;
    [SerializeField] RectTransform fish3;
    [SerializeField] RectTransform fish4;

    [Header("Play With Friends")]
    [SerializeField] RectTransform friendsFish;

    [Header("Buttons")]
    [SerializeField] RectTransform luckyDrawIcon;
    [SerializeField] RectTransform freeCoinIcon;

    private void OnEnable()
    {
        StartAnimations();
    }

    private void OnDisable()
    {
        StopAnimations();
    }

    public void StartAnimations()
    {
        AnimateOffline();
        AnimateOnline();
        AnimateFriends();
        AnimateLuckyDraw();
        AnimateFreeCoins();
    }

    public void StopAnimations()
    {
        DOTween.Kill(this);
    }

    private void AnimateOffline()
    {
        if (wingLeft != null)
        {
            Sequence seq = DOTween.Sequence();

            seq.Append(
                wingLeft.DOScale(1.08f, 0.8f));

            seq.Join(
                wingLeft.DOAnchorPosY(
                    wingLeft.anchoredPosition.y + 3f,
                    0.8f));

            seq.SetLoops(-1, LoopType.Yoyo);

            seq.SetTarget(this);
        }

        if (wingRight != null)
        {
            Sequence seq = DOTween.Sequence();

            seq.Append(
                wingRight.DOScale(1.08f, 0.8f));

            seq.Join(
                wingRight.DOAnchorPosY(
                    wingRight.anchoredPosition.y + 3f,
                    0.8f));

            seq.SetLoops(-1, LoopType.Yoyo);

            seq.SetTarget(this);
        }

        AnimateCard(card1, 0f);

        AnimateCard(card2, 0.5f);
    }

    private void AnimateCard(
        RectTransform card,
        float delay)
    {
        if (card == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.AppendInterval(delay);

        seq.Append(
            card.DOAnchorPosY(
                card.anchoredPosition.y + 10f,
                2f));

        seq.Join(
            card.DORotate(
                new Vector3(0, 0, 5),
                2f));

        seq.SetLoops(-1, LoopType.Yoyo);

        seq.SetTarget(this);
    }

    private void AnimateOnline()
    {
        AnimateFish(fish1, 0f);

        AnimateFish(fish2, 0.4f);

        AnimateFish(fish3, 0.8f);

        AnimateFish(fish4, 1.2f);
    }

    private void AnimateFish(
        RectTransform fish,
        float delay)
    {
        if (fish == null)
            return;

        Vector2 start = fish.anchoredPosition;

        Sequence seq = DOTween.Sequence();

        seq.AppendInterval(delay);

        seq.Append(
            fish.DOAnchorPos(
                start + new Vector2(15, 10),
                2f));

        seq.Join(
            fish.DORotate(
                new Vector3(0, 0, 10),
                2f));

        seq.Join(
            fish.DOScale(
                1.08f,
                2f));

        seq.SetLoops(-1, LoopType.Yoyo);

        seq.SetTarget(this);
    }

    private void AnimateFriends()
    {
        if (friendsFish == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            friendsFish.DOAnchorPosX(
                friendsFish.anchoredPosition.x + 20f,
                2f));

        seq.Join(
            friendsFish.DOAnchorPosY(
                friendsFish.anchoredPosition.y + 8f,
                2f));

        seq.Join(
            friendsFish.DORotate(
                new Vector3(0, 0, 10),
                2f));

        seq.SetLoops(-1, LoopType.Yoyo);

        seq.SetTarget(this);
    }

    private void AnimateLuckyDraw()
    {
        if (luckyDrawIcon == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            luckyDrawIcon.DOScale(
                1.15f,
                0.35f));

        seq.Join(
            luckyDrawIcon.DOAnchorPosY(
                luckyDrawIcon.anchoredPosition.y + 5f,
                0.35f));

        seq.Append(
            luckyDrawIcon.DOScale(
                1f,
                0.35f));

        seq.Join(
            luckyDrawIcon.DOAnchorPosY(
                luckyDrawIcon.anchoredPosition.y,
                0.35f));

        seq.SetLoops(-1);

        seq.SetTarget(this);
    }

    private void AnimateFreeCoins()
    {
        if (freeCoinIcon == null)
            return;

        Sequence seq = DOTween.Sequence();

        seq.Append(
            freeCoinIcon.DOScale(
                1.2f,
                0.3f));

        seq.Append(
            freeCoinIcon.DOScale(
                1f,
                0.3f));

        seq.SetLoops(-1);

        seq.SetTarget(this);

        freeCoinIcon
            .DORotate(
                new Vector3(0, 0, 8),
                0.5f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }
}