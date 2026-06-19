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

    // The individual base damage or healing value for this specific Pokemon
    public int BaseValue;

    // Evolution state — set to true when the owning player collects 4 evolution stones
    public bool IsEvolved;
    // Extra damage added to every attack after evolution (+5 base)
    public int EvolutionDamageBonus;
}

public class PlayerState
{
    public string Name;
    public int MovesRemaining;
    public int HP;
    public int MaxHP = 80;
    public int Shield;
    public List<PokemonState> Pokemons = new List<PokemonState>();

    // Evolution stone tracking: needs 4 to trigger evolution
    public int EvolutionStones;
    public const int EvolutionRequired = 4;
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
    // Fired whenever any Pokémon's collected-stone count changes
    public static Action OnEnergyChanged;
    // Fired when a player picks up an evolution stone (carries playerIndex)
    public static Action<int> OnEvolutionStonesChanged;
    // Fired when a Pokémon evolves (carries playerIndex)
    public static Action<int> OnEvolved;

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

    public static int GetMaxEnergyForPokemon(string name)
    {
        switch (name)
        {
            // Fire
            case "Charmander": return 6;
            case "Growlithe":  return 8;
            
            // Water
            case "Squirtle":   return 4;
            case "Psyduck":    return 6;
            
            // Nature
            case "Bulbasaur":  return 5;
            case "Oddish":     return 7;
            
            // Electric
            case "Pikachu":    return 6;
            case "Magnemite":  return 4;
            
            // Psychic
            case "Abra":       return 4;
            case "Gastly":     return 6;
            
            // Healing
            case "Chansey":    return 8;
            case "Jigglypuff": return 5;
            
            default:           return 5;
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
                MaxEnergy = GetMaxEnergyForPokemon(pokemonName),
                BaseValue = GetBaseValueForPokemon(pokemonName)
            });
        }
    }

    public static int GetBaseValueForPokemon(string name)
    {
        switch (name)
        {
            // Fire
            case "Charmander": return 10;
            case "Growlithe":  return 20;
            
            // Water
            case "Squirtle":   return 8;
            case "Psyduck":    return 12;
            
            // Nature
            case "Bulbasaur":  return 12;
            case "Oddish":     return 18;
            
            // Electric
            case "Pikachu":    return 12;
            case "Magnemite":  return 8;
            
            // Psychic
            case "Abra":       return 6;
            case "Gastly":     return 10;
            
            // Healing
            case "Chansey":    return 25;
            case "Jigglypuff": return 15;
            
            default:           return 10;
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
        // Reset attack-config cache so it's reloaded fresh each game
        _attackConfigLoaded = false;
        _cachedAttackConfig  = null;

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

    // Cached attack config — loaded once, not every GetAttackRule() call
    private PokemonAttackConfig _cachedAttackConfig;
    private bool _attackConfigLoaded;

    // Pre-allocated reusable objects to avoid GC pressure in the hot coroutine path
    private static readonly WaitForSeconds _waitMatch   = new WaitForSeconds(0.35f);
    private static readonly WaitForSeconds _waitCascade = new WaitForSeconds(0.3f);
    private static readonly WaitForSeconds _waitReset   = new WaitForSeconds(3f);

    // Reused dictionary to count gem types per match — avoids per-loop allocation
    private readonly Dictionary<GemType, int> _typeCounts = new Dictionary<GemType, int>(8);

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

                PlayerState active = Players[ActivePlayerIndex];

                // Reuse preallocated dictionary — no allocation per loop
                _typeCounts.Clear();
                int evolutionStonesThisBatch = 0;
                foreach (Vector2Int pos in matches)
                {
                    GemType type = Grid.Grid[pos.y, pos.x];
                    if (type == GemType.Charry) continue;
                    if (type == GemType.Evolution)
                    {
                        evolutionStonesThisBatch++;
                        continue;
                    }
                    if (!_typeCounts.ContainsKey(type)) _typeCounts[type] = 0;
                    _typeCounts[type]++;
                }

                // ── Evolution stone pickup ──────────────────────────────────────
                if (evolutionStonesThisBatch > 0)
                {
                    active.EvolutionStones += evolutionStonesThisBatch;
                    OnEvolutionStonesChanged?.Invoke(ActivePlayerIndex);

                    // Trigger evolution when threshold is reached
                    while (active.EvolutionStones >= PlayerState.EvolutionRequired)
                    {
                        active.EvolutionStones -= PlayerState.EvolutionRequired;

                        // Evolve each Pokémon that isn't evolved yet (first unevolved wins)
                        bool evolved = false;
                        foreach (var poke in active.Pokemons)
                        {
                            if (!poke.IsEvolved)
                            {
                                poke.IsEvolved = true;
                                poke.EvolutionDamageBonus += 5; // +5 damage bonus
                                evolved = true;
                                OnEvolved?.Invoke(ActivePlayerIndex);
                                OnShowMessage?.Invoke(active.Name + "'s " + poke.Name + " evolved! +5 bonus damage!");
                                break;
                            }
                        }
                        // If all Pokémon already evolved, give bonus to all
                        if (!evolved)
                        {
                            foreach (var poke in active.Pokemons)
                                poke.EvolutionDamageBonus += 2;
                            OnEvolved?.Invoke(ActivePlayerIndex);
                            OnShowMessage?.Invoke(active.Name + "'s team is fully evolved! +2 more damage!");
                        }
                        OnEvolutionStonesChanged?.Invoke(ActivePlayerIndex);
                    }
                }

                // Extra-move reward removed: no action here.

                // Charge matching Pokémon energy; queue any ability that fires
                // (abilities that mutate the grid are deferred until after cascade)
                _pendingAbilities.Clear();
                foreach (var kvp in _typeCounts)  // reuse cached dict
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

                            int stonesRequired = pokemon.MaxEnergy;

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

            yield return _waitMatch;   // cached — no GC alloc

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
            yield return _waitCascade; // cached — no GC alloc

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
        int activeIdx   = ActivePlayerIndex;
        int opponentIdx = (ActivePlayerIndex == 0) ? 1 : 0;

        PlayerState active   = Players[activeIdx];
        PlayerState opponent  = Players[opponentIdx];

        // Find the Pokémon that owns this gem type so we can read its evolution bonus and individual base value
        string pokemonName = "";
        int evolutionBonus = 0;
        bool evolvedLabel  = false;
        int pokemonBaseValue = 10;
        int collectLimit = 5;
        foreach (var p in active.Pokemons)
        {
            if (p.Type == type)
            {
                pokemonName    = p.Name;
                evolutionBonus = p.EvolutionDamageBonus;
                evolvedLabel   = p.IsEvolved;
                pokemonBaseValue = p.BaseValue;
                collectLimit   = p.MaxEnergy;
                break;
            }
        }

        AttackRule rule    = GetAttackRule(type);
        string attackName  = rule != null ? rule.AttackName : "Ability";

        // Helper: apply evolution bonus to damage-dealing attacks
        // Shows "(+N evolved)" in the message when bonus is active
        string EvolvedTag(int bonus) => bonus > 0 ? $" (+{bonus} evolved)" : "";

        if (type == GemType.Fire)
        {
            int damage = pokemonBaseValue + evolutionBonus;
            ApplyDamage(opponentIdx, damage);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                "! Dealt " + damage + " damage!" + EvolvedTag(evolutionBonus));
            return false;
        }
        else if (type == GemType.Water)
        {
            List<Vector2Int> allTiles = new List<Vector2Int>();
            for (int r = 0; r < GridModel.ROWS; r++)
                for (int c = 0; c < GridModel.COLS; c++)
                    if (Grid.Grid[r, c] != GemType.Charry)
                        allTiles.Add(new Vector2Int(c, r));

            int removeLimit = 1;
            if (pokemonName == "Psyduck") removeLimit = 3;
            else if (pokemonName == "Squirtle") removeLimit = 2;

            int actualRemove = Mathf.Min(removeLimit, allTiles.Count);
            for (int i = 0; i < actualRemove; i++)
            {
                int randIdx = UnityEngine.Random.Range(i, allTiles.Count);
                Vector2Int temp = allTiles[i]; allTiles[i] = allTiles[randIdx]; allTiles[randIdx] = temp;
            }
            if (actualRemove > 0) Grid.RemoveGems(allTiles.GetRange(0, actualRemove));

            int waterDamage = pokemonBaseValue + evolutionBonus;
            ApplyDamage(opponentIdx, waterDamage);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                "! Dealt " + waterDamage + " dmg & removed " + actualRemove + " opponent stones!" + EvolvedTag(evolutionBonus));
            return actualRemove > 0;
        }
        else if (type == GemType.Nature)
        {
            int heal = pokemonBaseValue + evolutionBonus;
            ApplyHeal(activeIdx, heal);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + 
                "! Healed " + heal + " HP!" + EvolvedTag(evolutionBonus));
            return false;
        }
        else if (type == GemType.Electric)
        {
            int damage = pokemonBaseValue + evolutionBonus;
            ApplyDamage(opponentIdx, damage);

            if (collectLimit >= 6)
            {
                bool isRow = UnityEngine.Random.value < 0.5f;
                if (isRow)
                {
                    int randRow = UnityEngine.Random.Range(0, GridModel.ROWS);
                    List<Vector2Int> rowTiles = new List<Vector2Int>();
                    for (int c = 0; c < GridModel.COLS; c++)
                        rowTiles.Add(new Vector2Int(c, randRow));
                    Grid.RemoveGems(rowTiles);

                    OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                        "! Dealt " + damage + " dmg & cleared Row " + (randRow + 1) + "!" + EvolvedTag(evolutionBonus));
                }
                else
                {
                    int randCol = UnityEngine.Random.Range(0, GridModel.COLS);
                    List<Vector2Int> colTiles = new List<Vector2Int>();
                    for (int r = 0; r < GridModel.ROWS; r++)
                        colTiles.Add(new Vector2Int(randCol, r));
                    Grid.RemoveGems(colTiles);

                    OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                        "! Dealt " + damage + " dmg & cleared Column " + (randCol + 1) + "!" + EvolvedTag(evolutionBonus));
                }
                return true;
            }
            else
            {
                OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                    "! Dealt " + damage + " dmg!" + EvolvedTag(evolutionBonus));
                return false;
            }
        }
        else if (type == GemType.Psychic)
        {
            int damage = pokemonBaseValue + evolutionBonus;
            int shield = 8;
            ApplyDamage(opponentIdx, damage);
            active.Shield += shield;
            OnHPChanged?.Invoke();
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName +
                "! Dealt " + damage + " dmg & got " + shield + " Shield!" + EvolvedTag(evolutionBonus));
            return false;
        }
        else if (type == GemType.Healing)
        {
            int heal = pokemonBaseValue + evolutionBonus;
            ApplyHeal(activeIdx, heal);
            OnShowMessage?.Invoke(active.Name + "'s " + pokemonName + " used " + attackName + 
                "! Healed " + heal + " HP!" + EvolvedTag(evolutionBonus));
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
        yield return _waitReset; // cached — no GC alloc
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
        // Load config once and cache it — prevents Resources.Load on every call
        if (!_attackConfigLoaded)
        {
            _cachedAttackConfig = PokemonAttackConfig.Load();
            _attackConfigLoaded = true;
        }
        if (_cachedAttackConfig != null) return _cachedAttackConfig.GetRule(type);
        // Hard-coded fallback
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
