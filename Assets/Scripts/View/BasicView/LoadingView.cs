using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class LoadingView : View
{
    public static LoadingView _instance;
    public TMP_Text loadingText;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    public static LoadingView GetInstance()
    {
        return _instance;
    }

    protected override void OnViewShow()
    {
        base.OnViewShow();

        AnimateView();
    }

    protected override void OnViewHide()
    {
        viewCanvasGroup.alpha = 0;

        base.OnViewHide();
    }

    private void AnimateView()
    {

    }

    public void ShowLoading(string text)
    {
        loadingText.text = text;

        Show();
    }

}
