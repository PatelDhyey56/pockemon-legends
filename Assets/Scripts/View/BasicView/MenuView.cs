using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using AdsManager;

public class MenuView : View
{
    private static MenuView _instance;
    public GameObject noAdsButton;
    private bool _isIAPOpen = false;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    private void OnEnable()
    {
        PurchaseController.OnRemoveAd += HideNoAdsButton;

    }

    private void OnDisable()
    {
        PurchaseController.OnRemoveAd -= HideNoAdsButton; 
    }

    public static MenuView GetInstance()
    {
        return _instance;
    }

    private IEnumerator Start()
    {

        Show();

        if (PreferenceHelper.IsAdRemoved())
        {
            HideNoAdsButton();
        }

        if (PreferenceHelper.GetIsWebViewOpen())
        {
            Hide();
            SettingView.GetInstance().Show();
            AdMobManager.GetInstance().HideBanner();
        }

        LogHelper.Info("MenView", "waiting..");

        yield return new WaitUntil(() => AdMobManager.GetInstance().IsSdkInitialized);

        LogHelper.Info("MenView", "waiting done..");

        AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom, AdStatusDelegate: AdStatusDelegate);
    }

    private void AdStatusDelegate(AdStatusCode adStatusCode)
    {
        LogHelper.Info("MenView", adStatusCode.ToString());

        if (adStatusCode == AdStatusCode.ADLoadSuccess)
        {
            if (isViewVisible && !_isIAPOpen)
            {
                AdMobManager.GetInstance().ShowBanner();
            }
        }
    }


    public override void OnBackeyPressed()
    {
        ExitView.GetInstance().Show();
    }

    protected override void OnViewShow()
    {
        base.OnViewShow();

        AdMobManager.GetInstance().ShowBanner();
    }

    public void OnSettingButtonClick()
    {
        Hide();
        AdMobManager.GetInstance().HideBanner();
        SettingView.GetInstance().Show();
    }

    public void OnRemoveAdsButton()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            MessageView.GetInstance().ShowMessageView(Constants.WARN_NO_INTERNET, "Ok");
        }
        else
        {
            AdMobManager.GetInstance().HideBanner();

            _isIAPOpen = true;

            AdFreeView.GetInstance().ShowAdFreeView(OnIAPViewHide);
        }
    }

    private void HideNoAdsButton()
    {
        LoadingView.GetInstance().Hide();

        noAdsButton.SetActive(false);
    }

    public void OnIAPViewHide()
    {
        _isIAPOpen = false;

        AdMobManager.GetInstance().ShowBanner();
    }
}
