using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using AdsManager;
using Utils;

/// <summary>
/// Main Menu (Home) controller — shows player profile overview,
/// navigation to Profile, Store, Team Selection, Settings, and handles Logout.
/// Includes battle cost (100 coins entry fee) and win reward (200 coins) display.
/// </summary>
public class HomeMenuController : MonoBehaviour
{
    [Header("Profile Display")]
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI winsText;
    [SerializeField] private TextMeshProUGUI lossesText;
    [SerializeField] private Image           xpBar;          // fill bar (0-1)
    [SerializeField] private TextMeshProUGUI xpText;         // "120 / 300 XP"



    [Header("Battle Info Panel")]
    [SerializeField] private TextMeshProUGUI battleCostText;    // "Entry: 🪙 100"
    [SerializeField] private TextMeshProUGUI battleRewardText;  // "Win: +🪙 200"
    [SerializeField] private TextMeshProUGUI insufficientFundsText; // shown when can't afford

    [Header("Buttons")]
    [SerializeField] private Button          playButton;
    [SerializeField] private Button          profileButton;   // NEW — opens PlayerProfileScene
    [SerializeField] private Button          storeButton;

    [SerializeField] private Button          settingsButton;
    [SerializeField] private Button          logoutButton;

    [Header("Logout Confirm Popup")]
    [SerializeField] private GameObject      logoutPopup;
    [SerializeField] private Button          logoutYesBtn;
    [SerializeField] private Button          logoutNoBtn;

    [Header("Not Enough Coins Popup")]
    [SerializeField] private GameObject      noCoinsPopup;
    [SerializeField] private TextMeshProUGUI noCoinsText;
    [SerializeField] private Button          noCoinsOkBtn;

    [Header("Misc")]
    [SerializeField] private GameObject      noAdsButton;
    private bool _isIAPOpen = false;

    private void Awake()
    {
        // If no profile exists, redirect to profile creation
        if (PlayerProfileManager.GetInstance() != null &&
            !PlayerProfileManager.GetInstance().IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
        }
    }

    private IEnumerator Start()
    {
        if (logoutPopup  != null) logoutPopup.SetActive(false);
        if (noCoinsPopup != null) noCoinsPopup.SetActive(false);
        if (insufficientFundsText != null) insufficientFundsText.gameObject.SetActive(false);

        if (PlayerProfileManager.GetInstance() == null)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
            yield break;
        }

        RefreshAllUI();

        // Wire up buttons
        if (playButton    != null) playButton.onClick.AddListener(OnPlayButtonClick);
        if (profileButton != null) profileButton.onClick.AddListener(OnProfileButtonClick);
        if (storeButton   != null) storeButton.onClick.AddListener(OnStoreButtonClick);

        if (settingsButton!= null) settingsButton.onClick.AddListener(OnSettingsButtonClick);
        if (logoutButton  != null) logoutButton.onClick.AddListener(OnLogoutButtonClick);
        if (logoutYesBtn  != null) logoutYesBtn.onClick.AddListener(OnLogoutConfirm);
        if (logoutNoBtn   != null) logoutNoBtn.onClick.AddListener(OnLogoutCancel);
        if (noCoinsOkBtn  != null) noCoinsOkBtn.onClick.AddListener(() => { if (noCoinsPopup != null) noCoinsPopup.SetActive(false); });

        // Static battle info labels
        if (battleCostText   != null) battleCostText.text   = $"Entry Fee: 🪙 {PlayerProfileManager.BATTLE_COST}";
        if (battleRewardText != null) battleRewardText.text = $"Win Reward: +🪙 {PlayerProfileManager.COINS_PER_WIN}";

        // Subscribe to profile events
        PlayerProfileManager.OnProfileChanged += RefreshAllUI;
        PlayerProfileManager.OnCoinsChanged   += RefreshCoinsAndPlayButton;
        PlayerProfileManager.OnLevelChanged   += RefreshLevelUI;


        // IAP
        PurchaseController.OnRemoveAd += HideNoAdsButton;
        if (PreferenceHelper.IsAdRemoved() && noAdsButton != null)
            noAdsButton.SetActive(false);

        // Ads banner
        if (AdMobManager.GetInstance() != null)
        {
            yield return new WaitUntil(() => AdMobManager.GetInstance().IsSdkInitialized);
            AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom, AdStatusDelegate: OnAdStatus);
        }
    }

    private void OnDestroy()
    {
        PlayerProfileManager.OnProfileChanged -= RefreshAllUI;
        PlayerProfileManager.OnCoinsChanged   -= RefreshCoinsAndPlayButton;
        PlayerProfileManager.OnLevelChanged   -= RefreshLevelUI;

        PurchaseController.OnRemoveAd         -= HideNoAdsButton;
    }

    private void OnAdStatus(AdStatusCode code)
    {
        if (code == AdStatusCode.ADLoadSuccess && !_isIAPOpen && AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().ShowBanner();
    }

    // ─── UI Refresh ──────────────────────────────────────────────

    private void RefreshAllUI()
    {
        RefreshCoinsAndPlayButton();
        RefreshLevelUI();


        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (usernameText != null) usernameText.text = $"👤  {p.Username}";
        if (winsText     != null) winsText.text  = $"✅ {p.Wins} Wins";
        if (lossesText   != null) lossesText.text = $"❌ {p.Losses} Losses";
    }

    private void RefreshCoinsAndPlayButton()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (coinsText != null) coinsText.text = $"🪙  {p.Coins}";

        // Pulse coins text when updated
        if (coinsText != null)
            coinsText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5, 0.5f);

        // Show "not enough coins" warning under play button
        bool canAfford = p.CanAffordBattle;
        if (insufficientFundsText != null)
        {
            insufficientFundsText.gameObject.SetActive(!canAfford);
            insufficientFundsText.text = $"Need 🪙 {PlayerProfileManager.BATTLE_COST} to play!";
        }

        // Grey out play button if can't afford
        if (playButton != null)
        {
            var colors = playButton.colors;
            colors.normalColor = canAfford
                ? new Color(0.15f, 0.75f, 0.3f)    // green — ready
                : new Color(0.5f, 0.5f, 0.5f);      // grey — insufficient funds
            playButton.colors = colors;
        }
    }

    private void RefreshLevelUI()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (levelText != null) levelText.text = $"Lv. {p.Level}";

        float progress = p.GetLevelProgress();
        if (xpBar != null)
            xpBar.DOFillAmount(progress, 0.5f).SetEase(Ease.OutCubic);

        if (xpText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL)
                xpText.text = "MAX LEVEL 🏆";
            else
                xpText.text = $"{p.XP} / {p.XP + p.GetXPToNextLevel()} XP";
        }
    }



    // ─── Navigation ──────────────────────────────────────────────

    /// <summary>Play — deducts 100 coins entry fee then goes to Battle Scene directly.</summary>
    public void OnPlayButtonClick()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        // Check coin balance first
        if (!profile.CanAffordBattle)
        {
            ShowNoCoinsPopup();
            return;
        }

        // Deduct entry fee
        profile.SpendCoinsForBattle();

        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();

        SceneManager.LoadScene(Constants.SCENE_POKEMON);
    }

    public void OnProfileButtonClick()
    {
        SceneManager.LoadScene(Constants.SCENE_PROFILE);
    }

    public void OnStoreButtonClick()
    {
        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();

        SceneManager.LoadScene(Constants.SCENE_STORE);
    }



    public void OnSettingsButtonClick()
    {
        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();
        if (SettingView.GetInstance() != null)
        {
            MenuView.GetInstance()?.Hide();
            SettingView.GetInstance().Show();
        }
    }

    // ─── Not Enough Coins Popup ──────────────────────────────────

    private void ShowNoCoinsPopup()
    {
        var p = PlayerProfileManager.GetInstance();
        if (noCoinsPopup == null) return;

        if (noCoinsText != null)
            noCoinsText.text = $"You need 🪙 {PlayerProfileManager.BATTLE_COST} coins to enter battle!\n\nYou have: 🪙 {p?.Coins ?? 0}\n\nVisit the Store or win battles to earn more coins.";

        noCoinsPopup.SetActive(true);
        noCoinsPopup.transform.localScale = Vector3.zero;
        noCoinsPopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack);
    }

    // ─── Logout Flow ─────────────────────────────────────────────

    public void OnLogoutButtonClick()
    {
        if (logoutPopup == null) return;
        logoutPopup.SetActive(true);
        logoutPopup.transform.localScale = Vector3.zero;
        logoutPopup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
    }

    public void OnLogoutConfirm()
    {
        logoutPopup?.SetActive(false);
        PlayerProfileManager.GetInstance()?.Logout();
        SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
    }

    public void OnLogoutCancel()
    {
        logoutPopup?.SetActive(false);
    }

    // ─── IAP / Ads ───────────────────────────────────────────────

    private void HideNoAdsButton()
    {
        LoadingView.GetInstance()?.Hide();
        if (noAdsButton != null) noAdsButton.SetActive(false);
    }
}
