using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Utils;
using System;
using AdsManager;
using IAPPurchasing;
using TMPro;

public class SettingView : View
{
    private static SettingView _instance;
    public GameObject consentButton;
    public GameObject noAdsButton;
    public GameObject restoreButton;
    private string _shareMessage;
    public GameSettings gameSettings;

    private bool _isWebViewOpen = false;
    private TMP_Text _settingTitleText;
    private GameObject _contentPanel;
    private WebViewObject webViewObject;
    private GameObject _soundOnBtn;
    private GameObject _soundOffBtn;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
        SetShareMessage();

        if (canvasGameObject != null)
        {
            Transform viewContent = canvasGameObject.transform.Find("ViewContent");
            if (viewContent != null)
            {
                Transform topUI = viewContent.Find("TopUI");
                if (topUI != null)
                {
                    Transform titleTrans = topUI.Find("SettingTitle");
                    if (titleTrans != null)
                    {
                        _settingTitleText = titleTrans.GetComponent<TMP_Text>();
                    }
                }
                Transform contentTrans = viewContent.Find("Content");
                if (contentTrans != null)
                {
                    _contentPanel = contentTrans.gameObject;
                    
                    Transform soundOnTrans = contentTrans.Find("SoundOnBtn");
                    if (soundOnTrans != null)
                    {
                        _soundOnBtn = soundOnTrans.gameObject;
                        Button btn = _soundOnBtn.GetComponent<Button>();
                        if (btn != null) btn.onClick.AddListener(OnSoundOnButtonClick);
                    }

                    Transform soundOffTrans = contentTrans.Find("SoundOffBtn");
                    if (soundOffTrans != null)
                    {
                        _soundOffBtn = soundOffTrans.gameObject;
                        Button btn = _soundOffBtn.GetComponent<Button>();
                        if (btn != null) btn.onClick.AddListener(OnSoundOffButtonClick);
                    }
                }
            }
        }
    }

    public static SettingView GetInstance()
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<SettingView>(FindObjectsInactive.Include);
        }
        return _instance;
    }

    private void Start()
    {
        EnableDisableConsentButton();

        ActiveRestoreButton();

        if (PreferenceHelper.IsAdRemoved())
            HideConsentAndBuyAdsButtons();
            
        UpdateSoundButtons();
    }

    private void OnEnable()
    {
        PurchaseController.OnRemoveAd += HideConsentAndBuyAdsButtons;
        PurchaseController.OnPurchaseFail += HideLoadingView;
        PurchaseController.OnRestoreFailed += ShowMessage;
        PurchaseController.OnRestoreSuccess += RestorePurchaseSuccess;
    }



    private void OnDisable()
    {
        PurchaseController.OnRemoveAd -= HideConsentAndBuyAdsButtons;
        PurchaseController.OnPurchaseFail -= HideLoadingView;
        PurchaseController.OnRestoreFailed -= ShowMessage;
        PurchaseController.OnRestoreSuccess -= RestorePurchaseSuccess;
    }

    protected override void OnViewShow()
    {
        base.OnViewShow();
        PreferenceHelper.SetIsWebViewOpen(false);
        _isWebViewOpen = false;
        if (_contentPanel != null)
        {
            _contentPanel.SetActive(true);
        }
    }

    protected override void OnViewHide()
    {
        base.OnViewHide();
        DestroyWebView();
    }

    private void ActiveRestoreButton()
    {
#if UNITY_ANDROID
        restoreButton.SetActive(false);
#elif UNITY_IOS
        restoreButton.SetActive(true);
#endif
    }

    private void HideConsentAndBuyAdsButtons()
    {
        consentButton.SetActive(false);
        noAdsButton.SetActive(false);
        restoreButton.SetActive(false);
    }

    private void OnSoundOnButtonClick()
    {
        PreferenceHelper.SetSoundOn(false);
        UpdateSoundButtons();
    }

    private void OnSoundOffButtonClick()
    {
        PreferenceHelper.SetSoundOn(true);
        UpdateSoundButtons();
    }

    private void UpdateSoundButtons()
    {
        bool isSoundOn = PreferenceHelper.IsSoundOn();
        if (_soundOnBtn != null) _soundOnBtn.SetActive(isSoundOn);
        if (_soundOffBtn != null) _soundOffBtn.SetActive(!isSoundOn);
        AudioListener.volume = isSoundOn ? 1f : 0f;
    }

    private void SetShareMessage()
    {
#if UNITY_ANDROID
        _shareMessage = Constants.GAME_SHARE_TEXT + gameSettings.GetPlayStoreURL();
#elif UNITY_IOS
        _shareMessage = Constants.GAME_SHARE_TEXT+gameSettings.GetAppStoreURL();
#endif
    }

    public override void OnBackeyPressed()
    {
        if (_isWebViewOpen)
        {
            CloseWebView();
        }
        else
        {
            OnCloseButtonClick();
        }
    }

    public void OnCloseButtonClick()
    {
        if (_isWebViewOpen)
        {
            CloseWebView();
        }
        else
        {
            Hide();
            MenuView.GetInstance().Show();
            if (AdMobManager.GetInstance() != null)
            {
                AdMobManager.GetInstance().ShowInterstitial();
            }
        }
    }

    public void OnPrivacyPolicyButtonClick()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            MessageView.GetInstance().ShowMessageView(Constants.WARN_NO_INTERNET, "Ok");
        }
        else
        {
            PreferenceHelper.SetWebViewTittle(Constants.PRIVACY_TITTLE);
            OpenWebView(Constants.PRIVACY_URL);
        }
    }

    public void OnLicenceButtonClick()
    {
        PreferenceHelper.SetWebViewTittle(Constants.LICENCE_TITTLE);
        OpenWebView("data:text/html;charset=utf-8," + System.Uri.EscapeDataString(Constants.LICENSE_HTML));
    }

    private void OpenWebView(string url)
    {
        _isWebViewOpen = true;
        PreferenceHelper.SetIsWebViewOpen(true);

        if (_contentPanel != null)
        {
            _contentPanel.SetActive(false);
        }

        StartCoroutine(InitWebviewCoroutine(url));
    }

    private void CloseWebView()
    {
        DestroyWebView();
        _isWebViewOpen = false;

        if (_contentPanel != null)
        {
            _contentPanel.SetActive(true);
        }
        PreferenceHelper.SetIsWebViewOpen(false);
    }

    private void DestroyWebView()
    {
        if (webViewObject != null)
        {
            Destroy(webViewObject.gameObject);
            webViewObject = null;
        }
    }

    private IEnumerator InitWebviewCoroutine(string url)
    {
        webViewObject = (new GameObject("WebViewObject")).AddComponent<WebViewObject>();
        webViewObject.Init(
            cb: (msg) =>
            {
                Debug.Log(string.Format("CallFromJS[{0}]", msg));
            },
            err: (msg) =>
            {
                Debug.Log(string.Format("CallOnError[{0}]", msg));
            },
            httpErr: (msg) =>
            {
                Debug.Log(string.Format("CallOnHttpError[{0}]", msg));
            },
            started: (msg) =>
            {
                Debug.Log(string.Format("CallOnStarted[{0}]", msg));
            },
            hooked: (msg) =>
            {
                Debug.Log(string.Format("CallOnHooked[{0}]", msg));
            },
            ld: (msg) =>
            {
                Debug.Log(string.Format("CallOnLoaded[{0}]", msg));
#if UNITY_EDITOR_OSX || (!UNITY_ANDROID && !UNITY_WEBPLAYER && !UNITY_WEBGL)
                webViewObject.EvaluateJS(@"
                  if (window && window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.unityControl) {
                    window.Unity = {
                      call: function(msg) {
                        window.webkit.messageHandlers.unityControl.postMessage(msg);
                      }
                    }
                  } else {
                    window.Unity = {
                      call: function(msg) {
                        window.location = 'unity:' + msg;
                      }
                    }
                  }
                ");
#elif UNITY_WEBPLAYER || UNITY_WEBGL
                webViewObject.EvaluateJS(
                    "window.Unity = {" +
                    "   call:function(msg) {" +
                    "       parent.unityWebView.sendMessage('WebViewObject', msg)" +
                    "   }" +
                    "};");
#endif
                webViewObject.EvaluateJS(@"Unity.call('ua=' + navigator.userAgent)");
            },
            enableWKWebView: true
        );
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        webViewObject.bitmapRefreshCycle = 1;
#endif

        if (Extensions.ScreenRatio())
        {
#if UNITY_ANDROID
            webViewObject.SetMargins(0, 120 * Screen.height / 960, 0, 0);
#elif UNITY_IOS
            webViewObject.SetMargins(0, 120 * Screen.height / 960, 0, 0);
#endif
        }
        else
        {
#if UNITY_ANDROID
            webViewObject.SetMargins(0, 150 * Screen.height / 960, 0, 0);
#elif UNITY_IOS
            webViewObject.SetMargins(0, 150 * Screen.height / 960, 0, 0);
#endif
        }

        webViewObject.SetTextZoom(100);
        webViewObject.SetVisibility(true);

#if !UNITY_WEBPLAYER && !UNITY_WEBGL
        if (url.StartsWith("http") || url.StartsWith("data:"))
        {
            webViewObject.LoadURL(url);
        }
#else
        if (url.StartsWith("http") || url.StartsWith("data:")) {
            webViewObject.LoadURL(url.Replace(" ", "%20"));
        }
#endif
        yield break;
    }

    public void OnBuyAdFreeButtonClick()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            MessageView.GetInstance().ShowMessageView(Constants.WARN_NO_INTERNET, "Ok");
        }
        else
        {
            AdFreeView.GetInstance().Show();
        }
    }

    public void OnRateThisAppButtonClick()
    {
        RateAppPopUpView.GetInstance().Show();
    }

    public void OnShareAppButtonClick()
    {
        AppSharing.GetInstance().ShareApp(_shareMessage);
    }

    public void EnableDisableConsentButton()
    {
        if (AdMobManager.GetInstance().IsConsentRequired())
            consentButton.SetActive(true);
        else
            consentButton.SetActive(false);
    }

    public void OnConsentButtonClick()
    {
        AdMobManager.GetInstance().LoadAndShowConsentForm();
    }

    public void OnRestorePurchaseButtonClick()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            MessageView.GetInstance().ShowMessageView(Constants.WARN_NO_INTERNET, "Ok");
        }
        else
        {
            LoadingView.GetInstance().ShowLoading("Please wait...");

            PurchaseManager.GetInstance().RestorePurchases();
        }
    }

    private void ShowMessage()
    {
        LoadingView.GetInstance().Hide();

        MessageView.GetInstance().ShowMessageView(Constants.RESTORE_PURCHASE_WARNING, "Ok");
    }

    private void HideLoadingView()
    {
        LoadingView.GetInstance().Hide();
    }

    public void RestorePurchaseSuccess()
    {
        LoadingView.GetInstance().Hide();

        MessageView.GetInstance().ShowMessageView(Constants.RESTORE_PURCHASE_SUCCESS, "Ok");
    }

    public void ResetConcent()
    {
        AdMobManager.GetInstance().ResetConsent();
    }
}
