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

    public int SelectedBet { get; private set; } = 250;
    public int ActiveBet { get; private set; } = 250;

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
        if (level <= 1) return 0;
        // Each level costs 150 + 50*(level-2) XP from the previous level
        return 150 + 50 * (level - 2);
    }

    #endregion

    private void Awake()
    {
        if (_instance == null)
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
        // Grant starter Creature
        foreach (var entry in AllCreatures)
        {
            if (entry.IsStarter)
            {
                OwnedCreatures.Add(entry.Name);
                BattleTeam.Add(entry.Name);
            }
        }

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

        SaveProfile();
        OnCoinsChanged?.Invoke();
        OnProfileChanged?.Invoke();
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // Battle Results
    // ──────────────────────────────────────────────────────────────

    /// <summary>Call after a battle ends. isWin = whether the player won.</summary>
    public void RecordBattleResult(bool isWin)
    {
        if (isWin)
        {
            Wins++;
            Coins += ActiveBet * 2;
            AddXP(XP_PER_WIN);
            OnCoinsChanged?.Invoke();
        }
        else
        {
            Losses++;
            AddXP(XP_PER_LOSS);
        }

        SaveProfile();
        OnProfileChanged?.Invoke();
    }

    private void AddXP(int amount)
    {
        if (Level >= MAX_LEVEL) return;

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
        SelectedBet      = 250;
        ActiveBet        = 250;

        OnProfileChanged?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────
    // XP progress helper
    // ──────────────────────────────────────────────────────────────

    /// <summary>Returns 0..1 representing XP progress toward next level.</summary>
    public float GetLevelProgress()
    {
        if (Level >= MAX_LEVEL) return 1f;
        int needed = XpRequiredForLevel(Level + 1);
        if (needed <= 0) return 1f;
        return Mathf.Clamp01((float)XP / needed);
    }

    public int GetXPToNextLevel()
    {
        if (Level >= MAX_LEVEL) return 0;
        return XpRequiredForLevel(Level + 1) - XP;
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

    private void LoadProfile()
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

        SelectedBet = GamePlayerPrefs.GetInt(KEY_SELECTED_BET, 250);
        ActiveBet   = GamePlayerPrefs.GetInt(KEY_ACTIVE_BET,   250);
    }
}
