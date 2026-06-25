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

    [Header("Bet Selector (Static UI)")]
    [SerializeField] private GameObject      betSelectorContainer;
    [SerializeField] private Button[]        betButtons;
    [SerializeField] private Image[]         betImages;
    [SerializeField] private TextMeshProUGUI[] betTexts; // Win/reward text components

    private bool _isIAPOpen = false;

    private void Awake()
    {
        Time.timeScale = 1f; // Ensure timeScale is active on menu load
        // If no profile exists, redirect to profile creation
        if (PlayerProfileManager.GetInstance() != null &&
            !PlayerProfileManager.GetInstance().IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
        }
    }

    private IEnumerator Start()
    {
        // Immediately disable raycast target on button text components to avoid blocking click events
        DisableButtonTextRaycasts(gameObject);
        if (logoutPopup != null) DisableButtonTextRaycasts(logoutPopup);
        if (noCoinsPopup != null) DisableButtonTextRaycasts(noCoinsPopup);

        if (logoutPopup  != null) logoutPopup.SetActive(false);
        if (noCoinsPopup != null) noCoinsPopup.SetActive(false);
        if (insufficientFundsText != null) insufficientFundsText.gameObject.SetActive(false);

        // Hide legacy cost texts since we use the Bet Selector UI
        if (battleCostText != null) battleCostText.gameObject.SetActive(false);
        if (battleRewardText != null) battleRewardText.gameObject.SetActive(false);

        var profileInstance = PlayerProfileManager.GetInstance();
        if (profileInstance == null)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
            yield break;
        }

        profileInstance.LoadProfile(); // Force reload latest saved data to avoid caching/stale XP issues

        try
        {
            RefreshAllUI();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HomeMenuController] Exception in RefreshAllUI: {ex}");
        }

        // Verify Bet Affordability
        var profile = PlayerProfileManager.GetInstance();
        if (profile != null)
        {
            profile.VerifySelectedBetAffordability();
        }

        // Disable raycasts for static container
        if (betSelectorContainer != null)
        {
            DisableButtonTextRaycasts(betSelectorContainer);
        }

        RefreshBetSelectionUI();

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
        if (winsText     != null) winsText.gameObject.SetActive(false);
        if (lossesText   != null) lossesText.gameObject.SetActive(false);
    }

    private void RefreshCoinsAndPlayButton()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (coinsText != null) coinsText.text = $"🪙  {p.Coins}";

        // Pulse coins text when updated
        if (coinsText != null)
            coinsText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5, 0.5f).SetUpdate(true);

        // Show "not enough coins" warning under play button
        bool canAfford = p.CanAffordBattle;
        if (insufficientFundsText != null)
        {
            insufficientFundsText.gameObject.SetActive(!canAfford);
            insufficientFundsText.text = $"Need 🪙 {p.SelectedBet} to play!";
        }

        RefreshBetSelectionUI();
    }

    private void RefreshLevelUI()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (levelText != null) levelText.text = $"Lv. {p.Level}";

        float progress = p.GetLevelProgress();
        if (xpBar != null)
            xpBar.DOFillAmount(progress, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);

        if (xpText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL && p.GetXPToNextLevel() <= 0)
                xpText.text = "GAME COMPLETED 🏆";
            else
                xpText.text = $"{p.XP} / {p.XP + p.GetXPToNextLevel()} XP";
        }
    }

    // ─── Navigation ──────────────────────────────────────────────

    /// <summary>Play — transitions to Battle Prep Scene.</summary>
    public void OnPlayButtonClick()
    {
        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();

        SceneManager.LoadScene(Constants.SCENE_BATTLE_PREP);
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

        int needed = p != null ? p.SelectedBet : 250;
        if (noCoinsText != null)
            noCoinsText.text = $"You need 🪙 {needed} coins to enter battle!\n\nYou have: 🪙 {p?.Coins ?? 0}\n\nVisit the Store or win battles to earn more coins.";

        noCoinsPopup.SetActive(true);
        noCoinsPopup.transform.localScale = Vector3.zero;
        noCoinsPopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void OnNoCoinsOkClick()
    {
        if (noCoinsPopup != null)
            noCoinsPopup.SetActive(false);
    }

    // ─── Logout Flow ─────────────────────────────────────────────

    public void OnLogoutButtonClick()
    {
        if (logoutPopup == null) return;
        logoutPopup.SetActive(true);
        logoutPopup.transform.localScale = Vector3.zero;
        logoutPopup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    public void OnLogoutConfirm()
    {
        if (logoutPopup != null)
        {
            logoutPopup.SetActive(false);
        }

        try
        {
            var profile = PlayerProfileManager.GetInstance();
            if (profile != null)
            {
                profile.Logout();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HomeMenuController] Exception during profile logout: {ex}");
        }

        // Always redirect to Profile Setup Scene on logout
        SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
    }

    public void OnLogoutCancel()
    {
        if (logoutPopup != null)
            logoutPopup.SetActive(false);
    }

    // ─── IAP / Ads ───────────────────────────────────────────────

    private void HideNoAdsButton()
    {
        LoadingView.GetInstance()?.Hide();
        if (noAdsButton != null) noAdsButton.SetActive(false);
    }

    // ─── Bet Selector UI ──────────────────────────────────────────

    private void RefreshBetSelectionUI()
    {
        // Color theming is handled entirely in the Inspector / Prefab.
        // This method only tracks the selected bet for gameplay logic.
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null || betButtons == null) return;

        int selectedBet = profile.SelectedBet;
        int[] betAmounts = { 250, 500, 1000 };

        for (int i = 0; i < 3; i++)
        {
            if (i >= betButtons.Length) break;
            var btn = betButtons[i];
            if (btn == null) continue;

            int bet = betAmounts[i];
            bool canAfford = profile.Coins >= bet;

            // Only control interactability — no color overrides
            btn.interactable = canAfford;
        }
    }

    public void SelectBet(int amount)
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        if (profile.Coins < amount)
        {
            ShowNoCoinsPopup();
            return;
        }

        profile.SetSelectedBet(amount);
        RefreshBetSelectionUI();
        RefreshCoinsAndPlayButton();
    }

    private void DisableButtonTextRaycasts(GameObject parent)
    {
        if (parent == null) return;
        var buttons = parent.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            var texts = btn.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var txt in texts)
            {
                txt.raycastTarget = false;
            }
            var images = btn.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != btn.gameObject)
                {
                    img.raycastTarget = false;
                }
            }
        }
    }
}
