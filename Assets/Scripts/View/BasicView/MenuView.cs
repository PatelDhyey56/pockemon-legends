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
            if (AdMobManager.GetInstance() != null)
            {
                AdMobManager.GetInstance().HideBanner();
            }
        }

        LogHelper.Info("MenView", "waiting..");

        if (AdMobManager.GetInstance() != null)
        {
            yield return new WaitUntil(() => AdMobManager.GetInstance().IsSdkInitialized);

            LogHelper.Info("MenView", "waiting done..");

            AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom, AdStatusDelegate: AdStatusDelegate);
        }
        else
        {
            LogHelper.Info("MenView", "AdMobManager instance is null, skipping banner initialization");
        }
    }

    private void AdStatusDelegate(AdStatusCode adStatusCode)
    {
        LogHelper.Info("MenView", adStatusCode.ToString());

        if (adStatusCode == AdStatusCode.ADLoadSuccess)
        {
            if (isViewVisible && !_isIAPOpen && AdMobManager.GetInstance() != null)
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

        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().ShowBanner();
        }
    }

    public void OnPlayButtonClick()
    {
        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().HideBanner();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_BATTLE_PREP);
    }

    public void OnSettingButtonClick()
    {
        Hide();
        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().HideBanner();
        }
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
            if (AdMobManager.GetInstance() != null)
            {
                AdMobManager.GetInstance().HideBanner();
            }

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

        if (AdMobManager.GetInstance() != null)
        {
            AdMobManager.GetInstance().ShowBanner();
        }
    }
}
