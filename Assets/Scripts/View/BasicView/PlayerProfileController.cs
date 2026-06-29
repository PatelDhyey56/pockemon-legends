using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// Player Profile Scene — shows the trainer's full profile:
///   • Username, Level, XP bar, Wins, Losses, Coin balance
///   • Full list of owned Creature with avatar, type, stats and "In Team" badge
///   • Back button returns to MenuScene
/// Scene name: "PlayerProfileScene"
/// </summary>
public class PlayerProfileController : MonoBehaviour
{
    // ─── Header ─────────────────────────────────────────────────────
    [Header("Profile Header")]
    [SerializeField] private Image           avatarBg;           // coloured circle behind avatar letter
    [SerializeField] private TextMeshProUGUI avatarInitialText;  // first letter of username
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image           xpBar;              // fill image, Horizontal
    [SerializeField] private TextMeshProUGUI xpProgressText;     // "320 / 500 XP"
    [SerializeField] private TextMeshProUGUI xpToNextText;       // "180 XP to next level"

    // ─── Stats Row ───────────────────────────────────────────────────
    [Header("Stats Row")]
    [SerializeField] private TextMeshProUGUI coinsValueText;    // "🪙 1240"
    [SerializeField] private TextMeshProUGUI winsValueText;     // "42"
    [SerializeField] private TextMeshProUGUI lossesValueText;   // "18"
    [SerializeField] private TextMeshProUGUI winRateText;       // "70%"
    [SerializeField] private TextMeshProUGUI battlesPlayedText; // "60 battles"

    // ─── Creature Collection ──────────────────────────────────────────
    [Header("Creature Collection")]
    [UnityEngine.Serialization.FormerlySerializedAs("creatureGridParent")]
    [SerializeField] private Transform      creatureGridParent;  // GridLayout parent
    [UnityEngine.Serialization.FormerlySerializedAs("creatureCardPrefab")]
    [SerializeField] private GameObject     creatureCardPrefab;  // card prefab
    [SerializeField] private TextMeshProUGUI collectionCountText; // "8 / 12 collected"

    [Header("Dialog Box Appearance")]
    [Tooltip("Sprite used as the creature details popup background.")]
    [SerializeField] private Sprite          dialogBoxBackground;

    [Header("Card Appearance")]
    [Tooltip("Sprite used as each creature card background.")]
    [SerializeField] private Sprite          cardBackground;
    [Tooltip("Sprite used as the battle-team badge background on profile cards.")]
    [SerializeField] private Sprite          teamBadgeBackground;

    // ─── Navigation ─────────────────────────────────────────────────
    [Header("Navigation")]
    [SerializeField] private Button backButton;

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

    private static readonly Color PopupBtnGreen = new Color(0.08f, 0.68f, 0.30f, 1f);
    private static readonly Color PopupBtnRed   = new Color(0.85f, 0.15f, 0.15f, 1f);

    private void Start()
    {
        Time.timeScale = 1f; // Ensure timeScale is active on entering profile scene
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null || !profile.IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
            return;
        }

        profile.LoadProfile(); // Force reload latest saved data to avoid caching/stale XP issues

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonClick);

        // Subscribe to live updates (e.g. if profile changes mid-scene)
        PlayerProfileManager.OnProfileChanged += RefreshAll;
        PlayerProfileManager.OnCoinsChanged   += RefreshStats;


        RefreshAll();

        // Boost scroll sensitivity on the creature grid scroll view
        if (creatureGridParent != null)
        {
            var scrollRect = creatureGridParent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity = 30f;
                scrollRect.movementType      = UnityEngine.UI.ScrollRect.MovementType.Elastic;
                scrollRect.elasticity        = 0.15f;
                scrollRect.inertia           = true;
                scrollRect.decelerationRate  = 0.135f;
                scrollRect.vertical          = true;
                scrollRect.horizontal        = false;
            }
        }
    }

    private void OnDestroy()
    {
        PlayerProfileManager.OnProfileChanged -= RefreshAll;
        PlayerProfileManager.OnCoinsChanged   -= RefreshStats;

    }

    // ─── Refresh Methods ─────────────────────────────────────────────

    private void RefreshAll()
    {
        RefreshHeader();
        RefreshStats();
        BuildCreatureGrid();
    }

    private void RefreshHeader()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        // Avatar circle — show first letter of username, colour based on level
        if (avatarInitialText != null)
            avatarInitialText.text = p.Username.Length > 0 ? p.Username[0].ToString().ToUpper() : "?";

        if (avatarBg != null)
        {
            // Cycle colour based on level: bronze→silver→gold→platinum
            Color bg = p.Level < 20  ? new Color(0.72f, 0.45f, 0.20f)  // bronze
                     : p.Level < 50  ? new Color(0.65f, 0.65f, 0.70f)  // silver
                     : p.Level < 80  ? new Color(0.85f, 0.72f, 0.10f)  // gold
                                     : new Color(0.40f, 0.85f, 0.95f); // platinum
            avatarBg.DOColor(bg, 0.4f).SetUpdate(true);
        }

        if (usernameText != null) usernameText.text = p.Username;
        if (levelText    != null) levelText.text    = $"Lv. {p.Level}";

        // XP bar
        float progress = p.GetLevelProgress();
        if (xpBar != null)
            xpBar.DOFillAmount(progress, 0.6f).SetEase(Ease.OutCubic).SetUpdate(true);

        if (xpProgressText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL && p.GetXPToNextLevel() <= 0)
                xpProgressText.text = "GAME COMPLETED";
            else
                xpProgressText.text = $"{p.XP} / {p.XP + p.GetXPToNextLevel()} XP";
        }

        if (xpToNextText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL && p.GetXPToNextLevel() <= 0)
                xpToNextText.text = "Congratulations!";
            else if (p.Level >= PlayerProfileManager.MAX_LEVEL)
                xpToNextText.text = $"{p.GetXPToNextLevel()} XP to complete the game!";
            else
                xpToNextText.text = $"{p.GetXPToNextLevel()} XP to next level";
        }
    }

    private void RefreshStats()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (coinsValueText != null)
            coinsValueText.text = $"{p.Coins}";

        int totalBattles = p.Wins + p.Losses;
        float winRate    = totalBattles > 0 ? (float)p.Wins / totalBattles * 100f : 0f;

        if (winsValueText != null)
            winsValueText.text = $"W: {p.Wins}";
        if (lossesValueText != null)
            lossesValueText.text = $"L: {p.Losses}";
        if (winRateText != null)
            winRateText.text = $"WR: {winRate:F0}%";
        if (battlesPlayedText != null)
            battlesPlayedText.text = $"{totalBattles} battle{(totalBattles == 1 ? "" : "s")}";
    }



    // ─── Creature Grid ────────────────────────────────────────────────

    private void BuildCreatureGrid()
    {
        if (creatureGridParent == null)
        {
            Debug.LogError("[PlayerProfileController] BuildCreatureGrid aborted: creatureGridParent is null!");
            return;
        }
        if (creatureCardPrefab == null)
        {
            Debug.LogError("[PlayerProfileController] BuildCreatureGrid aborted: creatureCardPrefab is null!");
            return;
        }

        // Destroy old cards
        foreach (var c in _cards) Destroy(c);
        _cards.Clear();

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null)
        {
            Debug.LogError("[PlayerProfileController] BuildCreatureGrid aborted: PlayerProfileManager instance is null!");
            return;
        }

        int total  = PlayerProfileManager.AllCreatures.Count;
        int owned  = profile.OwnedCreatures.Count;

        if (collectionCountText != null)
            collectionCountText.text = $"{owned} / {total} collected";

        int index = 0;
        foreach (var entry in PlayerProfileManager.AllCreatures)
        {
            bool isOwned = profile.OwnsCreatures(entry.Name);
            if (!isOwned) continue;

            GameObject card = Instantiate(creatureCardPrefab, creatureGridParent);
            _cards.Add(card);

            // — Card click details handler
            Button cardBtn = card.GetComponent<Button>();
            if (cardBtn == null) cardBtn = card.AddComponent<Button>();
            string captureName = entry.Name;
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() => ShowCreatureDetails(captureName));

            // — Avatar
            Image avatarImg = card.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
            {
                avatarImg.sprite = AvatarGenerator.CreateCreatureSprite(entry.Name);
                avatarImg.preserveAspect = true;
                avatarImg.color  = isOwned ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
            }

            // — Name
            TextMeshProUGUI nameText = card.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text  = entry.Name;
                nameText.color = new Color(1f, 0.97f, 0.88f, 1f);
                nameText.enableAutoSizing = true;
                nameText.fontSizeMin = 10f;
                nameText.fontSizeMax = 28f;
                nameText.enableWordWrapping = false;
            }

            // — Type label
            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = GetCategoryName(entry.Type);
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                    typeText.color = tc;
                typeText.enableAutoSizing = true;
                typeText.fontSizeMin = 8f;
                typeText.fontSizeMax = 22f;
                typeText.enableWordWrapping = false;
            }

            // — Stats (ATK / Energy stones)
            TextMeshProUGUI statsText = card.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
            {
                int dmg    = BoardManager.GetBaseValueForCreature(entry.Name);
                int energy = BoardManager.GetMaxEnergyForCreature(entry.Name);
                
                statsText.text = $"ATK {dmg}  EN {energy}";
                statsText.enableAutoSizing = true;
                statsText.fontSizeMin = 8f;
                statsText.fontSizeMax = 20f;
                statsText.enableWordWrapping = false;
                statsText.color = isOwned ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            // — Owned badge hidden; battle badge is the only selection indicator
            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
                priceText.gameObject.SetActive(false);

            var buyBtnTrans = card.transform.Find("BuyButton");
            if (buyBtnTrans != null)
                buyBtnTrans.gameObject.SetActive(false);

            TextMeshProUGUI slotLabel = card.transform.Find("SlotLabel")?.GetComponent<TextMeshProUGUI>();
            if (slotLabel != null)
                slotLabel.gameObject.SetActive(false);

            // — Card background — parchment sprite with dark olive-brown tint
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

            // — "In Team" badge
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

            // — Green owned glow disabled; battle badge marks squad picks only
            Image glowImage = card.transform.Find("OwnedGlow")?.GetComponent<Image>();
            if (glowImage != null)
                glowImage.gameObject.SetActive(false);

            // — Lock overlay
            GameObject lockOverlay = card.transform.Find("LockOverlay")?.gameObject;
            if (lockOverlay != null)
                lockOverlay.SetActive(!isOwned);

            // Stagger scale-in animation
            card.transform.localScale = Vector3.zero;
            card.transform.DOScale(1f, 0.28f)
                .SetDelay(index * 0.045f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            index++;
        }

        // Force ContentSizeFitter to recalculate immediately after all cards spawn
        if (creatureGridParent != null)
        {
            Canvas.ForceUpdateCanvases();
            var fitter = creatureGridParent.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    creatureGridParent.GetComponent<RectTransform>());

            DisableButtonTextRaycasts(creatureGridParent.gameObject);
        }
    }

    // ─── Details Popup ───────────────────────────────────────────────

    private void CreateDetailsPopup()
    {
        if (_detailsPopup != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // ── Dim overlay ──────────────────────────────────────────────
        _detailsPopup = new GameObject("ProfileDetailsPopup", typeof(RectTransform));
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

        // ── Dialog box ───────────────────────────────────────────────
        var box = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_detailsPopup.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta        = new Vector2(850f, 1150f); // Rescaled to 850x1150
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
        // Block clicks from bubbling to the dim layer
        box.AddComponent<Button>().onClick.AddListener(() => { });

        // ── ✕ Close button (top-right corner) ────────────────────────
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
        Sprite icons9 = System.Array.Find(allIcons, s => s.name == "icons_9");
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

        // ── Avatar (top center) ─────────────────────────────────────
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

        // ── Creature Name ────────────────────────────────────────────
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

        // ── Top separator ────────────────────────────────────────────
        MakeProfileSeparator(box.transform, -25f);

        // ── Stat rows ────────────────────────────────────────────────
        float rowY    = -65f;
        float rowStep = 45f;

        _popupStoneTypeText   = MakeProfileStatRow(box.transform, "StoneTypeRow",  rowY); rowY -= rowStep;
        _popupStoneCapText    = MakeProfileStatRow(box.transform, "StoneCapRow",   rowY); rowY -= rowStep;
        _popupBasePowerText   = MakeProfileStatRow(box.transform, "BasePowRow",    rowY); rowY -= rowStep;
        _popupEvoledPowerText = MakeProfileStatRow(box.transform, "EvoPowRow",     rowY); rowY -= rowStep;
        _popupSkillText       = MakeProfileStatRow(box.transform, "SkillRow",      rowY); rowY -= rowStep;
        _popupEffectText      = MakeProfileStatRow(box.transform, "EffectRow",     -300f);

        // ── Bottom separator ─────────────────────────────────────────
        MakeProfileSeparator(box.transform, -350f);

        // ── Battle / squad button (store-style bottom action button) ─
        var battleGo = new GameObject("BattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        battleGo.transform.SetParent(box.transform, false);
        var battleRect = battleGo.GetComponent<RectTransform>();
        battleRect.anchorMin        = new Vector2(0.5f, 0.5f);
        battleRect.anchorMax        = new Vector2(0.5f, 0.5f);
        battleRect.pivot            = new Vector2(0.5f, 0.5f);
        battleRect.anchoredPosition = new Vector2(0f, -415f);
        battleRect.sizeDelta        = new Vector2(360f, 65f);
        _popupBattleBtnImg = battleGo.GetComponent<Image>();
        _popupBattleBtn = battleGo.GetComponent<Button>();
        ApplyPopupButtonTheme(_popupBattleBtn, _popupBattleBtnImg, PopupBtnGreen);
        var battleTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        battleTextGo.transform.SetParent(battleGo.transform, false);
        var btRect = battleTextGo.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero; btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero; btRect.offsetMax = Vector2.zero;
        _popupBattleBtnText = battleTextGo.GetComponent<TextMeshProUGUI>();
        _popupBattleBtnText.fontSize = 20f;
        _popupBattleBtnText.fontStyle = FontStyles.Bold;
        _popupBattleBtnText.alignment = TextAlignmentOptions.Center;
        _popupBattleBtnText.color = Color.white;
        _popupBattleBtnText.raycastTarget = false;

        _detailsPopup.SetActive(false);
    }

    private void CloseDetailsPopup()
    {
        if (_detailsPopup != null)
            _detailsPopup.transform.DOScale(0f, 0.18f).SetEase(Ease.InBack).SetUpdate(true)
                .OnComplete(() => { if (_detailsPopup != null) _detailsPopup.SetActive(false); });
    }

    // ── Helper: horizontal separator line ──────────────────────────────
    private static void MakeProfileSeparator(Transform parent, float anchoredY)
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

    // ── Helper: single stat row ─────────────────────────────────────────
    private static TextMeshProUGUI MakeProfileStatRow(Transform parent, string name, float anchoredY)
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
        tmp.fontSize         = 30f;
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

        bool isOwned = profile.OwnsCreatures(name);

        // ── Avatar & name ───────────────────────────────────────────
        if (_popupAvatar   != null) _popupAvatar.sprite   = AvatarGenerator.CreateCreatureSprite(name);
        if (_popupNameText != null) _popupNameText.text   = name.ToUpper();

        // ── Stats ───────────────────────────────────────────────────
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

        // ── Battle Button Config ────────────────────────────────────
        if (_popupBattleBtn != null && _popupBattleBtnText != null && _popupBattleBtnImg != null)
        {
            if (isOwned)
            {
                bool inTeam = profile.BattleTeam.Contains(name);

                // Update text and color
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
                    }
                    else
                    {
                        MessageView.GetInstance()?.ShowMessageView("Battle team is full! Deselect another Creature first.", "Ok");
                    }
                });
            }
            else
            {
                // Unowned creature redirect to store scene
                _popupBattleBtnText.text = entry.Price > 0 ? $"{entry.Price}" : "FREE";
                _popupBattleBtnImg.color = new Color(0.55f, 0.55f, 0.55f, 0.38f);

                _popupBattleBtn.onClick.RemoveAllListeners();
                _popupBattleBtn.onClick.AddListener(() =>
                {
                    CloseDetailsPopup();
                    SceneManager.LoadScene(Constants.SCENE_STORE);
                });
            }
        }

        // ── Show with animation ─────────────────────────────────────
        _detailsPopup.SetActive(true);
        _detailsPopup.transform.SetAsLastSibling();
        _detailsPopup.transform.localScale = Vector3.zero;
        _detailsPopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // ─── Navigation ──────────────────────────────────────────────────

    public void OnBackButtonClick()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile != null && profile.BattleTeam.Count != 2)
        {
            MessageView.GetInstance()?.ShowMessageView("You must select exactly 2 Creatures for battle!", "Ok");
            return;
        }
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

    private void DisableButtonTextRaycasts(GameObject parent)
    {
        if (parent == null) return;
        var buttons = parent.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            var texts = btn.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var txt in texts)
                txt.raycastTarget = false;
        }
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
}
