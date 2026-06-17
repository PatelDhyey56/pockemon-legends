using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using AdsManager.ScriptableObjects;
using AdsManager.Utils;

namespace AdsManager
{
    #region ENUMS

    public enum AdStatusCode
    {
        NoInternet,
        SDKInitFailed,
        ADLoadSuccess,
        ADLoadFailed,
        RewardGranted,
        AdClosed
    }

    public enum BannerAdPosition
    {
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    public enum BannerAdSize
    {
        Banner,
        MediumRectangle,
        IABBanner,
        Leaderboard,
        FullWidth,
        Custom
    }

    public enum MaxContentRating
    {
        G,
        PG,
        T,
        MA
    }

    #endregion

    public class AdMobManager : MonoBehaviour
    {
        private const string TAG = "AdMobManager";
        private const string PREF_AD_REMOVED = "wpk1u25xwt";

        public AdData adData;

        public bool AppOpenAdAutoLoad = false;

        public static event Action OnInitializeComplete;
        public static event Action<AppState> OnAppStateChange;

        public delegate void OnAdStatusDelegate(AdStatusCode adStatusCode);
        public OnAdStatusDelegate AppOpenAdStatus;
        public OnAdStatusDelegate BannerAdStatus;
        public OnAdStatusDelegate RectBannerAdStatus;
        public OnAdStatusDelegate RewardVideoAdStatus;
        public OnAdStatusDelegate RewardInterstitialAdStatus;
        public OnAdStatusDelegate InterstitialAdStatus;

        public bool RequestInterstitialOnInitialize = true;

        [Tooltip("Load new banner on every new request")]
        public bool LoadNewBannerOnEachRequest = true;


        private static AdMobManager _instance;
        private DateTime _expireTime;
        private string _adIdBanner;
        private string _adIDInterstitial;
        private string _adIdRewardedInterstitial;
        private string _adIdRewardVideo;
        private string _adIdAppOpen;
        private string _adIDRectBanner;

        private BannerView _bannerView;
        private BannerView _rectBannerView;
        private InterstitialAd _interstitial;
        private RewardedInterstitialAd _rewardedInterstitialAd;
        private RewardedAd _rewardedAd;
        private AppOpenAd _appOpenAd;

        [SerializeField]
        private TagForChildDirectedTreatment tagForChildDirectedTreatment;

        [SerializeField]
        private MaxContentRating maxContentRating;

        [SerializeField]
        private bool initializeOnStart = true;

        private bool _isBannerOpened;

        private bool _isAppOpenAdLoaded;
        public bool IsAppOpenAdAvailable
        {
            get
            {
                if (_appOpenAd == null)
                    AdsDebug.Log("IsAppOpenAdAvailable _appOpenAd is null");

                AdsDebug.Log("IsAppOpenAdAvailable _isAppOpenAdLoaded " + _isAppOpenAdLoaded);
                AdsDebug.Log("IsAppOpenAdAvailable _expireTime " + _expireTime.ToString());


                return _appOpenAd != null
                       && _isAppOpenAdLoaded
                       && DateTime.Now < _expireTime;
            }
        }

        private bool _canShowOpenAd = true;
        private bool _forceDontShowOpenAd = false;
        private bool _initProcessStarted;

        public static AdMobManager GetInstance()
        {
            return _instance;
        }

        public bool IsSdkInitialized
        {
            get; private set;
        }
        #region UNITY_MONOBEHAVIOUR_METHODS

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
        }

        public void Start()
        {
            if (initializeOnStart)
            {
                Init();
            }
        }

        public void Init()
        {
            if (IsSdkInitialized || _initProcessStarted)
            {
                AdsDebug.Log("AdmobManager: init is in progress or aready initialized");
                return;
            }
            _initProcessStarted = true;
            StartCoroutine(InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            yield return new WaitUntil(() => Application.internetReachability != NetworkReachability.NotReachable);
            try
            {
                AdsDebug.Log("Consent checking start");

                // Create RequestConfiguration without builder
                RequestConfiguration requestConfiguration = new RequestConfiguration();
                requestConfiguration.TagForChildDirectedTreatment = tagForChildDirectedTreatment;
                MaxAdContentRating maxAdContentRating = MaxAdContentRating.Unspecified;

                switch (maxContentRating)
                {
                    case MaxContentRating.G:
                        maxAdContentRating = MaxAdContentRating.G;
                        break;
                    case MaxContentRating.PG:
                        maxAdContentRating = MaxAdContentRating.PG;
                        break;
                    case MaxContentRating.T:
                        maxAdContentRating = MaxAdContentRating.T;
                        break;
                    case MaxContentRating.MA:
                        maxAdContentRating = MaxAdContentRating.MA;
                        break;
                }
                requestConfiguration.MaxAdContentRating = maxAdContentRating;

                // Apply configuration
                MobileAds.SetRequestConfiguration(requestConfiguration);

                MobileAds.SetiOSAppPauseOnBackground(true);
#if UNITY_ANDROID
                AdsDebug.Log("ConsentInformation.ConsentStatus " + ConsentInformation.ConsentStatus);

                if (ConsentInformation.ConsentStatus == ConsentStatus.Required ||
                    ConsentInformation.ConsentStatus == ConsentStatus.Unknown)
                {
                    ConsentDebugSettings consentDebugSettings = new ConsentDebugSettings();
                    if (adData.isTestMode)
                    {
                        consentDebugSettings.DebugGeography = DebugGeography.EEA;
                    }
                    // Set tag for under age of consent.
                    // Here false means users are not under age of consent.
                    ConsentRequestParameters request = new ConsentRequestParameters
                    {
                        TagForUnderAgeOfConsent = false,
                        ConsentDebugSettings = consentDebugSettings
                    };

                    AdsDebug.Log("Consent request created & start update");
                    // Check the current consent information status.
                    ConsentInformation.Update(request, ConsentInfoUpdateCallback);
                }
                else if (ConsentInformation.CanRequestAds())
                {
                    MobileAds.Initialize(HandleInitCompleteAction);
                }
#elif UNITY_IOS
                MobileAds.Initialize(HandleInitCompleteAction);
#endif

            }
            catch (Exception e)
            {
                AdsDebug.LogError("AdMob InitCoroutine exception " + e.Message);
            }
        }

        #endregion

        #region CONCENT_POPUP

        private void ConsentInfoUpdateCallback(FormError consentError)
        {
            try
            {
                AdsDebug.Log("ConsentInfoUpdateCallback");
                if (consentError != null)
                {
                    // Handle the error.
                    AdsDebug.LogError("OnConsentInfoUpdated " + consentError.Message);
                    return;
                }

                AdsDebug.Log("IsConsentFormAvailable " + ConsentInformation.IsConsentFormAvailable());

                AdsDebug.Log("ConsentInformation.ConsentStatus " + ConsentInformation.ConsentStatus);

                // If the error is null, the consent information state was updated.
                // You are now ready to check if a form is available.
                ConsentForm.LoadAndShowConsentFormIfRequired((FormError formError) =>
                {
                    if (formError != null)
                    {
                        // Consent gathering failed.
                        AdsDebug.LogError("FormError: " + formError.Message);
                        return;
                    }
                    // Consent has been gathered.
                    if (ConsentInformation.CanRequestAds())
                    {

                        AdsDebug.Log(TAG, "admob sdk init started");

                        // Initialize the Google Mobile Ads SDK.
                        MobileAds.Initialize(HandleInitCompleteAction);
                    }
                });
            }
            catch (Exception e)
            {
                AdsDebug.LogError("ConsentInfoUpdateCallback exception " + e.Message);
            }
        }

        public void LoadAndShowConsentForm()
        {
            try
            {

                ConsentForm.Load((concentForm, loadError) =>
                {
                    if (loadError != null)
                    {
                        return;
                    }
                    AdsDebug.LogError("Consent Form loaded");
                    concentForm.Show((dismiss) => { });
                });
            }
            catch (Exception e)
            {
                AdsDebug.LogError("LoadAndShowConsentForm exception " + e.Message);
            }
        }

        public void ResetConsent()
        {
            ConsentInformation.Reset();
        }

        public bool IsConsentRequired()
        {
            return ConsentInformation.ConsentStatus == ConsentStatus.Obtained ||
                ConsentInformation.ConsentStatus == ConsentStatus.Required;
        }

        #endregion

        public void SetAdmobAdsID()
        {
            _adIdBanner = adData.GetBannerAdID();

            _adIDInterstitial = adData.GetInterstitialAdID();

            _adIdRewardVideo = adData.GetRewardAdID();

            _adIdAppOpen = adData.GetOpenAdID();

            _adIDRectBanner = adData.GetRectBannerAdID();

            _adIdRewardVideo = adData.GetRewardAdID();

            _adIdRewardedInterstitial = adData.GetRewardedInterstitialAdID();
        }

        public void SetAdmobAdsID(string bannerAdId, string interstitialAdId, string rewardAdId, string appOpenId, string rectBannerAdId, string rewardInterstitialAdId)
        {
            _adIdBanner = bannerAdId;

            _adIDInterstitial = interstitialAdId;

            _adIdRewardVideo = rewardAdId;

            _adIdAppOpen = appOpenId;

            _adIDRectBanner = rectBannerAdId;

            _adIdRewardedInterstitial = rewardInterstitialAdId;
        }

        private void HandleInitCompleteAction(InitializationStatus initstatus)
        {
            try
            {
                // Callbacks from GoogleMobileAds are not guaranteed to be called on
                // main thread.
                // In this example we use MobileAdsEventExecutor to schedule these calls on
                // the next Update() loop.
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    AdsDebug.Log(TAG, "admob sdk init succeed");
                    IsSdkInitialized = true;
                    OnInitializeComplete?.Invoke();
                    if (RequestInterstitialOnInitialize)
                        RequestInterstitial();
                });

                AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
            }
            catch (Exception e)
            {
                AdsDebug.LogError("HandleInitCompleteAction exception " + e.Message);
                IsSdkInitialized = false;
            }

            _initProcessStarted = false;
        }

        public void DestroyAdRequest()
        {
            AdsDebug.Log(TAG, "admob ads request destroy");

            DestroyBanner();
            DestroyRectBanner();
            DestroyAppopenAd();
            DestroyInterstitial();
            DestroyRewardVideoAd();
            DestroyRewardInterstitial();

            AdsDebug.Log(TAG, "admob ads request destroy success");
        }

        // Returns an ad request with custom ad targeting.
        private AdRequest CreateAdRequest()
        {
            if (!CanRequestAd())
                return null;

            AdRequest adRequest = new AdRequest();
            return adRequest;
        }

        public bool CanRequestAd()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                return false;
            }
            try
            {
                if (IsAdRemoved() || !IsSdkInitialized)
                {
                    return false;
                }
#if UNITY_ANDROID
                return adData.IsFromValidStoreInstallation() && ConsentInformation.CanRequestAds();
#elif UNITY_IOS || UNITY_EDITOR
                return true;
#endif
            }
            catch (Exception e)
            {
                AdsDebug.LogError("CanRequestAd exception " + e.Message);
                return false;
            }
        }

        #region APP_OPEN_AD

        public void LoadAppOpenAd(OnAdStatusDelegate adStatusDelegate = null)
        {
            try
            {

                if (IsAppOpenAdAvailable)
                {
                    AdsDebug.Log("App open ad is already available to show.");
                    return;
                }

                if (!CanRequestAd() || _adIdAppOpen.IsNullOrEmpty())
                {
                    return;
                }

                // Clean up the old ad before loading a new one.
                DestroyAppopenAd();

                AdsDebug.Log("Loading the app open ad.");
                AppOpenAdStatus = adStatusDelegate;
                // Create our request used to load the ad.
                var adRequest = new AdRequest();

                // send the request to load the ad.
                AppOpenAd.Load(_adIdAppOpen, adRequest, OnAppOpenLoadCompleted);
            }
            catch (Exception e)
            {
                AdsDebug.LogError("LoadAppOpenAd exception " + e.Message);
            }
        }

        private void OnAppOpenLoadCompleted(AppOpenAd appOpenAd, LoadAdError error)
        {
            try
            {
                if (error != null || appOpenAd == null)
                {
                    AdsDebug.LogError("app open ad failed to load an ad " +
                                   "with error : " + error);
                    MobileAdsEventExecutor.ExecuteInUpdate(() =>
                    {
                        _isAppOpenAdLoaded = false;
                        AppOpenAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                    });
                    return;
                }

                AdsDebug.Log("App open ad loaded with response : " + appOpenAd.GetResponseInfo());

                // App open ads can be preloaded for up to 4 hours.
                _expireTime = DateTime.Now + TimeSpan.FromHours(4);

                _appOpenAd = appOpenAd;
                HandleAppOpenAdCallback(appOpenAd);
                _isAppOpenAdLoaded = true;

                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    AppOpenAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                    AdsDebug.Log("OnAppOpenLoadCompleted called.");
                });

            }
            catch (Exception e)
            {
                AdsDebug.LogError("OnAppOpenLoadCompleted exception " + e.Message);
                // if error is not null, the load request failed.
            }
        }

        private void HandleAppOpenAdCallback(AppOpenAd ad)
        {
            try
            {
                AdsDebug.Log("HandleAppOpenAdCallback called.");
                // Raised when an ad opened full screen content.

                // Raised when the ad closed full screen content.
                ad.OnAdFullScreenContentClosed += OnAppOpenAdClosed;

                // Raised when the ad failed to open full screen content.
                ad.OnAdFullScreenContentFailed += OnAppOpenAddFailed;
            }
            catch (Exception e)
            {
                AdsDebug.LogError("HandleAppOpenAdCallback exception " + e.Message);
            }
        }

        private void OnAppStateChanged(AppState state)
        {
            AdsDebug.Log("App State changed to : " + state);

            if (state == AppState.Background)
            {
                return;
            }

            if (_isBannerOpened)
            {
                _isBannerOpened = false;
                _canShowOpenAd = true;
                return;
            }

            if (_forceDontShowOpenAd)
            {
                AdsDebug.Log("App open ad showing is forced to be disabled.");
                return;
            }

            if (_canShowOpenAd)
            {
                OnAppStateChange?.Invoke(state);
            }
        }

        /// <summary>
        /// Shows the app open ad.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (_canShowOpenAd && IsAppOpenAdAvailable)
            {
                AdsDebug.Log("Showing app open ad.");
                _appOpenAd.Show();
            }
            else
            {
                AdsDebug.LogError("App open ad is not ready yet.");
            }
        }

        public void SetAppOpenAdCanShow(bool show)
        {
            _forceDontShowOpenAd = !show;
        }

        private void OnAppOpenAdClosed()
        {
            AdsDebug.Log("App open ad full screen content closed.");

            // Reload the ad so that we can show another as soon as possible.
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    DestroyAppopenAd();
                    _isAppOpenAdLoaded = false;

                    if (AppOpenAdAutoLoad)
                        LoadAppOpenAd();
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("OnAppOpenAdClosed exception " + e.Message);
                }
            });
        }

        private void OnAppOpenAddFailed(AdError error)
        {
            // Reload the ad so that we can show another as soon as possible.

            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    DestroyAppopenAd();

                    _isAppOpenAdLoaded = false;
                    if (AppOpenAdAutoLoad)
                        LoadAppOpenAd();
                    AdsDebug.LogError("App open ad failed to open full screen content with error : " + error);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("OnAppOpenAddFailed exception " + e.Message);
                }
            });
        }

        public void DestroyAppopenAd()
        {
            if (_appOpenAd != null)
            {
                _appOpenAd.OnAdFullScreenContentClosed -= OnAppOpenAdClosed;
                _appOpenAd.OnAdFullScreenContentFailed -= OnAppOpenAddFailed;
                _appOpenAd.Destroy();
                _appOpenAd = null;
            }
        }

        #endregion

        #region BANNER_AD

        public void RequestBanner(BannerAdPosition bannerPosition, BannerAdSize bannerAdSize = BannerAdSize.FullWidth, OnAdStatusDelegate AdStatusDelegate = null)
        {
            try
            {

                if (!CanRequestAd())
                    return;
                if (!LoadNewBannerOnEachRequest)
                {
                    if (_bannerView != null && !_bannerView.IsDestroyed)
                    {
                        BannerAdStatus = AdStatusDelegate;
                        BannerAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                        return;
                    }
                }


                DestroyBanner();

                BannerAdStatus = AdStatusDelegate;

                _bannerView = new BannerView(_adIdBanner, GetAdSize(bannerAdSize), (AdPosition)((int)bannerPosition));


                _bannerView.OnBannerAdLoaded += this.HandleBannerOnAdLoaded;  // Called when an ad request has successfully loaded.
                _bannerView.OnBannerAdLoadFailed += this.HandleOnAdFailedToLoad; // Called when an ad request failed to load.
                _bannerView.OnAdClicked += OnBannerAdClicked;
                _bannerView.OnAdFullScreenContentClosed += this.HandleOnAdClosed;// Called when the user returned from the app after an ad click.

                AdRequest request = CreateAdRequest();
                if (request != null)
                {
                    _bannerView.LoadAd(request);
                }
            }
            catch (Exception e)
            {
                AdsDebug.LogError("RequestBanner exception " + e.Message);
            }
        }

        /// <summary>
        /// show the banner ADS
        /// </summary>
        public void ShowBanner()
        {
            if (!IsAdRemoved())
            {
                if (_bannerView != null)
                {
                    AdsDebug.Log(TAG, "ShowBanner");

                    _bannerView.Show();
                }
            }
        }

        /// <summary>
        /// Hide the banner ADS
        /// </summary>
        public void HideBanner()
        {
            if (_bannerView != null)
            {
                AdsDebug.Log(TAG, "hide Banner");

                _bannerView.Hide();
            }
        }

        /// <summary>
        /// Destroy the banner View Object.....
        /// </summary>
        /// <param name="bannerView"></param>
        public void DestroyBanner()
        {
            if (_bannerView != null)
            {
                _bannerView.OnBannerAdLoaded -= HandleBannerOnAdLoaded;
                _bannerView.OnBannerAdLoadFailed -= HandleOnAdFailedToLoad;
                _bannerView.OnAdClicked -= OnBannerAdClicked;
                _bannerView.OnAdFullScreenContentClosed -= HandleOnAdClosed;

                _bannerView.Destroy();
                _bannerView = null;
                AdsDebug.Log(TAG, "Banner Destroy");
            }
        }

        public void HandleBannerOnAdLoaded()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "BannedAd loaded");
                    HideBanner();
                    BannerAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleBannerOnAdLoaded exception " + e.Message);
                }
            });
        }

        public void HandleOnAdFailedToLoad(LoadAdError loadAdError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "BannedAd FailedToLoad");
                    BannerAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleOnAdFailedToLoad exception " + e.Message);
                }
            });
        }

        public void HandleOnAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "BannedAd OnAdClosed");
                    DestroyBanner();

                    BannerAdStatus?.Invoke(AdStatusCode.AdClosed);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleOnAdClosed exception " + e.Message);
                }

            });
        }

        #endregion

        #region RECT_BANNER_AD

        public void RequestRectBanner(BannerAdPosition bannerPosition, BannerAdSize bannerSize = BannerAdSize.MediumRectangle, OnAdStatusDelegate AdStatusDelegate = null, int customWidth = 300, int customHeight = 250)
        {
            try
            {

                if (!CanRequestAd() || _adIDRectBanner.IsNullOrEmpty())
                {
                    return;
                }

                DestroyRectBanner();

                RectBannerAdStatus = AdStatusDelegate;

                _rectBannerView = new BannerView(_adIDRectBanner, GetAdSize(bannerSize, customWidth, customHeight), (AdPosition)((int)bannerPosition));

                _rectBannerView.OnBannerAdLoaded += HandleRectBannerOnAdLoaded;// Called when an ad request has successfully loaded.
                _rectBannerView.OnBannerAdLoadFailed += HandleRectBannerOnAdFailedToLoad;// Called when an ad request failed to load.
                _rectBannerView.OnAdFullScreenContentClosed += HandleRectBannerOnAdClosed;
                _rectBannerView.OnAdClicked += OnBannerAdClicked;// Called when the user returned from the app after an ad click.

                AdRequest request = CreateAdRequest();
                if (request != null)
                {
                    _rectBannerView.LoadAd(request);
                }
            }
            catch (Exception e)
            {
                AdsDebug.LogError("RequestRectBanner exception " + e.Message);
            }
        }

        public void HandleRectBannerOnAdLoaded()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "RectBannedAd loaded");

                    HideRectBanner();
                    RectBannerAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRectBannerOnAdLoaded exception " + e.Message);
                }
            });
        }

        public void HandleRectBannerOnAdFailedToLoad(LoadAdError loadAdError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    RectBannerAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                    _canShowOpenAd = true;
                    AdsDebug.Log(TAG, "RectBannedAd FailedToLoad" + loadAdError.GetMessage());
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRectBannerOnAdFailedToLoad exception " + e.Message);
                }

            });
        }

        public void HandleRectBannerOnAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "RectBannedAd OnAdClosed");
                    DestroyRectBanner();
                    RectBannerAdStatus?.Invoke(AdStatusCode.AdClosed);
                    _canShowOpenAd = true;
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRectBannerOnAdClosed exception " + e.Message);
                }
            });
        }

        /// <summary>
        /// Hide the rect banner ADS
        /// </summary>
        public void ShowRectBanner()
        {
            if (!IsAdRemoved())
            {
                if (_rectBannerView != null)
                {
                    AdsDebug.Log("ShowRectBanner", "Show called");
                    _rectBannerView.Show();
                }
            }
        }

        /// <summary>
        /// Hide the banner ADS
        /// </summary>
        public void HideRectBanner()
        {
            AdsDebug.Log("HideRectBanner", "Hide called");

            if (_rectBannerView != null)
                _rectBannerView.Hide();
        }

        /// <summary>
        /// Destroy the banner View Object.....
        /// </summary>
        /// <param name="bannerView"></param>
        public void DestroyRectBanner()
        {
            if (_rectBannerView != null)
            {
                _rectBannerView.OnBannerAdLoaded -= HandleRectBannerOnAdLoaded;
                _rectBannerView.OnBannerAdLoadFailed -= HandleRectBannerOnAdFailedToLoad;
                _rectBannerView.OnAdFullScreenContentClosed -= HandleRectBannerOnAdClosed;
                _rectBannerView.OnAdClicked -= OnBannerAdClicked;
                _rectBannerView.Destroy();
                _rectBannerView = null;
            }
        }

        #endregion

        #region BANNER ADS COMMON

        private void OnBannerAdClicked()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _isBannerOpened = true;
                _canShowOpenAd = false;
            });
        }

        private AdSize GetAdSize(BannerAdSize bannerAdSize, int customWidth = 300, int customHeight = 250)
        {
            switch (bannerAdSize)
            {
                case BannerAdSize.Banner:
                    return AdSize.Banner;
                case BannerAdSize.MediumRectangle:
                    return AdSize.MediumRectangle;
                case BannerAdSize.IABBanner:
                    return AdSize.IABBanner;
                case BannerAdSize.Leaderboard:
                    return AdSize.Leaderboard;
                case BannerAdSize.FullWidth:
                    return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                case BannerAdSize.Custom:
                    return new AdSize(customWidth, customHeight);
                default:
                    return AdSize.Banner;
            }
        }

        #endregion

        #region INTERSTITIAL_AD

        public void RequestInterstitial(OnAdStatusDelegate onAdStatusDelegate = null)
        {
            RequestInterstitial(_adIDInterstitial, onAdStatusDelegate);
        }

        public void RequestInterstitial(string adId, OnAdStatusDelegate onAdStatusDelegate = null)
        {
            try
            {
                InterstitialAdStatus = onAdStatusDelegate;

                if (!CanRequestAd() || adId.IsNullOrEmpty())
                {
                    InterstitialAdStatus?.Invoke(AdStatusCode.SDKInitFailed);
                    return;
                }

                if (_interstitial != null && _interstitial.CanShowAd())
                {
                    AdsDebug.Log(TAG, "RequestInterstitial already loaded");
                    InterstitialAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                    return;
                }

                // Create an empty ad request.
                AdRequest request = CreateAdRequest();
                if (request != null)
                {
                    InterstitialAd.Load(adId, request, OnIntertitialAdLoadComplete);
                }
            }
            catch (Exception e)
            {
                AdsDebug.LogError("RequestInterstitial exception " + e.Message);
            }
        }

        public void ShowInterstitial()
        {
            if (!IsAdRemoved())
            {
                if (IsInterstitialAdLoaded())
                {
                    _canShowOpenAd = false;

                    _interstitial.Show();
                    AdsDebug.Log(TAG, "ShowInterstitial");
                }
                else
                {
                    RequestInterstitial();
                }
            }
        }

        private void OnIntertitialAdLoadComplete(InterstitialAd interestitial, LoadAdError loadError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "OnIntertitialAdLoadComplete called.");

                    if (interestitial == null)
                    {
                        AdsDebug.LogError("RequestInterstitial interstitial is null");
                        return;
                    }
                    if (loadError != null)
                    {
                        AdsDebug.Error("RequestInterstitial ", loadError.GetMessage());
                        return;
                    }

                    _interstitial = interestitial;

                    if (_interstitial != null)
                    {
                        InterstitialAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                        // Called when an ad is shown.
                        _interstitial.OnAdFullScreenContentOpened += HandleInterstitialOnAdOpened;
                        // Called when the ad is closed.
                        _interstitial.OnAdFullScreenContentClosed += HandleOnInterstitialAdClosed;
                        _interstitial.OnAdFullScreenContentFailed += HandleOnAdFullScreenContentFailed;
                    }
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("OnIntertitialAdLoadComplete exception " + e.Message);
                }

            });
        }

        public bool IsInterstitialAdLoaded()
        {
            if (_interstitial == null)
            {
                return false;
            }
            return _interstitial.CanShowAd();
        }

        public void HandleInterstitialOnAdOpened()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                AdsDebug.Log(TAG, "Interstitial OnAdOpened");
                _canShowOpenAd = false;
            });
        }

        public void HandleOnInterstitialAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "Interstitial AdClosed");
                    RequestInterstitial();
                    _canShowOpenAd = true;
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleOnInterstitialAdClosed exception " + e.Message);
                }

            });
        }

        private void HandleOnAdFullScreenContentFailed(AdError error)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _canShowOpenAd = true;
                AdsDebug.Error("HandleOnAdFullScreenContentFailed", error.GetMessage());
            });
        }

        public void DestroyInterstitial()
        {
            if (_interstitial != null)
            {
                _interstitial.OnAdFullScreenContentOpened -= HandleInterstitialOnAdOpened;
                _interstitial.OnAdFullScreenContentClosed -= HandleOnInterstitialAdClosed;
                _interstitial.OnAdFullScreenContentFailed -= HandleOnAdFullScreenContentFailed;
                _interstitial.Destroy();
                _interstitial = null;
            }
        }
        #endregion

        #region REWARDED_INTERSTITIAL_AD

        public void RequestRewardInterstitial(OnAdStatusDelegate AdStatusDelegate)
        {
            try
            {
                AdsDebug.Log(TAG, "RequestRewardInterstitial called.");
                RewardInterstitialAdStatus = AdStatusDelegate;

                DestroyRewardInterstitial();

                if (!CanRequestAd() || _adIdRewardedInterstitial.IsNullOrEmpty())
                {
                    RewardInterstitialAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                    return;
                }

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    RewardInterstitialAdStatus?.Invoke(AdStatusCode.NoInternet);
                    return;
                }

                AdRequest request = CreateAdRequest();
                if (request != null)
                {
                    RewardedInterstitialAd.Load(_adIdRewardedInterstitial, request, RewardInterstitialAdLoadCallBack);
                }
            }
            catch (Exception e)
            {
                AdsDebug.LogError("RequestRewardInterstitial exception " + e.Message);
            }
        }

        private void RewardInterstitialAdLoadCallBack(RewardedInterstitialAd rewardedInterstitialAd, LoadAdError error)
        {
            try
            {
                MobileAdsEventExecutor.ExecuteInUpdate(() =>
                {
                    if (error != null)
                    {
                        DestroyRewardInterstitial();

                        RewardInterstitialAdStatus?.Invoke(AdStatusCode.ADLoadFailed);

                        AdsDebug.Log(TAG, "RequestRewardInterstitial error " + error.GetMessage());
                    }

                    if (rewardedInterstitialAd == null)
                    {
                        RewardInterstitialAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                        return;
                    }

                    _rewardedInterstitialAd = rewardedInterstitialAd;

                    AdsDebug.Log(TAG, "RequestRewardInterstitial loaded " + "");

                    // Register for ad events.

                    _rewardedInterstitialAd.OnAdFullScreenContentClosed += HandleRewardAdClosed;
                    _rewardedInterstitialAd.OnAdFullScreenContentFailed += HandleOnRewardedInterstitialAdFailedToShow;

                    RewardInterstitialAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                });

            }
            catch (Exception e)
            {
                AdsDebug.LogError("RewardInterstitialAdLoadCallBack exception " + e.Message);
            }
        }

        /// <summary>
        /// check wheteher reward add is ready or not
        /// </summary>
        /// <returns></returns>
        public bool IsRewardInnterstitialAdLoaded()
        {
            if (_rewardedInterstitialAd == null)
            {
                return false;
            }
            else
            {
                return _rewardedInterstitialAd.CanShowAd();
            }
        }

        public void ShowRewardInterstitial()
        {
            if (!IsAdRemoved())
            {
                if (_rewardedInterstitialAd != null)
                {
                    AdsDebug.Log(TAG, "Show RequestRewardInterstitial");
                    _canShowOpenAd = false;
                    _rewardedInterstitialAd.Show(EarnedRewardCallbackForRewardInterstitial);
                }
            }
        }

        private void EarnedRewardCallbackForRewardInterstitial(Reward reward)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    RewardInterstitialAdStatus?.Invoke(AdStatusCode.RewardGranted);
                    _canShowOpenAd = true;
                    AdsDebug.Log(TAG, "RequestRewardInterstitial give reward");
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("EarnedRewardCallbackForRewardInterstitial exception " + e.Message);
                }


            });
        }

        private void HandleRewardAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                _canShowOpenAd = true;
                RewardInterstitialAdStatus?.Invoke(AdStatusCode.AdClosed);
            });
        }

        private void HandleOnRewardedInterstitialAdFailedToShow(AdError adError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    _canShowOpenAd = true;
                    RewardInterstitialAdStatus?.Invoke(AdStatusCode.AdClosed);
                    AdsDebug.Log(TAG, "RequestRewardInterstitial FailedToShow " + adError.GetMessage());
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleOnRewardedInterstitialAdFailedToShow exception " + e.Message);
                }

            });
        }

        public void DestroyRewardInterstitial()
        {
            if (_rewardedInterstitialAd != null)
            {
                _rewardedInterstitialAd.OnAdFullScreenContentClosed -= HandleRewardAdClosed;
                _rewardedInterstitialAd.OnAdFullScreenContentFailed -= HandleOnRewardedInterstitialAdFailedToShow;
                _rewardedInterstitialAd.Destroy();
                _rewardedInterstitialAd = null;
            }
        }
        #endregion

        #region REWARDED_VIDEO_ADD

        public void RequestRewardedAd(OnAdStatusDelegate AdStatusDelegate)
        {
            try
            {
                RewardVideoAdStatus = AdStatusDelegate;

                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    RewardVideoAdStatus?.Invoke(AdStatusCode.NoInternet);
                    return;
                }

                if (!CanRequestAd() || _adIdRewardVideo.IsNullOrEmpty())
                {
                    RewardVideoAdStatus?.Invoke(AdStatusCode.SDKInitFailed);
                    return;
                }
                AdRequest request = CreateAdRequest();
                if (request != null)
                {
                    RewardedAd.Load(_adIdRewardVideo, request, OnRewardLoadCompleted);
                }
            }
            catch (Exception e)
            {
                AdsDebug.LogError($"RequestRewardedAd: Failed to load {e.Message}");
            }
        }

        private void OnRewardLoadCompleted(RewardedAd rewardAd, LoadAdError adLoadError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                if (adLoadError != null)
                {
                    RewardAdLoadFailed();
                    AdsDebug.Error("Load RewardedAd", adLoadError.GetMessage() + " " + adLoadError.ToString());
                    return;
                }

                _rewardedAd = rewardAd;

                // Called when an ad request has successfully loaded.
                HandleRewardedAdLoaded();

                //Called when an ad is shown.
                _rewardedAd.OnAdFullScreenContentOpened += HandleRewardedAdOpening;

                // Called when an ad request failed to show.
                _rewardedAd.OnAdFullScreenContentFailed += HandleRewardedAdFailedToShow;

                // Called when the ad is closed.
                _rewardedAd.OnAdFullScreenContentClosed += HandleRewardedAdClosed;
            });
        }

        /// <summary>
        /// check wheteher reward add is ready or not
        /// </summary>
        /// <returns></returns>
        public bool IsRewardVideoAddLoaded()
        {
            if (_rewardedAd == null)
                return false;

            return _rewardedAd.CanShowAd();
        }

        /// <summary>
        /// User start to watch video ADS
        /// </summary>
        /// <returns></returns>
        public void ShowRewardAd()
        {
            if (IsRewardVideoAddLoaded())
            {
                _rewardedAd.Show(HandleUserEarnedReward);
            }
        }

        public void HandleRewardedAdLoaded()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "Rewarded Video Ad Loaded");
                    RewardVideoAdStatus?.Invoke(AdStatusCode.ADLoadSuccess);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRewardedAdLoaded exception " + e.Message);
                }

            });
        }

        public void HandleRewardedAdOpening()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "Rewarded Video Ad open");
                    _canShowOpenAd = false;
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRewardedAdOpening exception " + e.Message);
                }
            });

        }

        public void HandleRewardedAdFailedToShow(AdError adError)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "Rewarded Video Ad Failed to Show");
                    _canShowOpenAd = true;
                    RewardVideoAdStatus?.Invoke(AdStatusCode.AdClosed);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRewardedAdFailedToShow exception " + e.Message);
                }
            });
        }

        public void HandleRewardedAdClosed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    AdsDebug.Log(TAG, "Rewarded Video Ad closed!");
                    _canShowOpenAd = true;
                    RewardVideoAdStatus?.Invoke(AdStatusCode.AdClosed);
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleRewardedAdClosed exception " + e.Message);
                }
            });
        }

        public void HandleUserEarnedReward(Reward reward)
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    RewardVideoAdStatus?.Invoke(AdStatusCode.RewardGranted);
                    _canShowOpenAd = true;
                    AdsDebug.Log(TAG, "Rewarded Video Ad reward the user!");
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("HandleUserEarnedReward exception " + e.Message);
                }
            });
        }

        private void RewardAdLoadFailed()
        {
            MobileAdsEventExecutor.ExecuteInUpdate(() =>
            {
                try
                {
                    RewardVideoAdStatus?.Invoke(AdStatusCode.ADLoadFailed);
                    _canShowOpenAd = true;
                    AdsDebug.Log(TAG, "Rewarded Video Ad Load Failed!");
                }
                catch (Exception e)
                {
                    AdsDebug.LogError("RewardAdLoadFailed exception " + e.Message);
                }
            });
        }

        public void DestroyRewardVideoAd()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.OnAdFullScreenContentOpened -= HandleRewardedAdOpening;
                _rewardedAd.OnAdFullScreenContentFailed -= HandleRewardedAdFailedToShow;
                _rewardedAd.OnAdFullScreenContentClosed -= HandleRewardedAdClosed;
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
        }

        #endregion

        #region Remove Ad Code

        public static bool IsAdRemoved()
        {
            return GamePlayerPrefs.GetBool(PREF_AD_REMOVED, false);
        }

        public static void RemoveAd()
        {
            GamePlayerPrefs.SetBool(PREF_AD_REMOVED, true);
            GetInstance().DestroyAdRequest();
        }

        public static void RemoveSavedPrefs()
        {
            if (GamePlayerPrefs.HasKey(PREF_AD_REMOVED))
                GamePlayerPrefs.DeleteKey(PREF_AD_REMOVED);
        }

        #endregion
    }
}