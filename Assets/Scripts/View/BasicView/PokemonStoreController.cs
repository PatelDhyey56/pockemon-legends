using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// Pokémon Store — purchase new Pokémon with coins.
/// Attach to a GameObject in the StoreScene.
/// Scene name: "StoreScene"
/// </summary>
public class PokemonStoreController : MonoBehaviour
{
    [Header("Store UI")]
    [SerializeField] private Transform       cardContainer;    // scroll view content parent
    [SerializeField] private GameObject      storeCardPrefab;  // a card prefab
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private Button          backButton;

    [Header("Confirm Popup (scene-assigned, can be left empty)")]
    [SerializeField] private GameObject      confirmPopup;
    [SerializeField] private TextMeshProUGUI confirmPokemonName;
    [SerializeField] private TextMeshProUGUI confirmPriceText;
    [SerializeField] private Button          confirmYesBtn;
    [SerializeField] private Button          confirmNoBtn;
    [SerializeField] private Image           confirmPokemonAvatar;

    [Header("Result Popup")]
    [SerializeField] private GameObject      resultPopup;
    [SerializeField] private TextMeshProUGUI resultText;

    // ── Runtime-built purchase popup ─────────────────────────────────
    private GameObject      _purchasePopup;
    private Image           _popupAvatar;
    private TextMeshProUGUI _popupNameText;
    private TextMeshProUGUI _popupStoneTypeText;
    private TextMeshProUGUI _popupStoneCapText;
    private TextMeshProUGUI _popupBasePowerText;
    private TextMeshProUGUI _popupEvoledPowerText;
    private TextMeshProUGUI _popupCostText;
    private Button          _popupBuyBtn;
    private TextMeshProUGUI _popupBuyBtnLabel;
    private Image           _popupBuyBtnImg;

    private string                 _pendingPurchaseName;
    private List<GameObject>       _cards = new List<GameObject>();

    // Gem-type colours
    private static readonly Dictionary<GemType, Color> TypeColors = new Dictionary<GemType, Color>
    {
        { GemType.Fire,     new Color(0.95f, 0.25f, 0.05f) },
        { GemType.Water,    new Color(0.05f, 0.75f, 0.95f) },
        { GemType.Nature,   new Color(0.35f, 0.90f, 0.15f) },
        { GemType.Electric, new Color(1.00f, 0.78f, 0.00f) },
        { GemType.Psychic,  new Color(0.70f, 0.15f, 0.95f) },
        { GemType.Healing,  new Color(0.85f, 0.35f, 0.55f) },
    };

    private void Start()
    {
        // Hide legacy scene popup if wired up
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (resultPopup  != null) resultPopup.SetActive(false);

        RefreshCoinsUI();
        BuildCards();
        BuildPurchasePopup();

        // Boost scroll sensitivity and force layout on the ScrollRect/Content
        if (cardContainer != null)
        {
            var scrollRect = cardContainer.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.scrollSensitivity      = 30f;
                scrollRect.movementType           = UnityEngine.UI.ScrollRect.MovementType.Elastic;
                scrollRect.elasticity             = 0.15f;
                scrollRect.inertia                = true;
                scrollRect.decelerationRate       = 0.135f;
                scrollRect.vertical               = true;
                scrollRect.horizontal             = false;
            }
        }

        PlayerProfileManager.OnCoinsChanged   += RefreshCoinsUI;
        PlayerProfileManager.OnProfileChanged += RefreshCards;

        // Keep legacy buttons wired in case scene still uses them
        if (confirmYesBtn != null) confirmYesBtn.onClick.AddListener(OnConfirmPurchase);
        if (confirmNoBtn  != null) confirmNoBtn.onClick.AddListener(ClosePurchasePopup);
        if (backButton    != null) backButton.onClick.AddListener(OnBackButtonClick);
    }

    private void OnDestroy()
    {
        PlayerProfileManager.OnCoinsChanged   -= RefreshCoinsUI;
        PlayerProfileManager.OnProfileChanged -= RefreshCards;
    }

    // ─── Build / Refresh Cards ───────────────────────────────────────

    private void BuildCards()
    {
        if (cardContainer == null || storeCardPrefab == null) return;

        foreach (var c in _cards) Destroy(c);
        _cards.Clear();

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        int index = 0;
        foreach (var entry in PlayerProfileManager.AllPokemons)
        {
            GameObject card = Instantiate(storeCardPrefab, cardContainer);
            _cards.Add(card);

            bool owned     = profile.OwnsPokemons(entry.Name);
            bool canAfford = profile.Coins >= entry.Price;

            // Card click → purchase popup
            Button cardBtn = card.GetComponent<Button>();
            if (cardBtn == null) cardBtn = card.AddComponent<Button>();
            string captureName = entry.Name;
            cardBtn.onClick.RemoveAllListeners();
            cardBtn.onClick.AddListener(() => OnCardClicked(captureName));

            // Avatar
            Image avatarImg = card.transform.Find("Avatar")?.GetComponent<Image>();
            if (avatarImg != null)
                avatarImg.sprite = AvatarGenerator.CreatePokemonSprite(entry.Name);

            // Name
            TextMeshProUGUI nameText = card.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null) nameText.text = entry.Name;

            // Type label
            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = entry.Type.ToString();
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                    typeText.color = tc;
            }

            // Price / Owned
            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                if (owned)
                    priceText.text = entry.IsStarter ? "Free (Starter)" : "Owned ✓";
                else
                    priceText.text = entry.Price > 0 ? $"🪙 {entry.Price}" : "Free";
            }

            // Card background tint
            Image cardBg = card.transform.Find("CardBg")?.GetComponent<Image>();
            if (cardBg != null && TypeColors.TryGetValue(entry.Type, out Color bg))
                cardBg.color = new Color(bg.r, bg.g, bg.b, 0.18f);

            // Buy button on card
            Button buyBtn = card.transform.Find("BuyButton")?.GetComponent<Button>();
            if (buyBtn != null)
            {
                if (owned)
                {
                    buyBtn.gameObject.SetActive(false);
                }
                else
                {
                    buyBtn.interactable = canAfford;
                    var buyImg = buyBtn.GetComponent<Image>();
                    if (buyImg != null)
                        buyImg.color = canAfford ? Color.white : new Color(1f, 1f, 1f, 0.35f);

                    TextMeshProUGUI btnLabel = buyBtn.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnLabel != null)
                        btnLabel.text = canAfford ? "Buy" : "🪙 Needed";

                    buyBtn.onClick.RemoveAllListeners();
                    buyBtn.onClick.AddListener(() => OnCardClicked(captureName));
                }
            }

            // Stagger scale-in animation
            card.transform.localScale = Vector3.zero;
            card.transform.DOScale(1f, 0.3f).SetDelay(index * 0.06f).SetEase(Ease.OutBack);
            index++;
        }

        // Force the ContentSizeFitter to recalculate after all cards are added
        if (cardContainer != null)
        {
            Canvas.ForceUpdateCanvases();
            var fitter = cardContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    cardContainer.GetComponent<RectTransform>());
        }
    }

    private void RefreshCards() => BuildCards();

    private void RefreshCoinsUI()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (coinsText != null && profile != null)
            coinsText.text = $"🪙 {profile.Coins}";
        RefreshCards();
    }

    // ─── Runtime-built Purchase Popup ────────────────────────────────

    /// <summary>
    /// Builds a fully polished purchase confirmation popup at runtime.
    /// Layout (top → bottom, all centered):
    ///   [✕ Close]  (top-right corner)
    ///   [Avatar  120×120]
    ///   [Pokémon Name]
    ///   [── separator ──]
    ///   🔥 Stone Type:     Fire
    ///   💎 Stone Capacity: 5 Stones
    ///   ⚔️  Base Power:    20
    ///   ✨ Evolved Power:  25
    ///   🪙 Cost:           2000
    ///   [── separator ──]
    ///   [ PURCHASE ]  ← active or blurred
    /// </summary>
    private void BuildPurchasePopup()
    {
        if (_purchasePopup != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // ── Dim overlay ──
        _purchasePopup = new GameObject("StorePurchasePopup", typeof(RectTransform));
        _purchasePopup.transform.SetParent(canvas.transform, false);
        _purchasePopup.transform.SetAsLastSibling();

        var overlayRect = _purchasePopup.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        var dimImg = _purchasePopup.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.72f);

        // Clicking outside closes the popup
        var bgBtn = _purchasePopup.AddComponent<Button>();
        bgBtn.onClick.AddListener(ClosePurchasePopup);

        // ── Dialog box ──────────────────────────────────────────────
        var box = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_purchasePopup.transform, false);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.sizeDelta        = new Vector2(600f, 560f);
        boxRect.anchoredPosition = Vector2.zero;

        var boxImg = box.GetComponent<Image>();
        boxImg.color = new Color(0.09f, 0.09f, 0.13f, 0.97f);

        // Block clicks from propagating through the box to the dim layer
        box.AddComponent<Button>().onClick.AddListener(() => { }); // swallow clicks

        // ── ✕ Close button (top-right corner) ───────────────────────
        var xGo = MakeChild(box.transform, "CloseXBtn");
        var xRect = xGo.AddComponent<RectTransform>();
        xRect.anchorMin        = new Vector2(1f, 1f);
        xRect.anchorMax        = new Vector2(1f, 1f);
        xRect.pivot            = new Vector2(1f, 1f);
        xRect.anchoredPosition = new Vector2(-12f, -12f);
        xRect.sizeDelta        = new Vector2(44f, 44f);

        var xCircle = xGo.AddComponent<Image>();
        xCircle.color = new Color(0.80f, 0.18f, 0.18f, 0.92f);

        var xBtn = xGo.AddComponent<Button>();
        xBtn.onClick.AddListener(ClosePurchasePopup);

        var xLabel = MakeTextChild(xGo.transform, "✕", 22f, Color.white);
        xLabel.alignment = TextAlignmentOptions.Center;
        xLabel.fontStyle = FontStyles.Bold;

        // ── Avatar (centered at top) ─────────────────────────────────
        var avatarGo = MakeChild(box.transform, "Avatar");
        var avatarRect = avatarGo.AddComponent<RectTransform>();
        avatarRect.anchorMin        = new Vector2(0.5f, 0.5f);
        avatarRect.anchorMax        = new Vector2(0.5f, 0.5f);
        avatarRect.pivot            = new Vector2(0.5f, 0.5f);
        avatarRect.anchoredPosition = new Vector2(0f, 215f);
        avatarRect.sizeDelta        = new Vector2(110f, 110f);
        _popupAvatar = avatarGo.AddComponent<Image>();

        // Glow ring behind avatar
        var glowGo = new GameObject("AvatarGlow", typeof(RectTransform), typeof(Image));
        glowGo.transform.SetParent(box.transform, false);
        glowGo.transform.SetSiblingIndex(avatarGo.transform.GetSiblingIndex()); // behind avatar
        var glowRect = glowGo.GetComponent<RectTransform>();
        glowRect.anchorMin        = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax        = new Vector2(0.5f, 0.5f);
        glowRect.pivot            = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = new Vector2(0f, 215f);
        glowRect.sizeDelta        = new Vector2(126f, 126f);
        var glowImg = glowGo.GetComponent<Image>();
        glowImg.color = new Color(1f, 1f, 1f, 0.12f);
        avatarGo.transform.SetAsLastSibling();

        // ── Pokémon Name ─────────────────────────────────────────────
        var nameGo = MakeChild(box.transform, "PokemonName");
        var nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin        = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax        = new Vector2(0.5f, 0.5f);
        nameRect.pivot            = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, 148f);
        nameRect.sizeDelta        = new Vector2(520f, 38f);
        _popupNameText = nameGo.AddComponent<TextMeshProUGUI>();
        _popupNameText.fontSize    = 26f;
        _popupNameText.fontStyle   = FontStyles.Bold;
        _popupNameText.alignment   = TextAlignmentOptions.Center;
        _popupNameText.color       = Color.white;

        // ── Top separator ─────────────────────────────────────────────
        MakeSeparator(box.transform, 108f);

        // ── Stat rows (left-aligned, with icon + label + value) ──────
        float rowY    = 80f;   // top stat Y (inside box, positive = up)
        float rowStep = 42f;   // spacing between rows

        // Row: Stone Type
        var stoneTypeRow = MakeStatRow(box.transform, "StoneTypeRow", rowY);
        _popupStoneTypeText = stoneTypeRow;
        rowY -= rowStep;

        // Row: Stone Capacity
        var stoneCapRow = MakeStatRow(box.transform, "StoneCapRow", rowY);
        _popupStoneCapText = stoneCapRow;
        rowY -= rowStep;

        // Row: Base Power
        var basePowRow = MakeStatRow(box.transform, "BasePowRow", rowY);
        _popupBasePowerText = basePowRow;
        rowY -= rowStep;

        // Row: Evolved Power
        var evoPowRow = MakeStatRow(box.transform, "EvoPowRow", rowY);
        _popupEvoledPowerText = evoPowRow;
        rowY -= rowStep;

        // Row: Cost
        var costRow = MakeStatRow(box.transform, "CostRow", rowY);
        _popupCostText = costRow;

        // ── Bottom separator ──────────────────────────────────────────
        MakeSeparator(box.transform, -108f);

        // ── PURCHASE button ───────────────────────────────────────────
        var buyGo = new GameObject("PurchaseBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        buyGo.transform.SetParent(box.transform, false);
        var buyRect = buyGo.GetComponent<RectTransform>();
        buyRect.anchorMin        = new Vector2(0.5f, 0.5f);
        buyRect.anchorMax        = new Vector2(0.5f, 0.5f);
        buyRect.pivot            = new Vector2(0.5f, 0.5f);
        buyRect.anchoredPosition = new Vector2(0f, -160f);
        buyRect.sizeDelta        = new Vector2(320f, 54f);

        _popupBuyBtnImg       = buyGo.GetComponent<Image>();
        _popupBuyBtnImg.color = new Color(0.12f, 0.72f, 0.35f); // green

        _popupBuyBtn = buyGo.GetComponent<Button>();
        _popupBuyBtn.onClick.AddListener(OnConfirmPurchase);

        _popupBuyBtnLabel = MakeTextChild(buyGo.transform, "🪙  PURCHASE", 20f, Color.white);
        _popupBuyBtnLabel.alignment = TextAlignmentOptions.Center;
        _popupBuyBtnLabel.fontStyle = FontStyles.Bold;

        _purchasePopup.SetActive(false);
    }

    // ── Helper: plain empty child ─────────────────────────────────────
    private static GameObject MakeChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // ── Helper: horizontal separator line ─────────────────────────────
    private static void MakeSeparator(Transform parent, float anchoredY)
    {
        var sep = new GameObject("Separator", typeof(RectTransform), typeof(Image));
        sep.transform.SetParent(parent, false);
        var r = sep.GetComponent<RectTransform>();
        r.anchorMin        = new Vector2(0.5f, 0.5f);
        r.anchorMax        = new Vector2(0.5f, 0.5f);
        r.pivot            = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, anchoredY);
        r.sizeDelta        = new Vector2(520f, 1.5f);
        sep.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
    }

    // ── Helper: single stat-row TMP label (full width, left-aligned) ──
    private static TextMeshProUGUI MakeStatRow(Transform parent, string name, float anchoredY)
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
        tmp.fontSize             = 17f;
        tmp.alignment            = TextAlignmentOptions.Left;
        tmp.color                = new Color(0.88f, 0.88f, 0.92f, 1f);
        tmp.textWrappingMode     = TextWrappingModes.NoWrap;
        return tmp;
    }

    // ── Helper: TMP text spanning full parent rect ─────────────────────
    private static TextMeshProUGUI MakeTextChild(Transform parent, string text, float fontSize, Color color)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    // ─── Open Purchase Popup ──────────────────────────────────────────

    private void OnCardClicked(string pokemonName)
    {
        var entry = PlayerProfileManager.AllPokemons.Find(p => p.Name == pokemonName);
        if (entry == null) return;

        var profile  = PlayerProfileManager.GetInstance();
        bool isOwned = profile != null && profile.OwnsPokemons(pokemonName);

        // Only set pending purchase for unowned Pokémon
        _pendingPurchaseName = isOwned ? null : pokemonName;

        bool canAfford = !isOwned && profile != null && profile.Coins >= entry.Price;

        if (_purchasePopup == null) BuildPurchasePopup();
        if (_purchasePopup == null) return;

        // ── Avatar ──────────────────────────────────────────────────
        if (_popupAvatar != null)
            _popupAvatar.sprite = AvatarGenerator.CreatePokemonSprite(pokemonName);

        // ── Name ────────────────────────────────────────────────────
        if (_popupNameText != null)
            _popupNameText.text = pokemonName.ToUpper();

        // ── Lookup stats ─────────────────────────────────────────────
        int basePower   = BoardManager.GetBaseValueForPokemon(pokemonName);
        int evolvedPower = basePower + 5;
        int stoneLimit  = BoardManager.GetMaxEnergyForPokemon(pokemonName);

        bool isHeal     = entry.Type == GemType.Nature || entry.Type == GemType.Healing;
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

        // ── Stat rows (rich text: label dim, value bright) ───────────
        if (_popupStoneTypeText != null)
            _popupStoneTypeText.text =
                $"{typeIcon}  <color=#AAAACC>Stone Type:</color>     <b><color={typeColor}>{entry.Type}</color></b>";

        if (_popupStoneCapText != null)
            _popupStoneCapText.text =
                $"💎  <color=#AAAACC>Stone Capacity:</color>  <b><color=#FFE066>{stoneLimit} Stones</color></b>";

        if (_popupBasePowerText != null)
            _popupBasePowerText.text =
                $"{powerIcon}  <color=#AAAACC>Base {powerLabel}:</color>   <b><color=#66EEFF>{basePower}</color></b>";

        if (_popupEvoledPowerText != null)
            _popupEvoledPowerText.text =
                $"✨  <color=#AAAACC>Evolved {powerLabel}:</color> <b><color=#AAFFAA>{evolvedPower}</color></b>";

        if (isOwned)
        {
            // ── Already owned: hide cost row, turn buy button into an "owned" badge ──
            if (_popupCostText != null)
                _popupCostText.gameObject.SetActive(false);

            // Re-use the buy button GameObject as a non-interactive badge
            if (_popupBuyBtn != null)
            {
                _popupBuyBtn.gameObject.SetActive(true);
                _popupBuyBtn.interactable = false;  // not clickable
            }
            if (_popupBuyBtnImg != null)
                _popupBuyBtnImg.color = new Color(0.10f, 0.55f, 0.20f, 0.30f); // muted green tint

            if (_popupBuyBtnLabel != null)
            {
                _popupBuyBtnLabel.text  = "✅  ALREADY OWNED";
                _popupBuyBtnLabel.color = new Color(0.45f, 0.95f, 0.55f, 1f);  // bright green
            }
        }
        else
        {
            // ── Not yet owned: show cost + active/blurred purchase button ──
            if (_popupCostText != null)
            {
                _popupCostText.gameObject.SetActive(true);
                string coinColor = canAfford ? "#FFD700" : "#FF6666";
                string suffix    = canAfford ? "" : "  <color=#FF5555><size=14>(Not enough 🪙)</size></color>";
                _popupCostText.text =
                    $"🪙  <color=#AAAACC>Cost:</color>            <b><color={coinColor}>{entry.Price}</color></b>{suffix}";
            }

            if (_popupBuyBtn != null)
            {
                _popupBuyBtn.gameObject.SetActive(true);
                _popupBuyBtn.interactable = canAfford;
            }
            if (_popupBuyBtnImg != null)
            {
                _popupBuyBtnImg.color = canAfford
                    ? new Color(0.08f, 0.68f, 0.30f, 1.0f)   // vivid green when affordable
                    : new Color(0.55f, 0.55f, 0.55f, 0.38f); // grey translucent when not
            }
            if (_popupBuyBtnLabel != null)
            {
                _popupBuyBtnLabel.text  = canAfford ? "🪙  PURCHASE" : "🔒  NOT ENOUGH COINS";
                _popupBuyBtnLabel.color = canAfford ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }
        }

        // ── Show with animation ───────────────────────────────────────
        _purchasePopup.SetActive(true);
        _purchasePopup.transform.SetAsLastSibling();
        _purchasePopup.transform.localScale = Vector3.zero;
        _purchasePopup.transform.DOScale(1f, 0.28f).SetEase(Ease.OutBack);
    }

    // Also keep support for legacy BuyButton on cards
    private void OnBuyButtonClick(string pokemonName) => OnCardClicked(pokemonName);

    private void ClosePurchasePopup()
    {
        if (_purchasePopup != null)
        {
            _purchasePopup.transform.DOScale(0f, 0.18f).SetEase(Ease.InBack)
                .OnComplete(() => _purchasePopup.SetActive(false));
        }
        if (confirmPopup != null) confirmPopup.SetActive(false);
        _pendingPurchaseName = null;
    }

    // ─── Purchase Flow ────────────────────────────────────────────────

    private void OnConfirmPurchase()
    {
        ClosePurchasePopup();

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        bool success = profile.PurchasePokemon(_pendingPurchaseName);

        string msg = success
            ? $"{_pendingPurchaseName} added to your collection! 🎉"
            : "Purchase failed (not enough coins).";

        ShowResult(msg, success);
        _pendingPurchaseName = null;
    }

    private void OnCancelPurchase() => ClosePurchasePopup();

    private void ShowResult(string message, bool success)
    {
        if (resultPopup == null) return;
        if (resultText != null)
        {
            resultText.text  = message;
            resultText.color = success ? new Color(0.2f, 0.9f, 0.3f) : Color.red;
        }
        resultPopup.SetActive(true);
        resultPopup.transform.localScale = Vector3.zero;
        resultPopup.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack);
        DOVirtual.DelayedCall(2.5f, () => { if (resultPopup != null) resultPopup.SetActive(false); });
    }

    public void OnBackButtonClick()
    {
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }
}
