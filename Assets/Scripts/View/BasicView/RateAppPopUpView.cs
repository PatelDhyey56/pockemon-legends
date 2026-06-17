using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Utils;

public class RateAppPopUpView : View
{
    private static RateAppPopUpView _instance;
    private const float _PopUpScaleTime = 0.3f;
    public GameSettings gameSettings;
    public TMP_Text gameNameText;
    public GameObject popup;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    public static RateAppPopUpView GetInstance()
    {
        return _instance;
    }

    private void Start()
    {
        gameNameText.text = gameSettings.GetGameName();    
    }

    protected override void OnViewShow()
    {
        base.OnViewShow();
        PopUpAnimation.ShowAnimation(popup);
    }

    public void OnLaterButtonClick()
    {
        Hide();
    }

    public void OnRateThisAppButtonClick()
    {
        Hide();
        PreferenceHelper.SetRateThisAppState(true);
#if UNITY_ANDROID
        AppSharing.GetInstance().OnRateNowButtonClick(gameSettings.GetPlayStoreURL());
#elif UNITY_IOS
        AppSharing.GetInstance().OnRateNowButtonClick(gameSettings.GetAppStoreURL());
#endif

    }

    public override void OnBackeyPressed()
    {
        Hide();
    }

}
