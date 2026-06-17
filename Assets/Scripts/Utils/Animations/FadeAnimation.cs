using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class FadeAnimation : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public float delay = 0.5f;

    // Use this for initialization
    void Start()
    {
        canvasGroup.DOFade(0, 1f).From().SetLoops(-1, LoopType.Yoyo);
    }
}