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

    // ─── Pokémon Collection ──────────────────────────────────────────
    [Header("Pokémon Collection")]
    [SerializeField] private Transform      pokemonGridParent;
    [SerializeField] private GameObject     pokemonCardPrefab;
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

    private void Start()
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null)
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
        if (pokemonGridParent != null)
        {
            var scrollRect = pokemonGridParent.GetComponentInParent<ScrollRect>();
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
        BuildPokemonGrid();
        RefreshBattleButtonState();
    }

    private void RefreshHeader()
    {
        var p = PlayerProfileManager.GetInstance();
        if (p == null) return;

        if (usernameText != null) usernameText.text = "BATTLE PREPARATION";
        if (levelText != null) levelText.text = "Prepare your team of 2 Pokémon for combat";

        // Display user's initial icon
        if (avatarInitialText != null)
            avatarInitialText.text = p.Username.Length > 0 ? p.Username[0].ToString().ToUpper() : "?";

        if (avatarBg != null)
        {
            Color bg = p.Level < 20  ? new Color(0.72f, 0.45f, 0.20f)  // bronze
                     : p.Level < 50  ? new Color(0.65f, 0.65f, 0.70f)  // silver
                     : p.Level < 80  ? new Color(0.85f, 0.72f, 0.10f)  // gold
                                     : new Color(0.40f, 0.85f, 0.95f); // platinum
            avatarBg.DOColor(bg, 0.4f);
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
            coinsValueText.text = $"🪙  {p.Coins}";
        }
        RefreshBattleButtonState();
    }

    private void BuildPokemonGrid()
    {
        if (pokemonGridParent == null || pokemonCardPrefab == null) return;

        foreach (var c in _cards) Destroy(c);
        _cards.Clear();

        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        int owned = profile.OwnedPokemons.Count;
        if (collectionCountText != null)
            collectionCountText.text = $"Owned Pokémon: {owned}";

        int index = 0;
        foreach (var entry in PlayerProfileManager.AllPokemons)
        {
            bool isOwned = profile.OwnsPokemons(entry.Name);
            if (!isOwned) continue;

            GameObject card = Instantiate(pokemonCardPrefab, pokemonGridParent);
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
                avatarImg.sprite = AvatarGenerator.CreatePokemonSprite(entry.Name);
                avatarImg.color  = Color.white;
            }

            TextMeshProUGUI nameText = card.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text  = entry.Name;
                nameText.color = Color.white;
            }

            TextMeshProUGUI typeText = card.transform.Find("TypeText")?.GetComponent<TextMeshProUGUI>();
            if (typeText != null)
            {
                typeText.text = entry.Type.ToString();
                if (TypeColors.TryGetValue(entry.Type, out Color tc))
                    typeText.color = tc;
            }

            TextMeshProUGUI statsText = card.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
            {
                int dmg    = BoardManager.GetBaseValueForPokemon(entry.Name);
                int energy = BoardManager.GetMaxEnergyForPokemon(entry.Name);
                statsText.text  = $"ATK {dmg}  ⚡{energy}";
                statsText.color = new Color(0.8f, 0.8f, 0.8f);
            }

            TextMeshProUGUI priceText = card.transform.Find("PriceText")?.GetComponent<TextMeshProUGUI>();
            if (priceText != null)
            {
                priceText.text = entry.IsStarter ? "Starter" : "Owned";
                priceText.color = new Color(0.3f, 0.9f, 0.4f);
            }

            Image cardBg = card.transform.Find("CardBg")?.GetComponent<Image>();
            if (cardBg != null && TypeColors.TryGetValue(entry.Type, out Color bg))
            {
                cardBg.color = new Color(bg.r, bg.g, bg.b, 0.22f);
            }

            // In Team checkmark
            GameObject teamBadge = card.transform.Find("TeamBadge")?.gameObject;
            if (teamBadge != null)
            {
                bool inTeam = profile.BattleTeam.Contains(entry.Name);
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
                        teamTextTMP.text = "✓ READY";
                    }
                }
            }

            Image glowImage = card.transform.Find("OwnedGlow")?.GetComponent<Image>();
            if (glowImage != null)
                glowImage.gameObject.SetActive(profile.BattleTeam.Contains(entry.Name));

            GameObject lockOverlay = card.transform.Find("LockOverlay")?.gameObject;
            if (lockOverlay != null)
                lockOverlay.SetActive(false);

            card.transform.localScale = Vector3.zero;
            card.transform.DOScale(1f, 0.25f)
                .SetDelay(index * 0.04f)
                .SetEase(Ease.OutBack);
            
            index++;
        }

        if (pokemonGridParent != null)
        {
            Canvas.ForceUpdateCanvases();
            var fitter = pokemonGridParent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(pokemonGridParent.GetComponent<RectTransform>());
        }
    }

    private void OnCardClicked(string pokemonName)
    {
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null) return;

        bool success = profile.ToggleBattleTeam(pokemonName);
        if (!success)
        {
            MessageView.GetInstance()?.ShowMessageView("Your team is full (max 2)! Deselect a Pokémon first.", "Ok");
        }
        else
        {
            BuildPokemonGrid();
            RefreshBattleButtonState();
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
        startBattleButtonText.text = $"START BATTLE (🪙 {betFee})";

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
            MessageView.GetInstance()?.ShowMessageView("You must select exactly 2 Pokémon for battle!", "Ok");
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

        SceneManager.LoadScene(Constants.SCENE_POKEMON);
    }

    private void ShowNoCoinsPopup()
    {
        var p = PlayerProfileManager.GetInstance();
        if (noCoinsPopup == null) return;

        int needed = p != null ? p.SelectedBet : 250;
        if (noCoinsText != null)
        {
            noCoinsText.text = $"You need 🪙 {needed} coins to enter battle!\n\nYou have: 🪙 {p?.Coins ?? 0}\n\nVisit the Store to get more.";
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
            MessageView.GetInstance()?.ShowMessageView("You must select exactly 2 Pokémon for battle!", "Ok");
            return;
        }
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }
}
