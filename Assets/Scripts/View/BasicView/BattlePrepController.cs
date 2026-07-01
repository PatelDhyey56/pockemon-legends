using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;
using AdsManager;
using Utils;

public class BattlePrepController : MonoBehaviour
{
    // ─── Header (Same fields as PlayerProfileController for metadata compatibility) ───────────────────
    [Header("Profile Header")]
    [SerializeField] private Image           avatarBg;
    [SerializeField] private TextMeshProUGUI avatarInitialText;
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image           xpBar;
    [SerializeField] private TextMeshProUGUI xpProgressText;
    [SerializeField] private TextMeshProUGUI xpToNextText;

    // ─── Stats Row ───────────────────────────────────────────────────
    [Header("Stats Row")]
    [SerializeField] private TextMeshProUGUI coinsValueText;
    [SerializeField] private TextMeshProUGUI winsValueText;
    [SerializeField] private TextMeshProUGUI lossesValueText;
    [SerializeField] private TextMeshProUGUI winRateText;
    [SerializeField] private TextMeshProUGUI battlesPlayedText;

    // ─── Creature Collection ──────────────────────────────────────────
    [Header("Creature Collection")]
    [UnityEngine.Serialization.FormerlySerializedAs("creatureGridParent")]
    [SerializeField] private Transform      creatureGridParent;
    [UnityEngine.Serialization.FormerlySerializedAs("creatureCardPrefab")]
    [SerializeField] private GameObject     creatureCardPrefab;
    [SerializeField] private TextMeshProUGUI collectionCountText;

    [Header("Dialog Box Appearance")]
    [Tooltip("Sprite used as the creature details popup background.")]
    [SerializeField] private Sprite          dialogBoxBackground;

    [Header("Card Appearance")]
    [Tooltip("Sprite used as each creature card background.")]
    [SerializeField] private Sprite          cardBackground;
    [Tooltip("Sprite used as the battle-team badge background on cards.")]
    [SerializeField] private Sprite          teamBadgeBackground;

    // ─── Navigation ─────────────────────────────────────────────────
    [Header("Navigation")]
    [SerializeField] private Button backButton;

    // ─── Static UI References (No longer created programmatically) ────
    [Header("Static UI References")]
    [SerializeField] private Button          startBattleButton;
    [SerializeField] private GameObject      noCoinsPopup;
    [SerializeField] private RectTransform   noCoinsDialogBox;
    [SerializeField] private TextMeshProUGUI noCoinsText;
    [SerializeField] private Button          noCoinsOkBtn;

    // ─── Battle Info Panel — runtime only (no Inspector assignment needed) ────
    private GameObject      _battleInfoPanel;
    private TextMeshProUGUI _insufficientFundsText;
    private Button[]        _betButtons  = new Button[3];
    private Image[]         _betBtnImgs  = new Image[3];

    private TextMeshProUGUI startBattleButtonText;
    private Image           startBattleButtonImg;

    private GameObject _modalPopupInstance;
    private bool       _betConfirmedThisVisit;

    // Gem-type colours (shared palette)
    private static readonly Dictionary<GemType, Color> TypeColors = new Dictionary<GemType, Color>
    {
        { GemType.Fire,     new Color(0.95f, 0.25f, 0.05f) },
        { GemType.Water,    new Color(0.05f, 0.75f, 0.95f) },
        { GemType.Nature,   new Color(0.35f, 0.90f, 0.15f) },
        { GemType.Electric, new Color(1.00f, 0.78f, 0.00f) },
        { GemType.Psychic,  new Color(0.70f, 0.15f, 0.95f) },
        { GemType.Healing,  new Color(0.85f, 0.35f, 0.55f) },
    };

    private static readonly Color PopupBtnGreen  = new Color(0.08f, 0.68f, 0.30f, 1f);
    private static readonly Color PopupBtnRed    = new Color(0.85f, 0.15f, 0.15f, 1f);
    private static readonly Color PopupTitleColor = new Color(0.745283f, 0.56290144f, 0.28475437f, 1f);
    private static readonly Color PopupBodyColor  = new Color(1f, 0.7273872f, 0.5518868f, 1f);

    private static readonly Vector2 InfoButtonSize = new Vector2(250f, 175f);

    private static Sprite GetLoginLogoutSprite(string spriteName)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>("buttons/login-logout");
        return Array.Find(sprites, s => s.name == spriteName);
    }

    private static void ApplyLoginLogoutButtonImage(Image img, Sprite sprite)
    {
        if (img == null) return;
        if (sprite != null)
        {
            img.sprite         = sprite;
            img.type           = Image.Type.Simple;
            img.preserveAspect = true;
        }
        img.color = Color.white;
    }

    private static Sprite GetInfoOkButtonSprite()
    {
        return GetLoginLogoutSprite("login-logout_4");
    }

    private readonly List<GameObject> _cards = new List<GameObject>();

    // ── Runtime details popup ────────────────────────────────────────
    private GameObject      _detailsPopup;
    private Image           _popupAvatar;
    private TextMeshProUGUI _popupNameText;
    private TextMeshProUGUI _popupStoneTypeText;
    private TextMeshProUGUI _popupStoneCapText;
    private TextMeshProUGUI _popupBasePowerText;
    private TextMeshProUGUI _popupEvoledPowerText;
    private TextMeshProUGUI _popupSkillText;
    private TextMeshProUGUI _popupEffectText;
    private Button          _popupBattleBtn;
    private TextMeshProUGUI _popupBattleBtnText;
    private Image           _popupBattleBtnImg;

    private void Start()
    {
        Time.timeScale = 1f; // Ensure timeScale is active on entering battle prep scene
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null || !profile.IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
            return;
        }

        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClick);

        // Hide win/loss stats completely
        if (winsValueText     != null) winsValueText.gameObject.SetActive(false);
        if (lossesValueText   != null) lossesValueText.gameObject.SetActive(false);
        if (winRateText       != null) winRateText.gameObject.SetActive(false);
        if (battlesPlayedText != null) battlesPlayedText.gameObject.SetActive(false);

        SetupStartBattleButton();

        _betConfirmedThisVisit = false;

        if (noCoinsOkBtn != null) noCoinsOkBtn.onClick.AddListener(HideNoCoinsPopup);
        if (noCoinsPopup != null)
        {
            SetupNoCoinsPopupStyling();
            noCoinsPopup.SetActive(false);
        }

        // Setup static BattleInfoPanel
        SetupStaticBattleInfoPanel();
        SetupStaticHeaderPanel();

        // Verify affordability
        var p2 = PlayerProfileManager.GetInstance();
        if (p2 != null) p2.VerifySelectedBetAffordability();

        // Subscribe to live updates
        PlayerProfileManager.OnProfileChanged += RefreshAll;
        PlayerProfileManager.OnCoinsChanged   += RefreshCoins;

        RefreshAll();
        
        // Boost scroll sensitivity on the grid
        if (creatureGridParent != null)
        {
            var scrollRect = creatureGridParent.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = 45f;
                scrollRect.inertia           = true;
                scrollRect.decelerationRate  = 0.99f;
                scrollRect.vertical          = true;
                scrollRect.horizontal        = false;

                // Adjust ScrollView top offset to 250 (offsetMax.y = -250f) and bottom offset to 550 (offsetMin.y = 550f)
                var rt = scrollRect.GetComponent<RectTransform>();
                if (rt != null)
                {
                    Vector2 offsetMax = rt.offsetMax;
                    offsetMax.y = -250f;
                    rt.offsetMax = offsetMax;

                    Vector2 offsetMin = rt.offsetMin;
                    offsetMin.y = 550f;
                    rt.offsetMin = offsetMin;
                }
            }
        }
    }

    private void SetupStartBattleButton()
    {
        if (startBattleButton == null) return;

        startBattleButtonImg = startBattleButton.GetComponent<Image>();
        startBattleButtonText = startBattleButton.GetComponentInChildren<TextMeshProUGUI>();
        startBattleButton.onClick.RemoveAllListeners();
        startBattleButton.onClick.AddListener(OnStartBattleClick);
    }

    // ─── Setup Static Battle Info Panel ───────────────────────────────

    private void SetupStaticBattleInfoPanel()
    {
        _battleInfoPanel = GameObject.Find("BattleInfoPanel");
        if (_battleInfoPanel != null)
        {
            var titleTrans = _battleInfoPanel.transform.Find("BetSelectorContainer/Title");
            if (titleTrans != null)
            {
                _insufficientFundsText = titleTrans.GetComponent<TextMeshProUGUI>();
            }

            int[] betAmounts = { 250, 500, 1000 };
            for (int i = 0; i < 3; i++)
            {
                var btnTrans = _battleInfoPanel.transform.Find($"BetSelectorContainer/BetBtn_{betAmounts[i]}");
                if (btnTrans != null)
                {
                    var btn = btnTrans.GetComponent<Button>();
                    _betButtons[i] = btn;
                    _betBtnImgs[i] = btnTrans.GetComponent<Image>();

                    int amount = betAmounts[i];
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => SelectBet(amount));
                    }
                }
            }
        }
    }

    private void SetupStaticHeaderPanel()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            var allTexts = canvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTexts)
            {
                string n = t.name.ToLower();
                if (usernameText == null && (n.Contains("username") || n.Contains("name"))) usernameText = t;
                if (levelText == null && n.Contains("level")) levelText = t;
                if (avatarInitialText == null && n.Contains("initial")) avatarInitialText = t;
                if (xpProgressText == null && (n.Contains("xpprogress") || n.Contains("progress"))) xpProgressText = t;
                if (xpToNextText == null && (n.Contains("xptonext") || n.Contains("tonext") || n.Contains("next"))) xpToNextText = t;
                if (coinsValueText == null && (n.Equals("coinsvalue") || n.Contains("coin"))) coinsValueText = t;
            }

            var allImages = canvas.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                if (avatarBg == null && (img.name.Equals("AvatarBg") || img.name.Equals("AvatarBackground"))) avatarBg = img;
                if (xpBar == null && (img.name.Equals("XPBarFill") || img.name.Equals("Fill") || img.name.Equals("FillArea")))
                {
                    if (img.transform.parent != null && img.transform.parent.name.Contains("XP"))
                        xpBar = img;
                }
            }
        }
    }

    private Transform FindDeepChild(Transform aParent, string aName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(aParent);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.name == aName)
                return c;
            foreach(Transform t in c)
                queue.Enqueue(t);
        }
        return null;
    }



    private void OnDestroy()
    {
        PlayerProfileManager.OnProfileChanged -= RefreshAll;
        PlayerProfileManager.OnCoinsChanged   -= RefreshCoins;
    }

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshCoins();
        BuildCreatureGrid();
        RefreshBattleButtonState();
    }

    private void RefreshHeader()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (usernameText != null) usernameText.text = p.Username;
        if (levelText    != null) levelText.text    = $"Lv. {p.Level}";

        // Display user's initial icon
        if (avatarInitialText != null)
            avatarInitialText.text = p.Username.Length > 0 ? p.Username[0].ToString().ToUpper() : "?";

        if (avatarBg != null)
        {
            avatarBg.DOColor(Color.white, 0.4f).SetUpdate(true);
        }

        // Display XP bar & details dynamically
        float progress = p.GetLevelProgress();
        if (xpBar != null)
        {
            xpBar.gameObject.SetActive(true);
            xpBar.DOFillAmount(progress, 0.6f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        if (xpProgressText != null)
        {
            xpProgressText.gameObject.SetActive(true);
            if (p.Level >= PlayerProfileManager.MAX_LEVEL && p.GetXPToNextLevel() <= 0)
                xpProgressText.text = "GAME COMPLETED";
            else
                xpProgressText.text = $"{p.XP} / {p.XP + p.GetXPToNextLevel()} XP";
        }

        if (xpToNextText != null)
        {
            xpToNextText.gameObject.SetActive(true);
            if (p.Level >= PlayerProfileManager.MAX_LEVEL && p.GetXPToNextLevel() <= 0)
                xpToNextText.text = "Congratulations!";
            else if (p.Level >= PlayerProfileManager.MAX_LEVEL)
                xpToNextText.text = $"{p.GetXPToNextLevel()} XP to complete the game!";
            else
                xpToNextText.text = $"{p.GetXPToNextLevel()} XP to next level";
        }
    }

    private void RefreshCoins()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p != null && coinsValueText != null)
        {
            coinsValueText.text = $"<color=#FFD700>{p.Coins}</color>";
            PlayerProfileManager.AttachCoinSprite(coinsValueText);

            // Pulse coins text when updated
            coinsValueText.transform.DOPunchScale(Vector3.one * 0.15f, 0.3f, 5, 0.5f).SetUpdate(true);
        }
        RefreshBattleButtonState();
        RefreshBetSelectionUI();
    }

    // ─── Bet Selector ─────────────────────────────────────────────────

    private void RefreshBetSelectionUI()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        int selected = profile.SelectedBet;
        int[] betAmounts = { 250, 500, 1000 };

        for (int i = 0; i < 3; i++)
        {
            if (_betButtons[i] != null)
            {
                var shadow = _betButtons[i].gameObject.GetComponent<UnityEngine.UI.Shadow>();
                bool isSelected = (betAmounts[i] == selected && _betConfirmedThisVisit);

                if (isSelected)
                {
                    if (shadow == null)
                    {
                        shadow = _betButtons[i].gameObject.AddComponent<UnityEngine.UI.Shadow>();
                        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f); // Dark shadow
                        shadow.effectDistance = new Vector2(8f, -8f);
                    }
                    shadow.enabled = true;
                    // Much larger scale with a bouncy pop animation
                    _betButtons[i].transform.DOScale(new Vector3(1.18f, 1.18f, 1f), 0.25f).SetEase(Ease.OutBack);
                }
                else
                {
                    if (shadow != null) shadow.enabled = false;
                    // Slightly shrink unselected buttons to maximize contrast
                    _betButtons[i].transform.DOScale(new Vector3(0.92f, 0.92f, 1f), 0.2f).SetEase(Ease.OutCubic);
                }
            }
        }
    }


    public void SelectBet(int amount)
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        if (profile.Coins < amount)
        {
            ShowNoCoinsPopup(amount);
            return;
        }

        // Direct selection if they have enough coins
        ConfirmBetSelection(amount);
    }

    private void ConfirmBetSelection(int amount)
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        profile.SetSelectedBet(amount);
        _betConfirmedThisVisit = true;
        RefreshBetSelectionUI();
        RefreshBattleButtonState();
    }

    private void BuildCreatureGrid()
    {
        if (creatureGridParent == null)
        {
            Debug.LogError("[BattlePrepController] BuildCreatureGrid aborted: creatureGridParent is null!");
            return;
        }
        if (creatureCardPrefab == null)
        {
            Debug.LogError("[BattlePrepController] BuildCreatureGrid aborted: creatureCardPrefab is null!");
            return;
        }

        foreach (var c in _cards) Destroy(c);
        _cards.Clear();

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null)
        {
            Debug.LogError("[BattlePrepController] BuildCreatureGrid aborted: PlayerProfileManager instance is null!");
            return;
        }

        int total = PlayerProfileManager.AllCreatures.Count;
        int owned = profile.OwnedCreatures.Count;
        if (collectionCountText != null)
            collectionCountText.text = $"{owned} / {total} collected";

        int index = 0;
        foreach (var entry in PlayerProfileManager.AllCreatures)
        {
            bool isOwned = profile.OwnsCreatures(entry.Name);
            if (!isOwned) continue;

            GameObject card = Instantiate(creatureCardPrefab, creatureGridParent);
            _cards.Add(card);

            Button cardBtn = card.GetComponent<Button>();
            if (cardBtn == null) cardBtn = card.AddComponent<Button>();
            string captureName = entry.Name;
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() => OnCardClicked(captureName));

            // Setup card display
            Image avatarImg = card.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
            {
                avatarImg.sprite = AvatarGenerator.CreateCreatureSprite(entry.Name);
                avatarImg.preserveAspect = true;
                avatarImg.color  = isOwned ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
            }

            TextMeshProUGUI nameText = card.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text  = entry.Name;
                nameText.color = new Color(1f, 0.97f, 0.88f, 1f);
                nameText.enableWordWrapping = false;
            }

            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = GetCategoryName(entry.Type);
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                    typeText.color = tc;
                typeText.enableWordWrapping = false;
            }

            TextMeshProUGUI statsText = card.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
            {
                int dmg    = BoardManager.GetBaseValueForCreature(entry.Name);
                int energy = BoardManager.GetMaxEnergyForCreature(entry.Name);
                statsText.text = $"ATK {dmg}  STN {energy}";
                statsText.enableWordWrapping = false;
                statsText.color = new Color(0.8f, 0.8f, 0.8f);
            }

            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
                priceText.gameObject.SetActive(false);

            var buyBtnTrans = card.transform.Find("BuyButton");
            if (buyBtnTrans != null)
                buyBtnTrans.gameObject.SetActive(false);

            TextMeshProUGUI slotLabel = card.transform.Find("SlotLabel")?.GetComponent<TextMeshProUGUI>();
            if (slotLabel != null)
                slotLabel.gameObject.SetActive(false);

            Image cardBg = card.transform.Find("CardBg")?.GetComponent<Image>();
            if (cardBg != null)
            {
                if (cardBackground != null)
                {
                    cardBg.sprite         = cardBackground;
                    cardBg.type           = Image.Type.Simple;
                    cardBg.preserveAspect = false;
                }
                cardBg.color = new Color(65 / 255f, 60 / 255f, 40 / 255f, 1f);
            }

            GameObject teamBadge = card.transform.Find("TeamBadge")?.gameObject;
            if (teamBadge != null)
            {
                Image badgeBg = teamBadge.GetComponent<Image>();
                if (badgeBg != null)
                {
                    if (teamBadgeBackground != null)
                    {
                        badgeBg.sprite         = teamBadgeBackground;
                        badgeBg.type           = Image.Type.Simple;
                        badgeBg.preserveAspect = false;
                    }
                    badgeBg.color = Color.white;
                }

                var teamTextTMP = teamBadge.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
                if (teamTextTMP != null)
                {
                    teamTextTMP.text  = "BATTLE";
                    teamTextTMP.color = Color.black;
                }

                bool inTeam = isOwned && profile.BattleTeam.Contains(entry.Name);
                teamBadge.SetActive(inTeam);
            }

            Image glowImage = card.transform.Find("OwnedGlow")?.GetComponent<Image>();
            if (glowImage != null)
                glowImage.gameObject.SetActive(false);

            GameObject lockOverlay = card.transform.Find("LockOverlay")?.gameObject;
            if (lockOverlay != null)
                lockOverlay.SetActive(!isOwned);

            card.transform.localScale = Vector3.zero;
            card.transform.DOScale(1f, 0.25f)
                .SetDelay(index * 0.04f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            
            index++;
        }

        if (creatureGridParent != null)
        {
            Canvas.ForceUpdateCanvases();
            var fitter = creatureGridParent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(creatureGridParent.GetComponent<RectTransform>());

            DisableButtonTextRaycasts(creatureGridParent.gameObject);
        }
    }

    private void OnCardClicked(string creatureName)
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        var entry = PlayerProfileManager.AllCreatures.Find(p => p.Name == creatureName);
        if (entry == null) return;

        bool isOwned = profile.OwnsCreatures(creatureName);
        if (!isOwned)
        {
            MessageView.GetInstance()?.ShowMessageView(
                $"{creatureName} is locked! Visit the store to purchase it for {entry.Price} coins.",
                "Go to Store",
                () => { SceneManager.LoadScene(Constants.SCENE_STORE); }
            );
            return;
        }

        ShowCreatureDetails(creatureName);
    }

    private void CreateDetailsPopup()
    {
        if (_detailsPopup != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        _detailsPopup = new GameObject("PrepDetailsPopup", typeof(RectTransform));
        _detailsPopup.transform.SetParent(canvas.transform, false);
        _detailsPopup.transform.SetAsLastSibling();

        var overlayRect = _detailsPopup.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dimImg = _detailsPopup.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);

        var bgBtn = _detailsPopup.AddComponent<Button>();
        bgBtn.onClick.AddListener(CloseDetailsPopup);

        var box = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_detailsPopup.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta        = new Vector2(850f, 1150f);
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        if (dialogBoxBackground != null)
        {
            boxImg.sprite         = dialogBoxBackground;
            boxImg.type           = Image.Type.Simple;
            boxImg.preserveAspect = false;
            boxImg.color          = Color.white;
        }
        else
        {
            boxImg.color = new Color(0.09f, 0.09f, 0.13f, 0.97f);
        }
        box.AddComponent<Button>().onClick.AddListener(() => { });

        var xGo = new GameObject("CloseXBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        xGo.transform.SetParent(box.transform, false);
        var xRect = xGo.GetComponent<RectTransform>();
        xRect.anchorMin        = new Vector2(1f, 1f);
        xRect.anchorMax        = new Vector2(1f, 1f);
        xRect.pivot            = new Vector2(1f, 1f);
        xRect.anchoredPosition = new Vector2(-45f, -75f);
        xRect.sizeDelta        = new Vector2(75f, 75f);

        var xCircle = xGo.GetComponent<Image>();
        Sprite[] allIcons = Resources.LoadAll<Sprite>("buttons/icons");
        Sprite icons9 = Array.Find(allIcons, s => s.name == "icons_9");
        if (icons9 != null)
        {
            xCircle.sprite         = icons9;
            xCircle.type           = Image.Type.Simple;
            xCircle.preserveAspect = true;
            xCircle.color          = Color.white;
        }
        else
        {
            xCircle.color = new Color(0.80f, 0.18f, 0.18f, 0.92f);
        }

        xGo.GetComponent<Button>().onClick.AddListener(CloseDetailsPopup);

        var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(box.transform, false);
        var avatarRect = avatarGo.GetComponent<RectTransform>();
        avatarRect.anchorMin        = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax        = new Vector2(0.5f, 0.5f);
        avatarRect.pivot            = new Vector2(0.5f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(0f, 275f);
        avatarRect.sizeDelta        = new Vector2(400f, 400f);
        _popupAvatar = avatarGo.GetComponent<Image>();
        if (_popupAvatar != null) _popupAvatar.preserveAspect = true;

        var nameGo = new GameObject("CreatureName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(box.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, 30f);
        nameRect.sizeDelta        = new Vector2(750f, 70f);
        _popupNameText = nameGo.GetComponent<TextMeshProUGUI>();
        _popupNameText.fontSize  = 50f;
        _popupNameText.fontStyle = FontStyles.Bold;
        _popupNameText.alignment = TextAlignmentOptions.Center;
        _popupNameText.color     = Color.black;

        MakePrepSeparator(box.transform, -25f);

        float rowY    = -65f;
        float rowStep = 45f;

        _popupStoneTypeText   = MakePrepStatRow(box.transform, "StoneTypeRow",  rowY); rowY -= rowStep;
        _popupStoneCapText    = MakePrepStatRow(box.transform, "StoneCapRow",   rowY); rowY -= rowStep;
        _popupBasePowerText   = MakePrepStatRow(box.transform, "BasePowRow",    rowY); rowY -= rowStep;
        _popupEvoledPowerText = MakePrepStatRow(box.transform, "EvoPowRow",     rowY); rowY -= rowStep;
        _popupSkillText       = MakePrepStatRow(box.transform, "SkillRow",      rowY); rowY -= rowStep;
        _popupEffectText      = MakePrepStatRow(box.transform, "EffectRow",     -300f);

        MakePrepSeparator(box.transform, -350f);

        var battleGo = new GameObject("BattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        battleGo.transform.SetParent(box.transform, false);
        var battleRect = battleGo.GetComponent<RectTransform>();
        battleRect.anchorMin        = new Vector2(0.5f, 0.5f);
        battleRect.anchorMax        = new Vector2(0.5f, 0.5f);
        battleRect.pivot            = new Vector2(0.5f, 0.5f);
        battleRect.anchoredPosition = new Vector2(0f, -415f);
        battleRect.sizeDelta        = new Vector2(500f, 75f);
        _popupBattleBtnImg = battleGo.GetComponent<Image>();
        _popupBattleBtn = battleGo.GetComponent<Button>();
        ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnGreen);

        var battleTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        battleTextGo.transform.SetParent(battleGo.transform, false);
        var btRect = battleTextGo.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero; btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero; btRect.offsetMax = Vector2.zero;
        _popupBattleBtnText = battleTextGo.GetComponent<TextMeshProUGUI>();
        _popupBattleBtnText.fontSize = 30f;
        _popupBattleBtnText.fontStyle = FontStyles.Bold;
        _popupBattleBtnText.alignment = TextAlignmentOptions.Center;
        _popupBattleBtnText.color = Color.white;
        _popupBattleBtnText.raycastTarget = false;

        _detailsPopup.SetActive(false);
    }

    private void CloseDetailsPopup()
    {
        if (_detailsPopup != null)
        {
            _detailsPopup.transform.DOScale(0f, 0.18f).SetEase(Ease.InBack).SetUpdate(true)
                .OnComplete(() => { if (_detailsPopup != null) _detailsPopup.SetActive(false); });
        }
    }

    private static void MakePrepSeparator(Transform parent, float anchoredY)
    {
        var sep = new GameObject("Sep", typeof(RectTransform), typeof(Image));
        sep.transform.SetParent(parent, false);
        var r = sep.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0.5f);
        r.anchorMax        = new Vector2(0.5f, 0.5f);
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, anchoredY);
        r.sizeDelta        = new Vector2(750f, 2f);
        sep.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.4f);
    }

    private static TextMeshProUGUI MakePrepStatRow(Transform parent, string name, float anchoredY)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0.5f);
        r.anchorMax        = new Vector2(0.5f, 0.5f);
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, anchoredY);
        r.sizeDelta        = new Vector2(750f, 55f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize         = 34f;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.color            = Color.black;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    public void ShowCreatureDetails(string name)
    {
        CreateDetailsPopup();
        if (_detailsPopup == null) return;

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        var entry = PlayerProfileManager.AllCreatures.Find(p => p.Name == name);
        if (entry == null) return;

        // ── Avatar & name ──
        if (_popupAvatar   != null) _popupAvatar.sprite   = AvatarGenerator.CreateCreatureSprite(name);
        if (_popupNameText != null) _popupNameText.text   = name.ToUpper();

        // ── Stats ──
        int basePower    = BoardManager.GetBaseValueForCreature(name);
        int evolvedPower = basePower + 5;
        int stoneLimit   = BoardManager.GetMaxEnergyForCreature(name);
        bool isHeal      = entry.Type == GemType.Nature || entry.Type == GemType.Healing;
        string powerLabel = isHeal ? "Heal & Dmg" : "Damage";

        string typeColor = entry.Type switch
        {
            GemType.Fire     => "#FF5522",
            GemType.Water    => "#22AAFF",
            GemType.Nature   => "#33DD33",
            GemType.Electric => "#FFCC00",
            GemType.Psychic  => "#CC44FF",
            GemType.Healing  => "#FF88BB",
            _                => "#FFFFFF"
        };

        var attackConfig = CreatureAttackConfig.Load();
        var rule = attackConfig != null ? attackConfig.GetRule(entry.Type) : null;
        string abilityName = rule != null ? rule.AttackName : "Tackle";
        string abilityDesc = rule != null ? rule.EffectDescription : "Deals damage";
        int abilityDamage = rule != null ? rule.Damage : 10;
        int stonesReq = rule != null ? rule.StonesRequired : 5;

        string abilityPowerLabel = isHeal ? "Ability Heal:" : "Ability Damage:";

        if (_popupStoneTypeText != null)
            _popupStoneTypeText.text =
                $"<color=#333333>Elemental Class:</color> <b><color={typeColor}>{GetCategoryName(entry.Type)}</color></b>";

        if (_popupStoneCapText != null)
            _popupStoneCapText.text =
                $"<color=#333333>Power:</color> <b><color=#AA7700>{basePower}</color></b>";

        if (_popupBasePowerText != null)
            _popupBasePowerText.text =
                $"<color=#333333>{abilityPowerLabel}</color> <b><color=#006699>{abilityDamage}</color></b>";

        if (_popupEvoledPowerText != null)
            _popupEvoledPowerText.text =
                $"<color=#333333>Gems Required:</color> <b><color=#226622>{stonesReq}</color></b>";

        if (_popupSkillText != null)
            _popupSkillText.text =
                $"<color=#333333>Ability:</color> <b><color=#AA5500>{abilityName}</color></b>";

        if (_popupEffectText != null)
            _popupEffectText.text =
                $"<color=#333333>Effect:</color> <b><color=#444466>{abilityDesc}</color></b>";

        // ── Battle Button Config ──
        if (_popupBattleBtn != null && _popupBattleBtnText != null && _popupBattleBtnImg != null)
        {
            bool inTeam = profile.BattleTeam.Contains(name);

            if (inTeam)
            {
                _popupBattleBtnText.text = "DECOMMISSION UNIT";
                ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnRed);
            }
            else
            {
                _popupBattleBtnText.text = "DEPLOY UNIT TO SQUAD";
                ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnGreen);
            }

            _popupBattleBtn.onClick.RemoveAllListeners();
            _popupBattleBtn.onClick.AddListener(() =>
            {
                bool success = profile.ToggleBattleTeam(name);
                if (success)
                {
                    bool nowInTeam = profile.BattleTeam.Contains(name);
                    if (nowInTeam)
                    {
                        _popupBattleBtnText.text = "DECOMMISSION UNIT";
                        ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnRed);
                    }
                    else
                    {
                        _popupBattleBtnText.text = "DEPLOY UNIT TO SQUAD";
                        ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnGreen);
                    }
                    BuildCreatureGrid();
                    RefreshBattleButtonState();
                }
                else
                {
                    MessageView.GetInstance()?.ShowMessageView("Battle team is full! Deselect another Creature first.", "Ok");
                }
            });
        }

        // ── Show with animation ──
        _detailsPopup.SetActive(true);
        _detailsPopup.transform.SetAsLastSibling();
        _detailsPopup.transform.localScale = Vector3.zero;
        _detailsPopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
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
        }
    }

    private void RefreshBattleButtonState()
    {
        // Removed dynamic button state change per user request
    }

    private void OnStartBattleClick()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        if (profile.BattleTeam.Count != 2)
        {
            ShowSelectCreaturesPopup();
            return;
        }

        if (!_betConfirmedThisVisit)
        {
            ShowSelectBetPopup();
            return;
        }

        if (!profile.CanAffordBattle)
        {
            ShowNoCoinsPopup(profile.SelectedBet);
            return;
        }

        StartBattle();
    }

    private void StartBattle()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        profile.SetActiveBet(profile.SelectedBet);
        profile.SpendCoinsForBattle(profile.SelectedBet);

        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();

        SceneManager.LoadScene(Constants.SCENE_CREATURE);
    }

    private static void ApplyPopupButtonTheme(Button btn, Image img, Color color)
    {
        if (img != null) img.color = color;
        if (btn == null) return;

        ColorBlock cb = btn.colors;
        cb.normalColor      = color;
        cb.highlightedColor = color;
        cb.pressedColor     = color * 0.9f;
        cb.selectedColor    = color;
        cb.disabledColor    = color;
        btn.colors = cb;
    }

    private void ShowBetConfirmPopup(int amount)
    {
        ShowStyledModal(
            "CONFIRM BET",
            $"Use {amount} coins for this battle?",
            onOk: () => ConfirmBetSelection(amount));
    }

    private void ShowSelectCreaturesPopup()
    {
        ShowStyledModal(
            "CHOSE CREATURES",
            "Choose exactly 2 creatures from the collection area to form your battle squad.",
            onOk: null);
    }

    private void ShowSelectBetPopup()
    {
        ShowStyledModal(
            "CHOSE BET",
            "Please chose a bet amount from the bet selection area before starting battle.",
            onOk: null);
    }

    private void ShowStyledModal(string title, string body, Action onOk)
    {
        CloseModalPopup();

        Canvas rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        if (_battleInfoPanel != null)
            _battleInfoPanel.SetActive(false);

        _modalPopupInstance = new GameObject("PrepModalPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _modalPopupInstance.transform.SetParent(rootCanvas.transform, false);
        _modalPopupInstance.transform.SetAsLastSibling();

        RectTransform overlayRt = _modalPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        var overlayImg = _modalPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        var overlayBtn = _modalPopupInstance.AddComponent<Button>();
        overlayBtn.onClick.AddListener(CloseModalPopup);

        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_modalPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot     = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(780f, 700f);

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type   = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        TextMeshProUGUI titleTxt = CreateModalText(modalWindow.transform, "TitleText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -150f), new Vector2(700f, 80f), title, 50f, FontStyles.Bold, PopupTitleColor);

        TextMeshProUGUI descTxt = CreateModalText(modalWindow.transform, "DescriptionText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(680f, 200f), body, 36f, FontStyles.Normal, PopupBodyColor);
        descTxt.enableWordWrapping = true;
        descTxt.lineSpacing        = 0f;
        descTxt.paragraphSpacing   = 0f;

        Sprite okBtnSprite = GetInfoOkButtonSprite();

        CreateModalButton(modalWindow.transform, "OkButton", okBtnSprite, new Vector2(0f, 90f), () =>
        {
            CloseModalPopup();
            onOk?.Invoke();
        });

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private static TextMeshProUGUI CreateModalText(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        string text, float fontSize, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color;
        return tmp;
    }

    private static void CreateModalButton(Transform parent, string name, Sprite sprite, Vector2 pos, Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.sizeDelta        = InfoButtonSize;
        rt.anchoredPosition = pos;

        ApplyLoginLogoutButtonImage(go.GetComponent<Image>(), sprite);

        go.GetComponent<Button>().onClick.AddListener(() => onClick());
    }

    private void CloseModalPopup()
    {
        if (_modalPopupInstance != null)
        {
            Destroy(_modalPopupInstance);
            _modalPopupInstance = null;
        }

        if (noCoinsPopup == null || !noCoinsPopup.activeSelf)
        {
            if (_battleInfoPanel != null)
                _battleInfoPanel.SetActive(true);
        }
    }

    private void SetupNoCoinsPopupStyling()
    {
        if (noCoinsPopup == null) return;

        var overlayImg = noCoinsPopup.GetComponent<Image>();
        if (overlayImg != null)
            overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        Sprite okBtnSprite = GetInfoOkButtonSprite();

        RectTransform dialog = noCoinsDialogBox != null
            ? noCoinsDialogBox
            : noCoinsPopup.transform.Find("DialogBox") as RectTransform;

        if (dialog == null) return;

        dialog.sizeDelta = new Vector2(780f, 700f);

        var boxImg = dialog.GetComponent<Image>();
        if (boxImg != null && bgSprite != null)
        {
            boxImg.sprite = bgSprite;
            boxImg.type   = Image.Type.Simple;
            boxImg.color  = Color.white;
        }

        var titleRt = dialog.Find("Title") as RectTransform;
        if (titleRt != null)
        {
            titleRt.anchorMin        = new Vector2(0.5f, 1f);
            titleRt.anchorMax        = new Vector2(0.5f, 1f);
            titleRt.pivot            = new Vector2(0.5f, 1f);
            titleRt.sizeDelta        = new Vector2(700f, 80f);
            titleRt.anchoredPosition = new Vector2(0f, -150f);
        }

        var titleText = dialog.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            titleText.text      = "INSUFFICIENT COINS";
            titleText.fontSize  = 50f;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color     = PopupTitleColor;
        }

        if (noCoinsText != null)
        {
            var bodyRt = noCoinsText.rectTransform;
            bodyRt.anchorMin        = new Vector2(0.5f, 0.5f);
            bodyRt.anchorMax        = new Vector2(0.5f, 0.5f);
            bodyRt.pivot            = new Vector2(0.5f, 0.5f);
            bodyRt.sizeDelta        = new Vector2(680f, 200f);
            bodyRt.anchoredPosition = new Vector2(0f, 0f);

            noCoinsText.fontSize           = 36f;
            noCoinsText.enableWordWrapping = true;
            noCoinsText.lineSpacing        = 0f;
            noCoinsText.paragraphSpacing   = 0f;
            noCoinsText.alignment          = TextAlignmentOptions.Center;
            noCoinsText.color              = PopupBodyColor;
        }

        if (noCoinsOkBtn != null)
        {
            var okRt = noCoinsOkBtn.GetComponent<RectTransform>();
            okRt.anchorMin        = new Vector2(0.5f, 0f);
            okRt.anchorMax        = new Vector2(0.5f, 0f);
            okRt.pivot            = new Vector2(0.5f, 0f);
            okRt.sizeDelta        = InfoButtonSize;
            okRt.anchoredPosition = new Vector2(0f, 90f);

            ApplyLoginLogoutButtonImage(noCoinsOkBtn.GetComponent<Image>(), okBtnSprite);

            var label = okRt.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        var cancelBtn = dialog.Find("CancelBtn");
        if (cancelBtn != null)
            cancelBtn.gameObject.SetActive(false);
    }

    private void ShowNoCoinsPopup(int neededBet)
    {
        var p = PlayerProfileManager.GetInstance();
        if (noCoinsPopup == null) return;

        CloseModalPopup();

        if (noCoinsText != null)
        {
            noCoinsText.text = $"You need {neededBet} coins to enter battle!\nYou have: {p?.Coins ?? 0}\nVisit the Store to get more.";
        }

        if (_battleInfoPanel != null)
            _battleInfoPanel.SetActive(false);

        noCoinsPopup.SetActive(true);
        noCoinsPopup.transform.SetAsLastSibling();

        RectTransform dialog = noCoinsDialogBox != null
            ? noCoinsDialogBox
            : noCoinsPopup.transform.Find("DialogBox") as RectTransform;

        if (dialog == null) return;

        dialog.localScale = Vector3.zero;
        dialog.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void HideNoCoinsPopup()
    {
        if (noCoinsPopup != null)
            noCoinsPopup.SetActive(false);

        if (_battleInfoPanel != null)
            _battleInfoPanel.SetActive(true);
    }

    public void OnBackButtonClick()
    {
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }

    private string GetCategoryName(GemType type)
    {
        return type switch
        {
            GemType.Fire => "Fire Category",
            GemType.Water => "Water Category",
            GemType.Nature => "Nature Category",
            GemType.Electric => "Storm Category",
            GemType.Psychic => "Psychic Category",
            GemType.Healing => "Light Category",
            _ => type.ToString()
        };
    }
}
