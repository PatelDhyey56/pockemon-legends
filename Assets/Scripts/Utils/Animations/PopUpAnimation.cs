using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public static class PopUpAnimation
{
    public static void ShowAnimation(GameObject popup)
    {
        popup.transform.localScale = Vector3.zero;

        popup.transform.DOScale(new Vector3(1f, 1f, 1f), 0.3f).SetEase(Ease.OutBack);
    }

}
