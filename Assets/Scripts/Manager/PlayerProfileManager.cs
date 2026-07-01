using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All Creature available in the game with purchase prices.
/// Starter Creature (Charmander, Bulbasaur) are always unlocked for free.
/// </summary>
[System.Serializable]
public class CreatureShopEntry
{
    public string Name;
    public GemType Type;
    public int Price;        // coins required to purchase
    public bool IsStarter;   // starters are free and unlocked by default
}

/// <summary>
/// Persistent player profile: username, coins, level, XP, owned Creature,
/// selected battle team, win/loss stats. Backed by PlayerPrefs.
/// DontDestroyOnLoad singleton — one instance across all scenes.
/// </summary>
public class PlayerProfileManager : MonoBehaviour
{
    #region Singleton

    private static PlayerProfileManager _instance;
    public static PlayerProfileManager GetInstance()
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<PlayerProfileManager>();
            if (_instance == null)
            {
                GameObject go = new GameObject("PlayerProfileManager");
                _instance = go.AddComponent<PlayerProfileManager>();
            }
        }
        return _instance;
    }

    #endregion

    #region Constants — PlayerPrefs Keys

    private const string KEY_PROFILE_CREATED  = "profile_created";
    private const string KEY_USERNAME         = "username";
    private const string KEY_COINS            = "coins";
    private const string KEY_LEVEL            = "level";
    private const string KEY_XP               = "xp";
    private const string KEY_WINS             = "wins";
    private const string KEY_LOSSES           = "losses";
    private const string KEY_OWNED_CREATURES   = "owned_creatures";   // comma-separated names
    private const string KEY_BATTLE_TEAM      = "battle_team";      // comma-separated names
    private const string KEY_SELECTED_BET     = "selected_bet";
    private const string KEY_ACTIVE_BET       = "active_bet";
    private const string KEY_EVALUATED_CREATURES = "evaluated_creatures";

    // Game balance constants
    public const int INITIAL_COINS    = 1000;
    public const int MAX_LEVEL        = 100;
    public const int XP_PER_WIN       = 100;
    public const int XP_PER_LOSS      = 25;
    public const int COINS_PER_WIN    = 200;  // reward for winning a battle
    public const int BATTLE_COST      = 100;  // entry fee deducted before each battle

    #endregion

    #region Static Creature Catalogue

    /// <summary>
    /// Complete catalogue of all purchasable Creatures.
    /// </summary>
    public static readonly List<CreatureShopEntry> AllCreatures = new List<CreatureShopEntry>
    {
        // Starters — free
        new CreatureShopEntry { Name = "Ember Dragon",      Type = GemType.Fire,     Price = 0,    IsStarter = true },
        new CreatureShopEntry { Name = "Thorn Wolf",        Type = GemType.Nature,   Price = 0,    IsStarter = true },
        
        // Tier 1 — 2000 coins
        new CreatureShopEntry { Name = "Tide Serpent",       Type = GemType.Water,    Price = 2000 },
        new CreatureShopEntry { Name = "Lava Hound",         Type = GemType.Fire,     Price = 2000 },
        new CreatureShopEntry { Name = "Thunder Roc",        Type = GemType.Electric, Price = 2000 },
        new CreatureShopEntry { Name = "Astral Fox",         Type = GemType.Psychic,  Price = 2000 },
        
        // Tier 2 — 3000 coins
        new CreatureShopEntry { Name = "Coral Guardian",     Type = GemType.Water,    Price = 3000 },
        new CreatureShopEntry { Name = "Ancient Treant",     Type = GemType.Nature,   Price = 3000 },
        new CreatureShopEntry { Name = "Storm Drake",        Type = GemType.Electric, Price = 3000 },
        new CreatureShopEntry { Name = "Void Raven",         Type = GemType.Psychic,  Price = 3000 },
        
        // Tier 3 — 4000 coins
        new CreatureShopEntry { Name = "Celestial Unicorn",  Type = GemType.Healing,  Price = 4000 },
        
        // Tier 4 — 5000 coins
        new CreatureShopEntry { Name = "Light Phoenix",      Type = GemType.Healing,  Price = 5000 },
    };

    #endregion

    #region Runtime Profile Data

    public string  Username      { get; private set; }
    public int     Coins         { get; private set; }
    public int     Level         { get; private set; }
    public int     XP            { get; private set; }
    public int     Wins          { get; private set; }
    public int     Losses        { get; private set; }
    public bool    IsProfileCreated { get; private set; }

    /// <summary>Names of all owned Creature.</summary>
    public List<string> OwnedCreatures { get; private set; } = new List<string>();

    /// <summary>Names of selected battle Creature (exactly 2).</summary>
    public List<string> BattleTeam { get; private set; } = new List<string>();

    /// <summary>Names of evaluated Creature.</summary>
    public List<string> EvaluatedCreatures { get; private set; } = new List<string>();

    public int SelectedBet { get; private set; } = 250;
    public int ActiveBet { get; private set; } = 250;

    public int LastEarnedXP { get; private set; } = 0;
    public int LastEarnedCoins { get; private set; } = 0;

    #endregion

    #region Events

    public static Action OnProfileChanged;
    public static Action OnCoinsChanged;
    public static Action OnLevelChanged;

    #endregion

    #region XP Table

    /// <summary>XP needed to reach a given level (level 1 = 0).</summary>
    private static int XpRequiredForLevel(int level)
    {
        if (level <= 1) return 0; // Level 1 is starting, no XP needed to stay at level 1
        
        switch (level)
        {
            case 2: return 1000;
            case 3: return 1200;
            case 4: return 1500;
            case 5: return 1800;
            case 6: return 2000;
            case 7: return 2300;
            case 8: return 2500;
            case 9: return 2800;
            case 10: return 3000;
            case 11: return 3300;
        }

        // Exponential growth after level 11
        // Base of 3300 and a growth factor of 1.05 per level.
        double baseVal = 3300.0;
        double growthFactor = 1.05;
        double exponent = level - 11;
        int rawXp = (int)(baseVal * System.Math.Pow(growthFactor, exponent));

        // Rounding rules based on level to keep numbers clean
        if (level <= 50)
        {
            // Round to the nearest 100 XP
            return (int)(System.Math.Round(rawXp / 100.0) * 100.0);
        }
        else
        {
            // Round to the nearest 1000 XP
            return (int)(System.Math.Round(rawXp / 1000.0) * 1000.0);
        }
    }

    #endregion

    private void Awake()
    {
        if (_instance == null || _instance == this)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProfile();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Profile Creation
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once on first launch to set up the player's profile.
    /// Grants starter Creature and initial coins.
    /// </summary>
    public void CreateProfile(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) username = "Trainer";

        Username         = username.Trim();
        Coins            = INITIAL_COINS;
        Level            = 1;
        XP               = 0;
        Wins             = 0;
        Losses           = 0;
        IsProfileCreated = true;
        SelectedBet      = 250;
        ActiveBet        = 250;

        OwnedCreatures.Clear();
        BattleTeam.Clear();
        EvaluatedCreatures.Clear();
        // Grant starter Creature
        foreach (var entry in AllCreatures)
        {
            if (entry.IsStarter)
            {
                OwnedCreatures.Add(entry.Name);
                BattleTeam.Add(entry.Name);
            }
        }

        // Grant 200 XP for the two free starter creatures (100 XP each)
        AddXP(200);

        SaveProfile();
        OnProfileChanged?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────
    // Store Operations
    // ──────────────────────────────────────────────────────────────

    public bool OwnsCreatures(string name) => OwnedCreatures.Contains(name);

    /// <summary>Toggles a Creature in the battle team. Returns false if the team is full (cannot exceed 2).</summary>
    public bool ToggleBattleTeam(string name)
    {
        if (!OwnedCreatures.Contains(name)) return false;

        if (BattleTeam.Contains(name))
        {
            BattleTeam.Remove(name);
            SaveProfile();
            OnProfileChanged?.Invoke();
            return true;
        }
        else
        {
            if (BattleTeam.Count >= 2)
            {
                return false; // team is full
            }
            BattleTeam.Add(name);
            SaveProfile();
            OnProfileChanged?.Invoke();
            return true;
        }
    }

    public void SetSelectedBet(int amount)
    {
        SelectedBet = amount;
        SaveProfile();
    }

    public void SetActiveBet(int amount)
    {
        ActiveBet = amount;
        SaveProfile();
    }

    public void VerifySelectedBetAffordability()
    {
        if (Coins < SelectedBet)
        {
            if (Coins >= 1000) SelectedBet = 1000;
            else if (Coins >= 500) SelectedBet = 500;
            else SelectedBet = 250;
            SaveProfile();
        }
    }

    /// <summary>Purchase a Creature from the store. Returns true on success.</summary>
    public bool PurchaseCreature(string name)
    {
        var entry = AllCreatures.Find(p => p.Name == name);
        if (entry == null) return false;
        if (OwnedCreatures.Contains(name)) return false;
        if (Coins < entry.Price) return false;

        Coins -= entry.Price;
        OwnedCreatures.Add(name);

        // Grant XP based on the creature's purchase cost
        AddXP(entry.Price);

        SaveProfile();
        OnCoinsChanged?.Invoke();
        OnProfileChanged?.Invoke();
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // Creature Evaluation
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates a creature. 
    /// If evaluated for the first time, returns true (system evaluates).
    /// If already evaluated, returns false (requires match 3 game for power speed up) and loads the match 3 scene.
    /// </summary>
    public bool EvaluateCreature(string name)
    {
        if (EvaluatedCreatures.Contains(name))
        {
            // Already evaluated: load match 3 game for power speed up
            UnityEngine.SceneManagement.SceneManager.LoadScene(Constants.SCENE_CREATURE);
            return false;
        }
        else
        {
            // First time: system evaluates
            EvaluatedCreatures.Add(name);
            SaveProfile();
            OnProfileChanged?.Invoke();
            return true;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Battle Results
    // ──────────────────────────────────────────────────────────────

    /// <summary>Call after a battle ends. isWin = whether the player won.</summary>
    public void RecordBattleResult(bool isWin)
    {
        string outcome = isWin ? "win" : "lose";
        // Default to "Casual" mode and use the currently active bet
        RecordBattleResult(outcome, "Casual", ActiveBet);
    }

    /// <summary>
    /// Records the battle result and calculates XP based on outcome, game mode, and bet coins.
    /// </summary>
    public void RecordBattleResult(string outcome, string gameMode, int betCoin)
    {
        outcome = outcome.ToLower().Trim();
        
        // 1. Base XP depending on outcome
        int baseXP = 50; // Draw / default
        if (outcome == "win")
        {
            Wins++;
            Coins += betCoin * 2;
            baseXP = 100;
            LastEarnedCoins = betCoin * 2;
            OnCoinsChanged?.Invoke();
        }
        else if (outcome == "lose")
        {
            Losses++;
            baseXP = 25;
            LastEarnedCoins = 0;
        }
        else // Draw
        {
            Coins += betCoin; // Refund bet
            baseXP = 50;
            LastEarnedCoins = betCoin;
            OnCoinsChanged?.Invoke();
        }

        // 2. Level multiplier: "if user level is high user get more xp"
        // Add 10% more XP per level
        float levelMultiplier = 1.0f + (Level - 1) * 0.1f;

        // 3. Bet multiplier: depend on bet coin
        float betMultiplier = 1.0f + (betCoin / 500.0f);

        // 4. Game mode multiplier: depend on game mode
        float modeMultiplier = gameMode.ToLower().Trim() switch
        {
            "ranked"    => 1.5f,
            "casual"    => 1.0f,
            "practice"  => 0.5f,
            _           => 1.0f
        };

        // Calculate final XP
        int finalXP = Mathf.RoundToInt(baseXP * levelMultiplier * betMultiplier * modeMultiplier);
        
        LastEarnedXP = finalXP;
        AddXP(finalXP);

        SaveProfile();
        OnProfileChanged?.Invoke();
    }

    private void AddXP(int amount)
    {
        if (Level > MAX_LEVEL) return;

        XP += amount;
        // Level up loop
        while (Level < MAX_LEVEL)
        {
            int needed = XpRequiredForLevel(Level + 1);
            if (XP >= needed)
            {
                XP -= needed;
                Level++;
                OnLevelChanged?.Invoke();
            }
            else break;
        }

        // If exactly at MAX_LEVEL, cap the XP at the final requirements to complete the game
        if (Level == MAX_LEVEL)
        {
            int needed = XpRequiredForLevel(MAX_LEVEL + 1);
            if (XP > needed) XP = needed;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Coin management (for future use)
    // ──────────────────────────────────────────────────────────────

    /// <summary>True when the player has enough coins to enter a battle.</summary>
    public bool CanAffordBattle => Coins >= SelectedBet;

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        SaveProfile();
        OnCoinsChanged?.Invoke();
    }

    /// <summary>
    /// Deducts the battle entry fee (bet amount).
    /// Returns false if the player cannot afford it — caller should block battle entry.
    /// </summary>
    public bool SpendCoinsForBattle(int amount)
    {
        if (Coins < amount) return false;
        Coins -= amount;
        SaveProfile();
        OnCoinsChanged?.Invoke();
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // Logout
    // ──────────────────────────────────────────────────────────────

    /// <summary>Clears all saved profile data and resets the runtime state.</summary>
    public void Logout()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Username         = "";
        Coins            = 0;
        Level            = 1;
        XP               = 0;
        Wins             = 0;
        Losses           = 0;
        IsProfileCreated = false;
        OwnedCreatures.Clear();
        BattleTeam.Clear();
        EvaluatedCreatures.Clear();
        SelectedBet      = 250;
        ActiveBet        = 250;
        
        LastEarnedXP     = 0;
        LastEarnedCoins  = 0;

        OnProfileChanged?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────
    // XP progress helper
    // ──────────────────────────────────────────────────────────────

    /// <summary>Returns 0..1 representing XP progress toward next level.</summary>
    public float GetLevelProgress()
    {
        if (Level > MAX_LEVEL) return 1f;
        int needed = XpRequiredForLevel(Level + 1);
        if (needed <= 0) return 1f;
        
        // If at max level, check if we completed the final XP goal
        if (Level == MAX_LEVEL && XP >= needed) return 1f;

        return Mathf.Clamp01((float)XP / needed);
    }

    public int GetXPToNextLevel()
    {
        if (Level > MAX_LEVEL) return 0;
        int needed = XpRequiredForLevel(Level + 1);

        if (Level == MAX_LEVEL && XP >= needed) return 0;

        return Mathf.Max(0, needed - XP);
    }

    // ──────────────────────────────────────────────────────────────
    // Serialization — PlayerPrefs
    // ──────────────────────────────────────────────────────────────

    private void SaveProfile()
    {
        GamePlayerPrefs.SetBool(KEY_PROFILE_CREATED, IsProfileCreated);
        GamePlayerPrefs.SetString(KEY_USERNAME, Username ?? "");
        GamePlayerPrefs.SetInt(KEY_COINS,  Coins);
        GamePlayerPrefs.SetInt(KEY_LEVEL,  Level);
        GamePlayerPrefs.SetInt(KEY_XP,     XP);
        GamePlayerPrefs.SetInt(KEY_WINS,   Wins);
        GamePlayerPrefs.SetInt(KEY_LOSSES, Losses);
        GamePlayerPrefs.SetString(KEY_OWNED_CREATURES, string.Join(",", OwnedCreatures));
        GamePlayerPrefs.SetString(KEY_BATTLE_TEAM, string.Join(",", BattleTeam));
        GamePlayerPrefs.SetString(KEY_EVALUATED_CREATURES, string.Join(",", EvaluatedCreatures));
        GamePlayerPrefs.SetInt(KEY_SELECTED_BET, SelectedBet);
        GamePlayerPrefs.SetInt(KEY_ACTIVE_BET,   ActiveBet);
        GamePlayerPrefs.Save();
    }

    private string MigrateCreatureNameToCreature(string name)
    {
        switch (name)
        {
            case "Charmander": return "Ember Dragon";
            case "Bulbasaur": return "Thorn Wolf";
            case "Squirtle":
            case "Staryu":
            case "Gyarados": return "Tide Serpent";
            case "Magnemite":
            case "Pikachu": return "Thunder Roc";
            case "Abra":
            case "Mewtwo": return "Astral Fox";
            case "Ponyta":
            case "Growlithe": return "Lava Hound";
            case "Chikorita":
            case "Oddish": return "Ancient Treant";
            case "Clefairy":
            case "Togepi": return "Celestial Unicorn";
            case "Psyduck": return "Coral Guardian";
            case "Jigglypuff":
            case "Chansey": return "Light Phoenix";
            case "Vulpix": return "Ember Dragon";
            case "Tangela": return "Thorn Wolf";
            case "Voltorb":
            case "Electrabuzz": return "Storm Drake";
            case "Ralts":
            case "Gastly": return "Void Raven";
            default: return name;
        }
    }

    public void LoadProfile()
    {
        IsProfileCreated = GamePlayerPrefs.GetBool(KEY_PROFILE_CREATED, false);
        Username         = GamePlayerPrefs.GetString(KEY_USERNAME, "Trainer");
        Coins            = GamePlayerPrefs.GetInt(KEY_COINS,  INITIAL_COINS);
        Level            = GamePlayerPrefs.GetInt(KEY_LEVEL,  1);
        XP               = GamePlayerPrefs.GetInt(KEY_XP,     0);
        Wins             = GamePlayerPrefs.GetInt(KEY_WINS,   0);
        Losses           = GamePlayerPrefs.GetInt(KEY_LOSSES, 0);

        string ownedRaw  = GamePlayerPrefs.GetString(KEY_OWNED_CREATURES, "");
        if (string.IsNullOrEmpty(ownedRaw))
        {
            // Migrate legacy saved profile creatures
            ownedRaw = GamePlayerPrefs.GetString("owned_pokemons", "");
        }
        OwnedCreatures.Clear();
        if (!string.IsNullOrEmpty(ownedRaw))
        {
            foreach (var name in ownedRaw.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string migrated = MigrateCreatureNameToCreature(name.Trim());
                    if (!OwnedCreatures.Contains(migrated))
                        OwnedCreatures.Add(migrated);
                }
            }
        }

        // Ensure starters are always owned
        foreach (var entry in AllCreatures)
        {
            if (entry.IsStarter && !OwnedCreatures.Contains(entry.Name))
                OwnedCreatures.Add(entry.Name);
        }

        // Load Battle Team
        string teamRaw = GamePlayerPrefs.GetString(KEY_BATTLE_TEAM, "");
        BattleTeam.Clear();
        if (!string.IsNullOrEmpty(teamRaw))
        {
            foreach (var name in teamRaw.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string migrated = MigrateCreatureNameToCreature(name.Trim());
                    if (OwnedCreatures.Contains(migrated) && !BattleTeam.Contains(migrated))
                        BattleTeam.Add(migrated);
                }
            }
        }

        // Fallback: if loaded battle team is not exactly 2, auto-populate with the first 2 owned Creature
        if (BattleTeam.Count != 2)
        {
            BattleTeam.Clear();
            for (int i = 0; i < OwnedCreatures.Count && BattleTeam.Count < 2; i++)
            {
                if (!BattleTeam.Contains(OwnedCreatures[i]))
                    BattleTeam.Add(OwnedCreatures[i]);
            }
        }

        // Load Evaluated Creatures
        string evalRaw = GamePlayerPrefs.GetString(KEY_EVALUATED_CREATURES, "");
        EvaluatedCreatures.Clear();
        if (!string.IsNullOrEmpty(evalRaw))
        {
            foreach (var name in evalRaw.Split(','))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string migrated = MigrateCreatureNameToCreature(name.Trim());
                    if (!EvaluatedCreatures.Contains(migrated))
                        EvaluatedCreatures.Add(migrated);
                }
            }
        }

        SelectedBet = GamePlayerPrefs.GetInt(KEY_SELECTED_BET, 250);
        ActiveBet   = GamePlayerPrefs.GetInt(KEY_ACTIVE_BET,   250);
    }

    #region UI Helpers
    
    /// <summary>
    /// Dynamically attaches the Coin Sprite to the right side of the text bounds.
    /// Used globally in all scenes where coins are displayed.
    /// </summary>
    public static void AttachCoinSprite(TMPro.TextMeshProUGUI txt)
    {
        if (txt == null) return;
        txt.ForceMeshUpdate();

        Transform existing = txt.transform.Find("DynamicCoinIcon");
        GameObject coinIconGo;
        if (existing != null)
        {
            coinIconGo = existing.gameObject;
        }
        else
        {
            coinIconGo = new GameObject("DynamicCoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            coinIconGo.transform.SetParent(txt.transform, false);
            var coinIcon = coinIconGo.GetComponent<UnityEngine.UI.Image>();
            Sprite[] uiSprites = Resources.LoadAll<Sprite>("UI/UI-pack_Sprite_1");
            Sprite cSprite = Array.Find(uiSprites, s => s.name.EndsWith("11"));
            if (cSprite != null) coinIcon.sprite = cSprite;
        }

        // Match size exactly to the actual rendered text height (handles Auto Size properly)
        float size = txt.textBounds.size.y;
        if (size <= 0) size = txt.fontSize;
        if (size <= 0) size = 45f;
        
        var rt = coinIconGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        // Align anchors to the parent's pivot so anchoredPosition perfectly matches local space coordinates
        Vector2 parentPivot = txt.rectTransform.pivot;
        rt.anchorMin = parentPivot;
        rt.anchorMax = parentPivot;
        rt.pivot = new Vector2(0f, 0.5f); // Left-middle pivot for the icon

        // Position slightly to the right of the text bounds
        float textRightX = txt.textBounds.max.x;
        float textCenterY = txt.textBounds.center.y;
        float padding = size * 0.15f; // small padding relative to rendered size
        rt.anchoredPosition = new Vector2(textRightX + padding, textCenterY);
    }
    
    #endregion
}
