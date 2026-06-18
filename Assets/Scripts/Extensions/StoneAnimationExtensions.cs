// StoneAnimationExtensions.cs
// Provides a drag‑away animation for matched stones using DOTween.
// This file is placed under Assets/Scripts/Extensions/ and compiled into the default Assembly-CSharp.

using UnityEngine;
using UnityEngine.UI; // CanvasGroup is in UnityEngine.UI namespace
using DG.Tweening;

/// <summary>
/// Extension methods for stone GameObjects used in the match‑3 board.
/// </summary>
public static class StoneAnimationExtensions
{
    /// <summary>
    /// Plays a simple "drag‑away" animation and invokes a callback when finished.
    /// </summary>
    /// <param name="stone">The stone GameObject to animate.</param>
    /// <param name="onComplete">Optional callback executed after the animation (usually removal logic).</param>
    public static void PlayDragAway(this GameObject stone, System.Action onComplete = null)
    {
        // Ensure the stone has a CanvasGroup for fading.
        var canvasGroup = stone.GetComponent<CanvasGroup>();
        if (!canvasGroup) canvasGroup = stone.AddComponent<CanvasGroup>();

        // Ensure we have a RectTransform for scaling/moving.
        var rect = stone.GetComponent<RectTransform>();
        if (!rect) rect = stone.AddComponent<RectTransform>();

        // Fade out over 0.35 seconds.
        var fade = canvasGroup.DOFade(0f, 0.35f);

        // Pop‑up scaling then shrink to zero.
        var pop = rect.DOScale(1.2f, 0.15f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => rect.DOScale(0f, 0.2f).SetEase(Ease.InBack).Play());

        // Move a short random distance away from the board for the drag effect.
        Vector2 dir = Random.insideUnitCircle.normalized * 50f; // 50 px offset
        var move = rect.DOAnchorPos(rect.anchoredPosition + dir, 0.35f).SetEase(Ease.OutCubic);

        // Combine everything into one sequence and fire the completion callback when done.
        Sequence seq = DOTween.Sequence()
            .Join(fade)
            .Join(pop)
            .Join(move)
            .SetLink(stone) // auto‑kill if stone is destroyed early
            .OnComplete(() =>
            {
                canvasGroup.alpha = 0f; // ensure fully invisible
                onComplete?.Invoke();
            });

        // Attach a small helper to kill the tween if the stone is destroyed/reused early.
        stone.AddComponent<AnimationKiller>().Init(seq);
    }
}

/// <summary>
/// Helper component attached to the stone to clean up the tween if the object is destroyed before the animation ends.
/// </summary>
public class AnimationKiller : MonoBehaviour
{
    private Tween _tween;
    public void Init(Tween tween) => _tween = tween;
    private void OnDestroy() => _tween?.Kill();
}
