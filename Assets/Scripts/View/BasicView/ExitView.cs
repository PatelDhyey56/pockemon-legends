using AdsManager;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ExitView : View
{
    public static ExitView _instance;
    public TMP_Text gameNameText;
    public GameSettings gameSettings;
    public GameObject popup;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    public static ExitView GetInstance()
    {
        return _instance;
    }

    private void Start()
    {
        gameNameText.text = gameSettings.GetGameName();
    }

    public override void OnBackeyPressed()
    {
        AdMobManager.GetInstance().ShowBanner();
        Hide();
    }

    protected override void OnViewShow()
    {
        AdMobManager.GetInstance().HideBanner();

        popup.transform.localScale = Vector3.zero;

        base.OnViewShow();

        PopUpAnimation.ShowAnimation(popup);
    }

    public void OnYesButtonClick()
    {
        Application.Quit();
    }

    public void OnNoButtonClick()
    {
        AdMobManager.GetInstance().ShowBanner();
        Hide();
    }
}
