using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Extensions
{
    public static void BounceAnimation(this Transform transform, float animationDelay = 0.4f)
    {
        Tween exitTween = transform.DOScale(new Vector3(1, 1, 1), animationDelay);
        exitTween.SetEase(Ease.OutBack);
    }

    public static void ScaleAnimation(this RectTransform rectTransform, Vector3 scaleOffset, float animationDelay = 0.2f)
    {
        Tween exitTween = rectTransform.DOScale(scaleOffset, animationDelay);
        exitTween.SetEase(Ease.OutBack);
    }

    public static bool ScreenRatio()
    {
        var width_offset = Screen.width / 16;
        var height_offset = Screen.height / 9;

        if (width_offset != height_offset)
            return false;
        else
            return true;
    }
}
