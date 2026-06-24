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

    private Button[]          _betButtons;
    private Image[]           _betImages;
    private TextMeshProUGUI[] _betTexts;

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

        // Wire up buttons first so they are guaranteed to work even if UI refresh throws
        if (playButton    != null) playButton.onClick.AddListener(OnPlayButtonClick);
        if (profileButton != null) profileButton.onClick.AddListener(OnProfileButtonClick);
        if (storeButton   != null) storeButton.onClick.AddListener(OnStoreButtonClick);

        if (settingsButton!= null) settingsButton.onClick.AddListener(OnSettingsButtonClick);
        if (logoutButton  != null) logoutButton.onClick.AddListener(OnLogoutButtonClick);
        if (logoutYesBtn  != null) logoutYesBtn.onClick.AddListener(OnLogoutConfirm);
        if (logoutNoBtn   != null) logoutNoBtn.onClick.AddListener(OnLogoutCancel);
        if (noCoinsOkBtn  != null) noCoinsOkBtn.onClick.AddListener(() => { if (noCoinsPopup != null) noCoinsPopup.SetActive(false); });

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

        // Verify and build Bet Selector UI
        var profile = PlayerProfileManager.GetInstance();
        if (profile != null)
        {
            profile.VerifySelectedBetAffordability();
        }

        try
        {
            CreateBetSelectorUI();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[HomeMenuController] Exception in CreateBetSelectorUI: {ex}");
        }

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

        // Grey out play button if can't afford
        if (playButton != null)
        {
            var colors = playButton.colors;
            colors.normalColor = canAfford
                ? new Color(0.15f, 0.75f, 0.3f)    // green — ready
                : new Color(0.5f, 0.5f, 0.5f);      // grey — insufficient funds
            playButton.colors = colors;
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
        logoutPopup?.SetActive(false);
    }

    // ─── IAP / Ads ───────────────────────────────────────────────

    private void HideNoAdsButton()
    {
        LoadingView.GetInstance()?.Hide();
        if (noAdsButton != null) noAdsButton.SetActive(false);
    }

    // ─── Bet Selector UI ──────────────────────────────────────────

    private void CreateBetSelectorUI()
    {
        Transform parentPanel = null;
        if (battleCostText != null)
        {
            parentPanel = battleCostText.transform.parent;
            battleCostText.gameObject.SetActive(false);
        }
        if (battleRewardText != null)
        {
            battleRewardText.gameObject.SetActive(false);
        }

        if (parentPanel == null) return;

        var containerGo = new GameObject("BetSelectorContainer", typeof(RectTransform));
        containerGo.transform.SetParent(parentPanel, false);
        var containerRect = containerGo.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = new Vector2(0f, 0f);
        containerRect.sizeDelta = new Vector2(900f, 160f);

        int[] betAmounts = { 250, 500, 1000 };
        float buttonWidth = 260f;
        float spacing = 40f;
        float startX = -((buttonWidth * 3f) + (spacing * 2f)) / 2f + (buttonWidth / 2f);

        _betButtons = new Button[3];
        _betImages = new Image[3];
        _betTexts = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            int bet = betAmounts[i];
            int reward = bet * 2;

            var btnGo = new GameObject($"BetBtn_{bet}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(containerGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(startX + (i * (buttonWidth + spacing)), 0f);
            btnRect.sizeDelta = new Vector2(buttonWidth, 140f);

            var img = btnGo.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            _betImages[i] = img;

            var btn = btnGo.GetComponent<Button>();
            _betButtons[i] = btn;
            btn.onClick.AddListener(() => SelectBet(bet));

            var betTxtGo = new GameObject("BetText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            betTxtGo.transform.SetParent(btnGo.transform, false);
            var betTxtRect = betTxtGo.GetComponent<RectTransform>();
            betTxtRect.anchorMin = new Vector2(0.5f, 0.5f);
            betTxtRect.anchorMax = new Vector2(0.5f, 0.5f);
            betTxtRect.pivot = new Vector2(0.5f, 0.5f);
            betTxtRect.anchoredPosition = new Vector2(0f, 25f);
            betTxtRect.sizeDelta = new Vector2(buttonWidth, 40f);

            var betTmp = betTxtGo.GetComponent<TextMeshProUGUI>();
            betTmp.fontSize = 20f;
            betTmp.fontStyle = FontStyles.Bold;
            betTmp.alignment = TextAlignmentOptions.Center;
            betTmp.text = $"BET 🪙{bet}";

            var rewTxtGo = new GameObject("RewardText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rewTxtGo.transform.SetParent(btnGo.transform, false);
            var rewTxtRect = rewTxtGo.GetComponent<RectTransform>();
            rewTxtRect.anchorMin = new Vector2(0.5f, 0.5f);
            rewTxtRect.anchorMax = new Vector2(0.5f, 0.5f);
            rewTxtRect.pivot = new Vector2(0.5f, 0.5f);
            rewTxtRect.anchoredPosition = new Vector2(0f, -25f);
            rewTxtRect.sizeDelta = new Vector2(buttonWidth, 40f);

            var rewTmp = rewTxtGo.GetComponent<TextMeshProUGUI>();
            rewTmp.fontSize = 17f;
            rewTmp.fontStyle = FontStyles.Normal;
            rewTmp.alignment = TextAlignmentOptions.Center;
            rewTmp.text = $"WIN 🪙{reward}";
            _betTexts[i] = rewTmp;
        }

        DisableButtonTextRaycasts(containerGo);
        RefreshBetSelectionUI();
    }

    private void RefreshBetSelectionUI()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null || _betButtons == null || _betImages == null || _betTexts == null) return;

        int selectedBet = profile.SelectedBet;
        int[] betAmounts = { 250, 500, 1000 };

        for (int i = 0; i < 3; i++)
        {
            if (i >= _betButtons.Length || i >= _betImages.Length || i >= _betTexts.Length) break;

            var btn = _betButtons[i];
            var img = _betImages[i];
            var rewardTmp = _betTexts[i];

            if (btn == null || img == null) continue;

            int bet = betAmounts[i];
            bool canAfford = profile.Coins >= bet;
            bool isSelected = selectedBet == bet;

            var betTextTrans = btn.transform.Find("BetText");
            var betTmp = betTextTrans != null ? betTextTrans.GetComponent<TextMeshProUGUI>() : null;

            if (!canAfford)
            {
                img.color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
                if (betTmp != null) betTmp.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
                if (rewardTmp != null) rewardTmp.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
            else if (isSelected)
            {
                img.color = i switch
                {
                    0 => new Color(0.15f, 0.5f, 0.85f, 1f),
                    1 => new Color(0.9f, 0.7f, 0.1f, 1f),
                    _ => new Color(0.85f, 0.25f, 0.15f, 1f)
                };
                if (betTmp != null) betTmp.color = Color.white;
                if (rewardTmp != null) rewardTmp.color = Color.white;
            }
            else
            {
                img.color = new Color(0.12f, 0.12f, 0.16f, 0.95f);
                if (betTmp != null) betTmp.color = new Color(0.7f, 0.7f, 0.8f, 0.8f);
                if (rewardTmp != null) rewardTmp.color = new Color(0.6f, 0.6f, 0.7f, 0.7f);
            }
        }
    }

    private void SelectBet(int amount)
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
