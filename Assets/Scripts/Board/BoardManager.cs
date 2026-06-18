using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonState
{
    public string Name;
    public GemType Type;
    public Sprite Avatar;
    public int CurrentEnergy;
    public int MaxEnergy;
}

public class PlayerState
{
    public string Name;
    public int MovesRemaining;
    public int HP;
    public int MaxHP = 80;
    public int Shield;
    public List<PokemonState> Pokemons = new List<PokemonState>();
}

public class BoardManager : MonoBehaviour
{
    #region Singleton

    private static BoardManager _instance;

    public static BoardManager GetInstance()
    {
        return _instance;
    }

    #endregion

    #region Events

    public static Action OnBoardInitialized;
    public static Action<List<Vector2Int>> OnMatchesFound;
    // carries the positions that were refilled as brand-new stones (appeared at the top)
    public static Action<List<Vector2Int>> OnCascadeComplete;
    public static Action OnSwapDone;

    public static Action OnTurnChanged;
    public static Action OnMovesChanged;
    public static Action OnHPChanged;
    public static Action<string> OnShowMessage;
    // Fired whenever any Pokémon's collected-stone count changes so the UI
    // can refresh pip charge bars without waiting for a full HP/turn update.
    public static Action OnEnergyChanged;

    #endregion

    private static readonly Dictionary<GemType, string[]> PokemonDatabase = new Dictionary<GemType, string[]>
    {
        { GemType.Fire, new string[] { "Charmander", "Growlithe" } },
        { GemType.Water, new string[] { "Squirtle", "Psyduck" } },
        { GemType.Nature, new string[] { "Bulbasaur", "Oddish" } },
        { GemType.Electric, new string[] { "Pikachu", "Magnemite" } },
        { GemType.Psychic, new string[] { "Abra", "Gastly" } },
        { GemType.Healing, new string[] { "Chansey", "Jigglypuff" } }
    };

    public GridModel Grid { get; private set; }
    public bool IsProcessing { get; set; }

    public PlayerState[] Players { get; private set; }
    public int ActivePlayerIndex { get; private set; }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    private int GetMaxEnergyForType(GemType type)
    {
        switch (type)
        {
            case GemType.Fire: return 6;
            case GemType.Water: return 4;
            case GemType.Nature: return 5;
            case GemType.Electric: return 5;
            case GemType.Psychic: return 5;
            case GemType.Healing: return 4;
            default: return 5;
        }
    }

    private void AssignRandomPokemons(PlayerState player)
    {
        player.Pokemons.Clear();
        List<GemType> availableTypes = new List<GemType>
        {
            GemType.Fire, GemType.Water, GemType.Nature, GemType.Electric, GemType.Psychic, GemType.Healing
        };

        for (int i = 0; i < 2; i++)
        {
            int randIdx = UnityEngine.Random.Range(0, availableTypes.Count);
            GemType type = availableTypes[randIdx];
            availableTypes.RemoveAt(randIdx);

            string[] names = PokemonDatabase[type];
            string pokemonName = names[UnityEngine.Random.Range(0, names.Length)];

            player.Pokemons.Add(new PokemonState
            {
                Name = pokemonName,
                Type = type,
                Avatar = AvatarGenerator.CreatePokemonSprite(pokemonName),
                CurrentEnergy = 0,
                MaxEnergy = GetMaxEnergyForType(type)
            });
        }
    }

    public void InitGame()
    {
        Players = new PlayerState[2];
        Players[0] = new PlayerState
        {
            Name = "Player 1",
            MovesRemaining = 2,
            HP = 80,
            MaxHP = 80,
            Shield = 0
        };
        AssignRandomPokemons(Players[0]);

        Players[1] = new PlayerState
        {
            Name = "Player 2",
            MovesRemaining = 2,
            HP = 80,
            MaxHP = 80,
            Shield = 0
        };
        AssignRandomPokemons(Players[1]);

        ActivePlayerIndex = 0;
        IsProcessing = false;
    }

    public void InitBoard()
    {
        Grid = new GridModel();
        Grid.Init();
        InitGame();
        OnBoardInitialized?.Invoke();
    }

    public bool TrySwap(Vector2Int from, Vector2Int to)
    {
        if (IsProcessing) return false;
        if (!GridModel.IsValidPosition(from.y, from.x)) return false;
        if (!GridModel.IsValidPosition(to.y, to.x)) return false;
        if (!GridModel.AreAdjacent(from, to)) return false;

        Grid.Swap(from, to);

        List<Vector2Int> matches = Grid.FindMatches();

        // Removed the check that reverts the swap if matches.Count == 0.
        // This allows players to swap stones even if there's no match, 
        // deliberately costing them a turn to set up future combos.

        IsProcessing = true;

        Players[ActivePlayerIndex].MovesRemaining--;
        OnMovesChanged?.Invoke();

        OnSwapDone?.Invoke();
        StartCoroutine(ProcessMatches(matches));

        return true;
    }

    // Abilities that fire mid-energy-calculation are queued here and executed
    // AFTER the matched gems have been removed + cascaded so the grid is clean.
    private readonly System.Collections.Generic.Queue<GemType> _pendingAbilities
        = new System.Collections.Generic.Queue<GemType>();

    private IEnumerator ProcessMatches(List<Vector2Int> matches)
    {
        // previously rewardedExtraThisTurn controlled an extra-move bonus
        // for large matches; that feature caused unintended extra moves
        // and has been removed.

        try { IsProcessing = true; } catch { }

        while (matches.Count > 0)
        {
            bool errorOccurred = false;
            try
            {
                OnMatchesFound?.Invoke(matches);

                // Count gem types in the matched set (ignore already-empty slots)
                Dictionary<GemType, int> typeCounts = new Dictionary<GemType, int>();
                foreach (Vector2Int pos in matches)
                {
                    GemType type = Grid.Grid[pos.y, pos.x];
                    if (type != GemType.Charry)
                    {
                        if (!typeCounts.ContainsKey(type)) typeCounts[type] = 0;
                        typeCounts[type]++;
                    }
                }

                // Extra-move reward removed: no action here.

                // Charge matching Pokémon energy; queue any ability that fires
                // (abilities that mutate the grid are deferred until after cascade)
                PlayerState active = Players[ActivePlayerIndex];
                _pendingAbilities.Clear();
                foreach (var kvp in typeCounts)
                {
                    GemType matchType = kvp.Key;
                    int count = kvp.Value;
                    foreach (var pokemon in active.Pokemons)
                    {
                        if (pokemon.Type == matchType)
                        {
                            // Accumulate stones collected — do NOT clamp here.
                            // The while loop below drains energy in stonesRequired chunks,
                            // which naturally keeps CurrentEnergy in [0, stonesRequired-1].
                            // Clamping to MaxEnergy would silently block the attack from firing
                            // if stonesRequired != MaxEnergy (e.g. when using a custom config asset).
                            pokemon.CurrentEnergy += count;
                            OnEnergyChanged?.Invoke();

                            // Use the attack rule's StonesRequired so UI and logic
                            // stay in sync with configurable attacks.
                            AttackRule rule = GetAttackRule(pokemon.Type);
                            int stonesRequired = (rule != null && rule.StonesRequired > 0) ? rule.StonesRequired : pokemon.MaxEnergy;

                            // Fire the attack (and deal damage) once per full bar filled.
                            // Multiple fires in one turn are possible for very large matches.
                            while (pokemon.CurrentEnergy >= stonesRequired)
                            {
                                pokemon.CurrentEnergy -= stonesRequired;
                                _pendingAbilities.Enqueue(pokemon.Type);
                                OnEnergyChanged?.Invoke();
                            }
                        }
                    }
                }

                OnHPChanged?.Invoke();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[ProcessMatches] Exception in energy phase: " + e);
                errorOccurred = true;
            }

            if (errorOccurred) break;

            yield return new WaitForSeconds(0.5f);

            List<Vector2Int> newStonePositions;
            try
            {
                // Remove matched gems, cascade, refill BEFORE abilities mutate the grid.
                // Cascade: existing stones fall down to fill gaps (gravity toward ROWS-1).
                // Refill: brand-new random stones fill every remaining Charry slot (top rows).
                Grid.RemoveGems(matches);
                Grid.Cascade();
                newStonePositions = Grid.Refill();
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[ProcessMatches] Exception in cascade phase: " + e);
                break;
            }

            // Now fire queued abilities (grid is clean — no stale matched positions)
            bool abilitiesModifiedGrid = false;
            while (_pendingAbilities.Count > 0)
            {
                GemType abilityType = _pendingAbilities.Dequeue();
                try
                {
                    bool gridChanged = ExecuteAbility(abilityType);
                    if (gridChanged) abilitiesModifiedGrid = true;
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError("[ProcessMatches] Exception in ability: " + e);
                }
            }

            // If any ability removed tiles, cascade + refill again so the board is
            // always full with no Charry holes before the visual refresh fires.
            if (abilitiesModifiedGrid)
            {
                Grid.Cascade();
                List<Vector2Int> extraNew = Grid.Refill();
                // Merge extra new positions so the UI can animate them dropping in too
                newStonePositions.AddRange(extraNew);
            }

            // Pass the set of brand-new stone positions to the view layer so it can
            // animate them falling in from the top while cascading existing stones.
            OnCascadeComplete?.Invoke(newStonePositions);
            yield return new WaitForSeconds(0.5f);

            try { matches = Grid.FindMatches(); }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError("[ProcessMatches] Exception in FindMatches: " + e);
                break;
            }
        }

        try { EndPlayerMove(); }
        catch (System.Exception e) { UnityEngine.Debug.LogError("[ProcessMatches] EndPlayerMove: " + e); }
        finally { IsProcessing = false; } // ALWAYS released — prevents permanent board lock
    }


    /// <summary>
    /// Executes the ability for the given stone type.
    /// Returns true if the ability removed tiles from the grid
    /// (Water / Electric), false otherwise.
    /// </summary>
    private bool ExecuteAbility(GemType type)
    {
        int activeIdx = ActivePlayerIndex;
        int opponentIdx = (ActivePlayerIndex == 0) ? 1 : 0;

        PlayerState active = Players[activeIdx];
        PlayerState opponent = Players[opponentIdx];

        string pokemonName = "";
        foreach (var p in active.Pokemons)
        {
            if (p.Type == type)
            {
                pokemonName = p.Name;
                break;
            }
        }

        AttackRule rule = GetAttackRule(type);
        string attackName = rule != null ? rule.AttackName : "Ability";
        int ruleDamage    = rule != null ? rule.Damage    : 0;

        if (type == GemType.Fire)
        {
            int damage = ruleDamage > 0 ? ruleDamage : 15;
            ApplyDamage(opponentIdx, damage);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Dealt " + damage + " damage!");
            return false; // grid unchanged
        }
        else if (type == GemType.Water)
        {
            // Collect all non-empty tiles, shuffle the first 3, then remove them.
            List<Vector2Int> allTiles = new List<Vector2Int>();
            for (int r = 0; r < GridModel.ROWS; r++)
                for (int c = 0; c < GridModel.COLS; c++)
                    if (Grid.Grid[r, c] != GemType.Charry)
                        allTiles.Add(new Vector2Int(c, r));

            int actualRemove = Mathf.Min(3, allTiles.Count);
            for (int i = 0; i < actualRemove; i++)
            {
                int randIdx = UnityEngine.Random.Range(i, allTiles.Count);
                Vector2Int temp = allTiles[i];
                allTiles[i] = allTiles[randIdx];
                allTiles[randIdx] = temp;
            }

            if (actualRemove > 0)
                Grid.RemoveGems(allTiles.GetRange(0, actualRemove));

            int waterDamage = ruleDamage > 0 ? ruleDamage : 10;
            ApplyDamage(opponentIdx, waterDamage);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Dealt " + waterDamage + " dmg & removed " + actualRemove + " stones!");
            return actualRemove > 0; // grid modified only if tiles were actually removed
        }
        else if (type == GemType.Nature)
        {
            int heal = ruleDamage > 0 ? ruleDamage : 15; // reuse Damage field for heal amount if set
            ApplyHeal(activeIdx, heal);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Healed " + heal + " HP!");
            return false;
        }
        else if (type == GemType.Electric)
        {
            int damage = ruleDamage > 0 ? ruleDamage : 10;
            ApplyDamage(opponentIdx, damage);

            int randRow = UnityEngine.Random.Range(0, GridModel.ROWS);
            List<Vector2Int> rowTiles = new List<Vector2Int>();
            for (int c = 0; c < GridModel.COLS; c++)
                rowTiles.Add(new Vector2Int(c, randRow));
            Grid.RemoveGems(rowTiles);

            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Dealt " + damage + " dmg and cleared Row " + (randRow + 1) + "!");
            return true; // row cleared → grid modified
        }
        else if (type == GemType.Psychic)
        {
            int damage = ruleDamage > 0 ? ruleDamage : 8;
            int shield = 8;
            ApplyDamage(opponentIdx, damage);
            active.Shield += shield;
            OnHPChanged?.Invoke(); // shield value shows in HP display
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Dealt " + damage + " dmg & got " + shield + " Shield!");
            return false;
        }
        else if (type == GemType.Healing)
        {
            int heal = ruleDamage > 0 ? ruleDamage : 20;
            ApplyHeal(activeIdx, heal);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + "! Healed " + heal + " HP!");
            return false;
        }

        return false; // unknown type — grid unchanged
    }

    private void ApplyDamage(int targetIdx, int amount)
    {
        PlayerState target = Players[targetIdx];
        if (target.Shield > 0)
        {
            if (target.Shield >= amount)
            {
                target.Shield -= amount;
                amount = 0;
            }
            else
            {
                amount -= target.Shield;
                target.Shield = 0;
            }
        }

        target.HP = Mathf.Max(0, target.HP - amount);
        OnHPChanged?.Invoke();

        if (target.HP <= 0)
        {
            PlayerState winner = Players[targetIdx == 0 ? 1 : 0];
            OnShowMessage?.Invoke(winner.Name + " Wins!");
            StartCoroutine(ResetGameAfterDelay());
        }
    }

    private void ApplyHeal(int targetIdx, int amount)
    {
        PlayerState target = Players[targetIdx];
        target.HP = Mathf.Min(target.MaxHP, target.HP + amount);
        OnHPChanged?.Invoke();
    }

    private IEnumerator ResetGameAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        // Stop the ProcessMatches coroutine (and any others) that may still be
        // running from the finished game before we reinitialise the board.
        // Without this the old coroutine keeps accessing and mutating the new grid.
        StopAllCoroutines();
        IsProcessing = false;
        _pendingAbilities.Clear();
        InitBoard();
    }

    private void AddExtraMove()
    {
        Players[ActivePlayerIndex].MovesRemaining++;
        OnMovesChanged?.Invoke();
        OnShowMessage?.Invoke("+1 Extra Move!");
    }

    /// <summary>
    /// Returns the attack rule (stone requirement, damage, name) for a given gem type.
    /// Used by the UI to build pip charge bars and tooltips.
    /// </summary>
    public AttackRule GetAttackRule(GemType type)
    {
        PokemonAttackConfig cfg = PokemonAttackConfig.Load();
        if (cfg != null) return cfg.GetRule(type);
        // Hard-coded fallback mirroring GetMaxEnergyForType
        switch (type)
        {
            case GemType.Fire:     return new AttackRule { Type = type, StonesRequired = 6, Damage = 15, AttackName = "Ember",       EffectDescription = "Deals 15 dmg" };
            case GemType.Water:    return new AttackRule { Type = type, StonesRequired = 4, Damage = 10, AttackName = "Water Gun",   EffectDescription = "10 dmg + remove 3 stones" };
            case GemType.Nature:   return new AttackRule { Type = type, StonesRequired = 5, Damage = 0,  AttackName = "Mega Drain",  EffectDescription = "Heal 15 HP" };
            case GemType.Electric: return new AttackRule { Type = type, StonesRequired = 5, Damage = 10, AttackName = "Spark",       EffectDescription = "10 dmg + clear row" };
            case GemType.Psychic:  return new AttackRule { Type = type, StonesRequired = 5, Damage = 8,  AttackName = "Psybeam",     EffectDescription = "8 dmg + 8 shield" };
            case GemType.Healing:  return new AttackRule { Type = type, StonesRequired = 4, Damage = 0,  AttackName = "Soft-Boiled", EffectDescription = "Heal 20 HP" };
            default:               return new AttackRule { Type = type, StonesRequired = 5, Damage = 5,  AttackName = "Tackle",      EffectDescription = "5 dmg" };
        }
    }

    private void EndPlayerMove()
    {
        if (Players[ActivePlayerIndex].MovesRemaining <= 0)
        {
            ActivePlayerIndex = (ActivePlayerIndex == 0) ? 1 : 0;
            Players[ActivePlayerIndex].MovesRemaining = 2; // Reset moves
            OnTurnChanged?.Invoke();
            OnMovesChanged?.Invoke();
            OnShowMessage?.Invoke(Players[ActivePlayerIndex].Name + "'s Turn!");
        }
    }
}
