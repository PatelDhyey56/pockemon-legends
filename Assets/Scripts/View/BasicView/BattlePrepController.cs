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

    // ─── Navigation ─────────────────────────────────────────────────
    [Header("Navigation")]
    [SerializeField] private Button backButton;

    // ─── Static UI References (No longer created programmatically) ────
    [Header("Static UI References")]
    [SerializeField] private Button          startBattleButton;
    [SerializeField] private GameObject      noCoinsPopup;
    [SerializeField] private TextMeshProUGUI noCoinsText;
    [SerializeField] private Button          noCoinsOkBtn;

    private TextMeshProUGUI startBattleButtonText;
    private Image           startBattleButtonImg;

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

        // Resolve or build the Start Battle Button dynamically
        SetupStartBattleButtonDynamically();
        
        if (noCoinsOkBtn != null) noCoinsOkBtn.onClick.AddListener(() => { if (noCoinsPopup != null) noCoinsPopup.SetActive(false); });
        if (noCoinsPopup != null) noCoinsPopup.SetActive(false);

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
            }
        }
    }

    private void SetupStartBattleButtonDynamically()
    {
        if (startBattleButton != null)
        {
            startBattleButtonImg = startBattleButton.GetComponent<Image>();
            startBattleButtonText = startBattleButton.GetComponentInChildren<TextMeshProUGUI>();
            startBattleButton.onClick.AddListener(OnStartBattleClick);
            return;
        }

        // Try to find the Text GameObject under the same canvas
        Transform canvasTransform = transform; // BattlePrepController is on the Canvas GameObject
        Transform textTransform = null;

        // Search for a text child with "START BATTLE" or named "Text" positioned in the bottom area
        for (int i = 0; i < canvasTransform.childCount; i++)
        {
            Transform child = canvasTransform.GetChild(i);
            var tmp = child.GetComponent<TextMeshProUGUI>();
            var rt = child.GetComponent<RectTransform>();
            if (tmp != null && (tmp.text.Contains("START BATTLE") || (child.name == "Text" && rt != null && rt.anchoredPosition.y < -700f)))
            {
                textTransform = child;
                startBattleButtonText = tmp;
                break;
            }
        }

        if (textTransform != null)
        {
            // Create a new Button GameObject at the same hierarchy level
            GameObject buttonGo = new GameObject("StartBattleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(canvasTransform, false);

            var buttonRect = buttonGo.GetComponent<RectTransform>();
            var textRect = textTransform.GetComponent<RectTransform>();

            // Copy layout properties from text to button
            buttonRect.anchorMin = textRect.anchorMin;
            buttonRect.anchorMax = textRect.anchorMax;
            buttonRect.anchoredPosition = textRect.anchoredPosition;
            buttonRect.sizeDelta = textRect.sizeDelta;
            buttonRect.pivot = textRect.pivot;
            buttonRect.rotation = textRect.rotation;
            buttonRect.localScale = textRect.localScale;

            // Reparent text to button
            textTransform.SetParent(buttonGo.transform, false);

            // Stretch text to fill button
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;

            startBattleButton = buttonGo.GetComponent<Button>();
            startBattleButtonImg = buttonGo.GetComponent<Image>();

            // Copy background sprite from avatarBg (which is a standard UI sprite) to get round corners
            if (avatarBg != null)
            {
                startBattleButtonImg.sprite = avatarBg.sprite;
                startBattleButtonImg.type = Image.Type.Sliced;
            }
            else
            {
                startBattleButtonImg.color = new Color(0.15f, 0.75f, 0.3f, 1f); // fallback green
            }

            // Set button transition to color tint
            startBattleButton.transition = Selectable.Transition.ColorTint;
            var colors = startBattleButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.5f);
            startBattleButton.colors = colors;

            // Register onClick
            startBattleButton.onClick.AddListener(OnStartBattleClick);
        }
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

        if (usernameText != null) usernameText.text = "BATTLE PREPARATION";
        if (levelText != null) levelText.text = "Prepare your team of 2 Creature for combat";

        // Display user's initial icon
        if (avatarInitialText != null)
            avatarInitialText.text = p.Username.Length > 0 ? p.Username[0].ToString().ToUpper() : "?";

        if (avatarBg != null)
        {
            Color bg = p.Level < 20  ? new Color(0.72f, 0.45f, 0.20f)  // bronze
                     : p.Level < 50  ? new Color(0.65f, 0.65f, 0.70f)  // silver
                     : p.Level < 80  ? new Color(0.85f, 0.72f, 0.10f)  // gold
                                     : new Color(0.40f, 0.85f, 0.95f); // platinum
            avatarBg.DOColor(bg, 0.4f).SetUpdate(true);
        }

        // Hide XP bar & details as they aren't relevant for battle prep
        if (xpBar != null) xpBar.gameObject.SetActive(false);
        if (xpProgressText != null) xpProgressText.gameObject.SetActive(false);
        if (xpToNextText != null) xpToNextText.gameObject.SetActive(false);
    }

    private void RefreshCoins()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p != null && coinsValueText != null)
        {
            coinsValueText.text = $"<color=#FFD700>{p.Coins}</color>";
        }
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
            collectionCountText.text = $"Owned Creatures: {owned} / {total}";

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
                nameText.color = isOwned ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.7f);
            }

            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = GetCategoryName(entry.Type);
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                {
                    typeText.color = isOwned ? tc : new Color(tc.r * 0.6f, tc.g * 0.6f, tc.b * 0.6f, 0.6f);
                }
            }

            TextMeshProUGUI statsText = card.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
            {
                int dmg    = BoardManager.GetBaseValueForCreature(entry.Name);
                int energy = BoardManager.GetMaxEnergyForCreature(entry.Name);
                statsText.text  = $"ATK {dmg}  EN {energy}";
                statsText.color = isOwned ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.5f, 0.5f, 0.5f, 0.5f);
            }

            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                var priceRt = priceText.GetComponent<RectTransform>();
                if (isOwned)
                {
                    priceText.text = entry.IsStarter ? "Starter" : "Owned";
                    priceText.color = new Color(0.3f, 0.9f, 0.4f);
                    if (priceRt != null)
                    {
                        priceRt.anchoredPosition = new Vector2(0f, -300f);
                        priceRt.sizeDelta = new Vector2(420f, 60f);
                    }
                    priceText.alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    priceText.text = entry.Price > 0 ? $"Locked ({entry.Price})" : "Locked";
                    priceText.color = new Color(0.85f, 0.35f, 0.35f);
                    if (priceRt != null)
                    {
                        priceRt.anchoredPosition = new Vector2(-100f, -300f);
                        priceRt.sizeDelta = new Vector2(200f, 60f);
                    }
                    priceText.alignment = TextAlignmentOptions.Center;
                }
            }

            var buyBtnTrans = card.transform.Find("BuyButton");
            if (buyBtnTrans != null)
            {
                buyBtnTrans.gameObject.SetActive(false);
            }

            Image cardBg = card.transform.Find("CardBg")?.GetComponent<Image>();
            if (cardBg != null)
            {
                cardBg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            }

            // In Team checkmark
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
                        teamTextTMP.text = "READY";
                    }
                }
            }

            Image glowImage = card.transform.Find("OwnedGlow")?.GetComponent<Image>();
            if (glowImage != null)
                glowImage.gameObject.SetActive(isOwned && profile.BattleTeam.Contains(entry.Name));

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

        // ── Dim overlay ──
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

        // ── Dialog box ──
        var box = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_detailsPopup.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta        = new Vector2(850f, 1150f); // Rescaled to 850x1150
        boxRect.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(0.09f, 0.09f, 0.13f, 0.97f);
        box.AddComponent<Button>().onClick.AddListener(() => { });

        // ── ✕ Close button (top-right corner) ──
        var xGo = new GameObject("CloseXBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        xGo.transform.SetParent(box.transform, false);
        var xRect = xGo.GetComponent<RectTransform>();
        xRect.anchorMin        = new Vector2(1f, 1f);
        xRect.anchorMax        = new Vector2(1f, 1f);
        xRect.pivot            = new Vector2(1f, 1f);
        xRect.anchoredPosition = new Vector2(-15f, -15f);
        xRect.sizeDelta        = new Vector2(55f, 55f);
        xGo.GetComponent<Image>().color = new Color(0.80f, 0.18f, 0.18f, 0.92f);
        xGo.GetComponent<Button>().onClick.AddListener(CloseDetailsPopup);

        var xLabel = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        xLabel.transform.SetParent(xGo.transform, false);
        var xlRect = xLabel.GetComponent<RectTransform>();
        xlRect.anchorMin = Vector2.zero; xlRect.anchorMax = Vector2.one;
        xlRect.offsetMin = Vector2.zero; xlRect.offsetMax = Vector2.zero;
        var xTMP = xLabel.GetComponent<TextMeshProUGUI>();
        xTMP.text = "X"; xTMP.fontSize = 28f; xTMP.alignment = TextAlignmentOptions.Center;
        xTMP.fontStyle = FontStyles.Bold; xTMP.color = Color.white;
        xTMP.raycastTarget = false;

        // ── Avatar ──
        var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        avatarGo.transform.SetParent(box.transform, false);
        var avatarRect = avatarGo.GetComponent<RectTransform>();
        avatarRect.anchorMin        = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax        = new Vector2(0.5f, 0.5f);
        avatarRect.pivot            = new Vector2(0.5f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(0f, 280f);
        avatarRect.sizeDelta        = new Vector2(500f, 500f);
        _popupAvatar = avatarGo.GetComponent<Image>();
        if (_popupAvatar != null) _popupAvatar.preserveAspect = true;



        // ── Creature Name ──
        var nameGo = new GameObject("PokeName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(box.transform, false);
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, -25f);
        nameRect.sizeDelta        = new Vector2(750f, 60f);
        _popupNameText = nameGo.GetComponent<TextMeshProUGUI>();
        _popupNameText.fontSize  = 44f;
        _popupNameText.fontStyle = FontStyles.Bold;
        _popupNameText.alignment = TextAlignmentOptions.Center;
        _popupNameText.color     = Color.white;

        // ── Top separator ──
        MakePrepSeparator(box.transform, -70f);

        // ── Stat rows ──
        float rowY    = -110f;
        float rowStep = 54f;

        _popupStoneTypeText   = MakePrepStatRow(box.transform, "StoneTypeRow",  rowY); rowY -= rowStep;
        _popupStoneCapText    = MakePrepStatRow(box.transform, "StoneCapRow",   rowY); rowY -= rowStep;
        _popupBasePowerText   = MakePrepStatRow(box.transform, "BasePowRow",    rowY); rowY -= rowStep;
        _popupEvoledPowerText = MakePrepStatRow(box.transform, "EvoPowRow",     rowY); rowY -= rowStep;
        _popupSkillText       = MakePrepStatRow(box.transform, "SkillRow",      rowY); rowY -= rowStep;
        _popupEffectText      = MakePrepStatRow(box.transform, "EffectRow",     rowY);

        // ── Bottom separator ──
        MakePrepSeparator(box.transform, -475f);

        // ── Close button (bottom left) ──
        var closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        closeGo.transform.SetParent(box.transform, false);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin        = new Vector2(0.5f, 0.5f);
        closeRect.anchorMax        = new Vector2(0.5f, 0.5f);
        closeRect.pivot            = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-180f, -525f);
        closeRect.sizeDelta        = new Vector2(280f, 65f);
        closeGo.GetComponent<Image>().color = new Color(0.15f, 0.50f, 0.85f, 1f);
        closeGo.GetComponent<Button>().onClick.AddListener(CloseDetailsPopup);

        var closeTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeTextGo.transform.SetParent(closeGo.transform, false);
        var ctRect = closeTextGo.GetComponent<RectTransform>();
        ctRect.anchorMin = Vector2.zero; ctRect.anchorMax = Vector2.one;
        ctRect.offsetMin = Vector2.zero; ctRect.offsetMax = Vector2.zero;
        var closeTMP = closeTextGo.GetComponent<TextMeshProUGUI>();
        closeTMP.text = "CLOSE"; closeTMP.fontSize = 24f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.alignment = TextAlignmentOptions.Center; closeTMP.color = Color.white;
        closeTMP.raycastTarget = false;

        // ── Battle Button (bottom right) ──
        var battleGo = new GameObject("BattleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        battleGo.transform.SetParent(box.transform, false);
        var battleRect = battleGo.GetComponent<RectTransform>();
        battleRect.anchorMin        = new Vector2(0.5f, 0.5f);
        battleRect.anchorMax        = new Vector2(0.5f, 0.5f);
        battleRect.pivot            = new Vector2(0.5f, 0.5f);
        battleRect.anchoredPosition = new Vector2(180f, -525f);
        battleRect.sizeDelta        = new Vector2(280f, 65f);
        _popupBattleBtnImg = battleGo.GetComponent<Image>();
        _popupBattleBtn = battleGo.GetComponent<Button>();

        var battleTextGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        battleTextGo.transform.SetParent(battleGo.transform, false);
        var btRect = battleTextGo.GetComponent<RectTransform>();
        btRect.anchorMin = Vector2.zero; btRect.anchorMax = Vector2.one;
        btRect.offsetMin = Vector2.zero; btRect.offsetMax = Vector2.zero;
        _popupBattleBtnText = battleTextGo.GetComponent<TextMeshProUGUI>();
        _popupBattleBtnText.fontSize = 24f;
        _popupBattleBtnText.fontStyle = FontStyles.Bold;
        _popupBattleBtnText.alignment = TextAlignmentOptions.Center; _popupBattleBtnText.color = Color.white;
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
        sep.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
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
        r.sizeDelta        = new Vector2(750f, 44f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize         = 24f;
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
        string typeIcon = entry.Type switch
        {
            GemType.Fire     => "",
            GemType.Water    => "",
            GemType.Nature   => "",
            GemType.Electric => "",
            GemType.Psychic  => "",
            GemType.Healing  => "",
            _                => ""
        };
        string powerIcon = "";

        var attackConfig = CreatureAttackConfig.Load();
        var rule = attackConfig != null ? attackConfig.GetRule(entry.Type) : null;
        string abilityName = rule != null ? rule.AttackName : "Tackle";
        string abilityDesc = rule != null ? rule.EffectDescription : "Deals damage";
        int abilityDamage = rule != null ? rule.Damage : 10;
        int stonesReq = rule != null ? rule.StonesRequired : 5;

        string abilityPowerIcon = "";
        string abilityPowerLabel = isHeal ? "Ability Heal:" : "Ability Damage:";

        if (_popupStoneTypeText != null)
            _popupStoneTypeText.text =
                $"{typeIcon}  <color=#AAAACC>Elemental Class:</color> <b><color={typeColor}>{GetCategoryName(entry.Type)}</color></b>";

        if (_popupStoneCapText != null)
            _popupStoneCapText.text =
                $"{powerIcon}  <color=#AAAACC>Power:</color>           <b><color=#FFE066>{basePower}</color></b>";

        if (_popupBasePowerText != null)
            _popupBasePowerText.text =
                $"{abilityPowerIcon}  <color=#AAAACC>{abilityPowerLabel}</color>  <b><color=#66EEFF>{abilityDamage}</color></b>";

        if (_popupEvoledPowerText != null)
            _popupEvoledPowerText.text =
                $"<color=#AAAACC>Gems Required:</color>   <b><color=#AAFFAA>{stonesReq}</color></b>";

        if (_popupSkillText != null)
            _popupSkillText.text =
                $"<color=#AAAACC>Ability:</color>         <b><color=#FFAA22>{abilityName}</color></b>";

        if (_popupEffectText != null)
            _popupEffectText.text =
                $"<color=#AAAACC>Effect:</color>          <b><color=#DDDDFF>{abilityDesc}</color></b>";

        // ── Battle Button Config ──
        if (_popupBattleBtn != null && _popupBattleBtnText != null && _popupBattleBtnImg != null)
        {
            bool inTeam = profile.BattleTeam.Contains(name);

            if (inTeam)
            {
                _popupBattleBtnText.text = "REMOVE TEAM";
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
                        _popupBattleBtnText.text = "REMOVE TEAM";
                        _popupBattleBtnImg.color = new Color(0.15f, 0.75f, 0.3f, 1f);
                    }
                    else
                    {
                        _popupBattleBtnText.text = "USE FOR BATTLE";
                        _popupBattleBtnImg.color = new Color(0.85f, 0.45f, 0.1f, 1f);
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
        if (startBattleButton == null || startBattleButtonText == null || startBattleButtonImg == null) return;

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        bool hasTwo = profile.BattleTeam.Count == 2;
        bool canAfford = profile.CanAffordBattle;

        int betFee = profile.SelectedBet;
        startBattleButtonText.text = $"START BATTLE ({betFee})";

        if (hasTwo && canAfford)
        {
            startBattleButton.interactable = true;
            startBattleButtonImg.color = new Color(0.15f, 0.75f, 0.3f, 1f); // ready green
            startBattleButtonText.color = Color.white;
        }
        else
        {
            startBattleButton.interactable = true; // Let them click to get warning message
            startBattleButtonImg.color = new Color(0.4f, 0.4f, 0.4f, 1f); // grayed out
            startBattleButtonText.color = new Color(1f, 1f, 1f, 0.7f);
        }
    }

    private void OnStartBattleClick()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        if (profile.BattleTeam.Count != 2)
        {
            MessageView.GetInstance()?.ShowMessageView("You must select exactly 2 Creature for battle!", "Ok");
            return;
        }

        if (!profile.CanAffordBattle)
        {
            ShowNoCoinsPopup();
            return;
        }

        // Lock in bet and deduct coins
        profile.SetActiveBet(profile.SelectedBet);
        profile.SpendCoinsForBattle(profile.SelectedBet);

        if (AdMobManager.GetInstance() != null)
            AdMobManager.GetInstance().HideBanner();

        SceneManager.LoadScene(Constants.SCENE_CREATURE);
    }

    private void ShowNoCoinsPopup()
    {
        var p = PlayerProfileManager.GetInstance();
        if (noCoinsPopup == null) return;

        int needed = p != null ? p.SelectedBet : 250;
        if (noCoinsText != null)
        {
            noCoinsText.text = $"You need {needed} coins to enter battle!\n\nYou have: {p?.Coins ?? 0}\n\nVisit the Store to get more.";
        }

        noCoinsPopup.SetActive(true);
        noCoinsPopup.transform.localScale = Vector3.zero;
        noCoinsPopup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
    }

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
