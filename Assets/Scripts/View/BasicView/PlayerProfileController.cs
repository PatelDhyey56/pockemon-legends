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
        { GemType.Healing,  new Color(0.58f, 0.42f, 0.22f) },
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
    private Button          _popupBattleBtn;
    private TextMeshProUGUI _popupBattleBtnText;
    private Image           _popupBattleBtnImg;

    private void Start()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
            return;
        }

        if (backButton != null) backButton.onClick.AddListener(OnBackButtonClick);

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
            avatarBg.DOColor(bg, 0.4f);
        }

        if (usernameText != null) usernameText.text = p.Username;
        if (levelText    != null) levelText.text    = $"Level {p.Level}";

        // XP bar
        float progress = p.GetLevelProgress();
        if (xpBar != null)
            xpBar.DOFillAmount(progress, 0.6f).SetEase(Ease.OutCubic);

        if (xpProgressText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL)
                xpProgressText.text = "MAX LEVEL 🏆";
            else
                xpProgressText.text = $"{p.XP} / {p.XP + p.GetXPToNextLevel()} XP";
        }

        if (xpToNextText != null)
        {
            if (p.Level >= PlayerProfileManager.MAX_LEVEL)
                xpToNextText.text = "";
            else
                xpToNextText.text = $"{p.GetXPToNextLevel()} XP to Level {p.Level + 1}";
        }
    }

    private void RefreshStats()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (coinsValueText != null) coinsValueText.text = $"🪙  {p.Coins}";

        int totalBattles = p.Wins + p.Losses;
        float winRate    = totalBattles > 0 ? (float)p.Wins / totalBattles * 100f : 0f;

        if (winsValueText    != null) winsValueText.gameObject.SetActive(false);
        if (lossesValueText  != null) lossesValueText.gameObject.SetActive(false);
        if (winRateText      != null) winRateText.gameObject.SetActive(false);
        if (battlesPlayedText!= null) battlesPlayedText.gameObject.SetActive(false);
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
            collectionCountText.text = $"Collection: {owned} / {total} Creatures";

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
                avatarImg.color  = isOwned ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
            }

            // — Name
            TextMeshProUGUI nameText = card.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text  = entry.Name;
                nameText.color = isOwned ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.7f);
            }

            // — Type badge
            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = GetCategoryName(entry.Type);
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                {
                    typeText.color = isOwned ? tc : new Color(tc.r * 0.6f, tc.g * 0.6f, tc.b * 0.6f, 0.6f);
                }
            }

            // — Stats (ATK / Energy stones)
            TextMeshProUGUI statsText = card.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
            {
                int dmg    = BoardManager.GetBaseValueForCreature(entry.Name);
                int energy = BoardManager.GetMaxEnergyForCreature(entry.Name);
                statsText.text  = $"ATK {dmg}  ⚡{energy}";
                statsText.color = isOwned ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            // — Price (not shown on owned cards, show "Owned")
            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                if (isOwned)
                {
                    priceText.text = entry.IsStarter ? "Starter" : "Owned";
                    priceText.color = new Color(0.3f, 0.9f, 0.4f); // green
                }
                else
                {
                    priceText.text = entry.Price > 0 ? $"Locked (🪙 {entry.Price})" : "Locked";
                    priceText.color = new Color(0.85f, 0.35f, 0.35f); // reddish
                }
            }

            // — Type background tint
            Image cardBg = card.transform.Find("CardBg")?.GetComponent<Image>();
            if (cardBg != null && TypeColors.TryGetValue(entry.Type, out Color bg))
            {
                cardBg.color = isOwned ? new Color(bg.r, bg.g, bg.b, 0.22f) : new Color(bg.r * 0.4f, bg.g * 0.4f, bg.b * 0.4f, 0.12f);
            }

            // — "In Team" badge
            GameObject teamBadge = card.transform.Find("TeamBadge")?.gameObject;
            if (teamBadge != null)
            {
                bool inTeam = isOwned && profile.BattleTeam.Contains(entry.Name);
                teamBadge.SetActive(inTeam);
                if (inTeam)
                {
                    int slotIndex = profile.BattleTeam.IndexOf(entry.Name) + 1;
                    var slotLabelTMP = teamBadge.transform.Find("SlotLabel")?.GetComponent<TextMeshProUGUI>();
                    if (slotLabelTMP != null)
                    {
                        slotLabelTMP.text = $"SLOT {slotIndex}";
                    }
                    var teamTextTMP = teamBadge.transform.Find("Text")?.GetComponent<TextMeshProUGUI>();
                    if (teamTextTMP != null)
                    {
                        teamTextTMP.text = "✓ BATTLE"; // right sign
                    }
                }
            }

            // — Owned glow outline
            Image glowImage = card.transform.Find("OwnedGlow")?.GetComponent<Image>();
            if (glowImage != null)
                glowImage.gameObject.SetActive(isOwned);

            // — Lock overlay
            GameObject lockOverlay = card.transform.Find("LockOverlay")?.gameObject;
            if (lockOverlay != null)
                lockOverlay.SetActive(!isOwned);

            // Stagger scale-in animation
            card.transform.localScale = Vector3.zero;
            card.transform.DOScale(1f, 0.28f)
                .SetDelay(index * 0.045f)
                .SetEase(Ease.OutBack);
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
        boxRect.sizeDelta        = new Vector2(600f, 520f);
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(0.09f, 0.09f, 0.13f, 0.97f);
        // Block clicks from bubbling to the dim layer
        box.AddComponent<Button>().onClick.AddListener(() => { });

        // ── ✕ Close button (top-right corner) ────────────────────────
        var xGo = new GameObject("CloseXBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        xGo.transform.SetParent(box.transform, false);
        var xRect = xGo.GetComponent<RectTransform>();
        xRect.anchorMin        = new Vector2(1f, 1f);
        xRect.anchorMax        = new Vector2(1f, 1f);
        xRect.pivot            = new Vector2(1f, 1f);
        xRect.anchoredPosition = new Vector2(-12f, -12f);
        xRect.sizeDelta        = new Vector2(44f, 44f);
        xGo.GetComponent<Image>().color = new Color(0.80f, 0.18f, 0.18f, 0.92f);
        xGo.GetComponent<Button>().onClick.AddListener(CloseDetailsPopup);
        var xLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        xLabel.transform.SetParent(xGo.transform, false);
        var xLabelRect = xLabel.GetComponent<RectTransform>();
        xLabelRect.anchorMin = Vector2.zero; xLabelRect.anchorMax = Vector2.one;
        xLabelRect.offsetMin = Vector2.zero; xLabelRect.offsetMax = Vector2.zero;
        var xTMP = xLabel.GetComponent<TextMeshProUGUI>();
        xTMP.text = "✕"; xTMP.fontSize = 22f; xTMP.alignment = TextAlignmentOptions.Center;
        xTMP.fontStyle = FontStyles.Bold; xTMP.color = Color.white;
        xTMP.raycastTarget = false;

        // ── Avatar (top center) ─────────────────────────────────────
        var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(box.transform, false);
        var avatarRect = avatarGo.GetComponent<RectTransform>();
        avatarRect.anchorMin        = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax        = new Vector2(0.5f, 0.5f);
        avatarRect.pivot            = new Vector2(0.5f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(0f, 200f);
        avatarRect.sizeDelta        = new Vector2(110f, 110f);
        _popupAvatar = avatarGo.GetComponent<Image>();

        // Glow ring behind avatar
        var glowGo = new GameObject("AvatarGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(box.transform, false);
        glowGo.transform.SetSiblingIndex(avatarGo.transform.GetSiblingIndex());
        var glowRect = glowGo.GetComponent<RectTransform>();
        glowRect.anchorMin        = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax        = new Vector2(0.5f, 0.5f);
        glowRect.pivot            = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = new Vector2(0f, 200f);
        glowRect.sizeDelta        = new Vector2(126f, 126f);
        glowGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
        avatarGo.transform.SetAsLastSibling();

        // ── Creature Name ────────────────────────────────────────────
        var nameGo = new GameObject("PokeName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(box.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, 135f);
        nameRect.sizeDelta        = new Vector2(500f, 38f);
        _popupNameText = nameGo.GetComponent<TextMeshProUGUI>();
        _popupNameText.fontSize  = 26f;
        _popupNameText.fontStyle = FontStyles.Bold;
        _popupNameText.alignment = TextAlignmentOptions.Center;
        _popupNameText.color     = Color.white;

        // ── Top separator ────────────────────────────────────────────
        MakeProfileSeparator(box.transform, 97f);

        // ── Stat rows ────────────────────────────────────────────────
        float rowY    = 62f;
        float rowStep = 42f;

        _popupStoneTypeText   = MakeProfileStatRow(box.transform, "StoneTypeRow",  rowY); rowY -= rowStep;
        _popupStoneCapText    = MakeProfileStatRow(box.transform, "StoneCapRow",   rowY); rowY -= rowStep;
        _popupBasePowerText   = MakeProfileStatRow(box.transform, "BasePowRow",    rowY); rowY -= rowStep;
        _popupEvoledPowerText = MakeProfileStatRow(box.transform, "EvoPowRow",     rowY);

        // ── Bottom separator ─────────────────────────────────────────
        MakeProfileSeparator(box.transform, -108f);

        // ── Close button (bottom left side) ──────────────────────────
        var closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(box.transform, false);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin        = new Vector2(0.5f, 0.5f);
        closeRect.anchorMax        = new Vector2(0.5f, 0.5f);
        closeRect.pivot            = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-120f, -155f);
        closeRect.sizeDelta        = new Vector2(200f, 50f);
        closeGo.GetComponent<Image>().color = new Color(0.15f, 0.50f, 0.85f, 1f);
        closeGo.GetComponent<Button>().onClick.AddListener(CloseDetailsPopup);
        var closeTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeTextGo.transform.SetParent(closeGo.transform, false);
        var ctRect = closeTextGo.GetComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero; ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = Vector2.zero; ctRect.offsetMax = Vector2.zero;
        var closeTMP = closeTextGo.GetComponent<TextMeshProUGUI>();
        closeTMP.text = "CLOSE"; closeTMP.fontSize = 18f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.alignment = TextAlignmentOptions.Center; closeTMP.color = Color.white;
        closeTMP.raycastTarget = false;

        // ── Battle Button (bottom right side) ──────────────────────────
        var battleGo = new GameObject("BattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        battleGo.transform.SetParent(box.transform, false);
        var battleRect = battleGo.GetComponent<RectTransform>();
        battleRect.anchorMin        = new Vector2(0.5f, 0.5f);
        battleRect.anchorMax        = new Vector2(0.5f, 0.5f);
        battleRect.pivot            = new Vector2(0.5f, 0.5f);
        battleRect.anchoredPosition = new Vector2(120f, -155f);
        battleRect.sizeDelta        = new Vector2(240f, 50f);
        _popupBattleBtnImg = battleGo.GetComponent<Image>();
        _popupBattleBtn = battleGo.GetComponent<Button>();
        var battleTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        battleTextGo.transform.SetParent(battleGo.transform, false);
        var btRect = battleTextGo.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero; btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero; btRect.offsetMax = Vector2.zero;
        _popupBattleBtnText = battleTextGo.GetComponent<TextMeshProUGUI>();
        _popupBattleBtnText.fontSize = 18f;
        _popupBattleBtnText.fontStyle = FontStyles.Bold;
        _popupBattleBtnText.alignment = TextAlignmentOptions.Center; _popupBattleBtnText.color = Color.white;
        _popupBattleBtnText.raycastTarget = false;

        _detailsPopup.SetActive(false);
    }

    private void CloseDetailsPopup()
    {
        if (_detailsPopup != null)
            _detailsPopup.transform.DOScale(0f, 0.18f).SetEase(Ease.InBack)
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
        r.sizeDelta        = new Vector2(520f, 1.5f);
        sep.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
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
        r.sizeDelta        = new Vector2(520f, 36f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize         = 17f;
        tmp.alignment        = TextAlignmentOptions.Left;
        tmp.color            = new Color(0.88f, 0.88f, 0.92f, 1f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
        string typeIcon = entry.Type switch
        {
            GemType.Fire     => "🔥",
            GemType.Water    => "💧",
            GemType.Nature   => "🍃",
            GemType.Electric => "⚡",
            GemType.Psychic  => "🔮",
            GemType.Healing  => "💖",
            _                => "◆"
        };
        string powerIcon = isHeal ? "💊" : "⚔️";

        if (_popupStoneTypeText != null)
            _popupStoneTypeText.text =
                $"{typeIcon}  <color=#AAAACC>Gem Type:</color>     <b><color={typeColor}>{GetCategoryName(entry.Type)}</color></b>";

        if (_popupStoneCapText != null)
            _popupStoneCapText.text =
                $"💎  <color=#AAAACC>Gem Capacity:</color>  <b><color=#FFE066>{stoneLimit} Gems</color></b>";

        if (_popupBasePowerText != null)
            _popupBasePowerText.text =
                $"{powerIcon}  <color=#AAAACC>Base {powerLabel}:</color>   <b><color=#66EEFF>{basePower}</color></b>";

        if (_popupEvoledPowerText != null)
            _popupEvoledPowerText.text =
                $"✨  <color=#AAAACC>Evolved {powerLabel}:</color> <b><color=#AAFFAA>{evolvedPower}</color></b>";

        // ── Battle Button Config ────────────────────────────────────
        if (_popupBattleBtn != null && _popupBattleBtnText != null && _popupBattleBtnImg != null)
        {
            if (isOwned)
            {
                bool inTeam = profile.BattleTeam.Contains(name);

                // Update text and color
                if (inTeam)
                {
                    _popupBattleBtnText.text = "✓ REMOVE TEAM";
                    _popupBattleBtnImg.color = new Color(0.15f, 0.75f, 0.3f, 1f); // Green
                }
                else
                {
                    _popupBattleBtnText.text = "USE FOR BATTLE";
                    _popupBattleBtnImg.color = new Color(0.85f, 0.45f, 0.1f, 1f); // Orange/Gold
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
                            _popupBattleBtnText.text = "✓ REMOVE TEAM";
                            _popupBattleBtnImg.color = new Color(0.15f, 0.75f, 0.3f, 1f);
                        }
                        else
                        {
                            _popupBattleBtnText.text = "USE FOR BATTLE";
                            _popupBattleBtnImg.color = new Color(0.85f, 0.45f, 0.1f, 1f);
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
                _popupBattleBtnText.text = $"🔒 BUY (🪙 {entry.Price})";
                _popupBattleBtnImg.color = new Color(0.75f, 0.25f, 0.25f, 1f); // Reddish

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
        _detailsPopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack);
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
}
