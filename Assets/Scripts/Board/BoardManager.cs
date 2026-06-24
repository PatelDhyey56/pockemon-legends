using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreatureState
{
    public string Name;
    public GemType Type;
    public Sprite Avatar;
    public int CurrentEnergy;
    public int MaxEnergy;

    // The individual base damage or healing value for this specific Creature
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
    public List<CreatureState> Creatures = new List<CreatureState>();

    // Evolution stone tracking: needs 4 to trigger evolution
    public int EvolutionStones;
    public const int EvolutionRequired = 4;

    // Tracker to show auto evolution selection popup only once per 4 stones
    public bool HasPromptedEvolution;
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
    // and the list of existing stones that fell during cascade (from→to)
    public static Action<List<Vector2Int>, List<CascadeMove>> OnCascadeComplete;
    public static Action OnSwapDone;

    public static Action OnTurnChanged;
    public static Action OnMovesChanged;
    public static Action OnHPChanged;
    public static Action<string> OnShowMessage;
    // Fired whenever any Creature's collected-stone count changes
    public static Action OnEnergyChanged;
    // Fired when a player picks up an evolution stone (carries playerIndex)
    public static Action<int> OnEvolutionStonesChanged;
    // Fired when a Creature evolves (carries playerIndex)
    public static Action<int> OnEvolved;
    // Fired when a player's HP drops to 0 or below (carries index of losing player)
    public static Action<int> OnGameOver;
    // Fired when a player reaches 4 or more evolution stones (carries playerIndex and the selection callback)
    public static Action<int, Action<CreatureState>> OnRequestEvolutionSelection;
    // Fired when a Creature successfully evolves (carries evolved CreatureState, old value, new value, and complete callback)
    public static Action<CreatureState, int, int, Action> OnShowEvolutionSuccessPopup;

    #endregion

    private static readonly Dictionary<GemType, string[]> CreatureDatabase = new Dictionary<GemType, string[]>
    {
        { GemType.Fire, new string[] { "Ember Dragon", "Lava Hound" } },
        { GemType.Water, new string[] { "Tide Serpent", "Coral Guardian" } },
        { GemType.Nature, new string[] { "Thorn Wolf", "Ancient Treant" } },
        { GemType.Electric, new string[] { "Thunder Roc", "Storm Drake" } },
        { GemType.Psychic, new string[] { "Astral Fox", "Void Raven" } },
        { GemType.Healing, new string[] { "Celestial Unicorn", "Light Phoenix" } }
    };

    public GridModel Grid { get; private set; }
    public bool IsProcessing { get; set; }
    public bool IsWaitingForEvolutionSelection { get; set; }

    public PlayerState[] Players { get; private set; }
    public int ActivePlayerIndex { get; private set; }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    public static int GetMaxEnergyForCreature(string name)
    {
        switch (name)
        {
            // Fire
            case "Ember Dragon":      return 6;
            case "Lava Hound":        return 8;
            
            // Water
            case "Tide Serpent":      return 8;
            case "Coral Guardian":    return 6;
            
            // Nature
            case "Thorn Wolf":        return 5;
            case "Ancient Treant":    return 7;
            
            // Electric
            case "Thunder Roc":       return 6;
            case "Storm Drake":       return 7;
            
            // Psychic
            case "Astral Fox":        return 8;
            case "Void Raven":        return 6;
            
            // Healing / Light
            case "Celestial Unicorn": return 4;
            case "Light Phoenix":     return 8;
            
            default:                  return 5;
        }
    }

    private void AssignRandomCreatures(PlayerState player)
    {
        player.Creatures.Clear();
        List<GemType> availableTypes = new List<GemType>
        {
            GemType.Fire, GemType.Water, GemType.Nature, GemType.Electric, GemType.Psychic, GemType.Healing
        };

        for (int i = 0; i < 2; i++)
        {
            int randIdx = UnityEngine.Random.Range(0, availableTypes.Count);
            GemType type = availableTypes[randIdx];
            availableTypes.RemoveAt(randIdx);

            string[] names = CreatureDatabase[type];
            string creatureName = names[UnityEngine.Random.Range(0, names.Length)];

            player.Creatures.Add(new CreatureState
            {
                Name = creatureName,
                Type = type,
                Avatar = AvatarGenerator.CreateCreatureSprite(creatureName),
                CurrentEnergy = 0,
                MaxEnergy = GetMaxEnergyForCreature(creatureName),
                BaseValue = GetBaseValueForCreature(creatureName)
            });
        }
    }

    public static int GetBaseValueForCreature(string name)
    {
        switch (name)
        {
            // Fire
            case "Ember Dragon":      return 10;
            case "Lava Hound":        return 20;
            
            // Water
            case "Tide Serpent":      return 25;
            case "Coral Guardian":    return 15;
            
            // Nature
            case "Thorn Wolf":        return 15;
            case "Ancient Treant":    return 20;
            
            // Electric
            case "Thunder Roc":       return 25;
            case "Storm Drake":       return 20;
            
            // Psychic
            case "Astral Fox":        return 25;
            case "Void Raven":        return 25;
            
            // Healing / Light
            case "Celestial Unicorn": return 10;
            case "Light Phoenix":     return 25;
            
            default:                  return 10;
        }
    }

    public void InitGame()
    {
        Players = new PlayerState[2];

        // ── Player 1: use the team selected in PlayerProfileManager ──────────────
        Players[0] = new PlayerState
        {
            Name          = "You",
            MovesRemaining = 2,
            HP            = 80,
            MaxHP         = 80,
            Shield        = 0
        };

        var profile = PlayerProfileManager.GetInstance();
        bool profileTeamReady = profile != null && profile.BattleTeam != null && profile.BattleTeam.Count == 2;

        if (profileTeamReady)
        {
            // Use the player's selected battle team
            Players[0].Name = profile.Username;
            Players[0].Creatures.Clear();
            for (int i = 0; i < 2; i++)
            {
                string pokeName = profile.BattleTeam[i];
                GemType type = GetTypeForCreature(pokeName);
                Players[0].Creatures.Add(new CreatureState
                {
                    Name        = pokeName,
                    Type        = type,
                    Avatar      = AvatarGenerator.CreateCreatureSprite(pokeName),
                    CurrentEnergy = 0,
                    MaxEnergy   = GetMaxEnergyForCreature(pokeName),
                    BaseValue   = GetBaseValueForCreature(pokeName)
                });
            }
        }
        else
        {
            // Fallback: assign random Creature (editor / testing)
            AssignRandomCreatures(Players[0]);
        }

        // ── Player 2: Bot — always random ────────────────────────────────────────
        Players[1] = new PlayerState
        {
            Name          = "Bot",
            MovesRemaining = 2,
            HP            = 80,
            MaxHP         = 80,
            Shield        = 0
        };
        AssignRandomCreatures(Players[1]);

        ActivePlayerIndex = UnityEngine.Random.Range(0, 2);
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
        OnTurnChanged?.Invoke();
        OnMovesChanged?.Invoke();
        OnShowMessage?.Invoke(Players[ActivePlayerIndex].Name + "'s Turn!");
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
    private CreatureAttackConfig _cachedAttackConfig;
    private bool _attackConfigLoaded;

    // Pre-allocated reusable objects to avoid GC pressure in the hot coroutine path
    private static readonly WaitForSeconds _waitMatch   = new WaitForSeconds(0.35f);
    private static readonly WaitForSeconds _waitCascade = new WaitForSeconds(0.6f);
    private static readonly WaitForSeconds _waitReset   = new WaitForSeconds(3f);

    // Reused dictionary to count gem types per match — avoids per-loop allocation
    private readonly Dictionary<GemType, int> _typeCounts = new Dictionary<GemType, int>(8);

    // Abilities that fire mid-energy-calculation are queued here and executed
    // AFTER the matched gems have been removed + cascaded so the grid is clean.
    private readonly System.Collections.Generic.Queue<GemType> _pendingAbilities
        = new System.Collections.Generic.Queue<GemType>();

    private IEnumerator ProcessMatches(List<Vector2Int> matches)
    {
        try { IsProcessing = true; } catch { }

        bool isPlayerSwapMatch = true;

        while (matches.Count > 0)
        {
            bool errorOccurred = false;
            try
            {
                OnMatchesFound?.Invoke(matches);

                PlayerState active = Players[ActivePlayerIndex];

                if (isPlayerSwapMatch && HasMatchGroupOfSizeFourOrMore(matches))
                {
                    AddExtraMove();
                }
                isPlayerSwapMatch = false;

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
                }

                // Charge matching Creature energy; queue any ability that fires
                // (abilities that mutate the grid are deferred until after cascade)
                _pendingAbilities.Clear();
                foreach (var kvp in _typeCounts)  // reuse cached dict
                {
                    GemType matchType = kvp.Key;
                    int count = kvp.Value;
                    foreach (var creature in active.Creatures)
                    {
                        if (creature.Type == matchType)
                        {
                            // Accumulate stones collected — do NOT clamp here.
                            // The while loop below drains energy in stonesRequired chunks,
                            // which naturally keeps CurrentEnergy in [0, stonesRequired-1].
                            // Clamping to MaxEnergy would silently block the attack from firing
                            // if stonesRequired != MaxEnergy (e.g. when using a custom config asset).
                            creature.CurrentEnergy += count;
                            OnEnergyChanged?.Invoke();

                            int stonesRequired = creature.MaxEnergy;

                            // Fire the attack (and deal damage) once per full bar filled.
                            // Multiple fires in one turn are possible for very large matches.
                            while (creature.CurrentEnergy >= stonesRequired)
                            {
                                creature.CurrentEnergy -= stonesRequired;
                                _pendingAbilities.Enqueue(creature.Type);
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
            List<CascadeMove> cascadeMoves = new List<CascadeMove>();
            try
            {
                // Remove matched gems, cascade, refill BEFORE abilities mutate the grid.
                // Cascade: existing stones fall down to fill gaps (gravity toward ROWS-1).
                // Refill: brand-new random stones fill every remaining Charry slot (top rows).
                Grid.RemoveGems(matches);
                cascadeMoves = Grid.Cascade();
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
                List<CascadeMove> abilityMoves = Grid.Cascade();
                cascadeMoves.AddRange(abilityMoves);
                List<Vector2Int> extraNew = Grid.Refill();
                // Merge extra new positions so the UI can animate them dropping in too
                newStonePositions.AddRange(extraNew);
            }

            // Pass the set of brand-new stone positions and cascade move data to the
            // view layer so it can animate both new stones AND falling existing stones.
            OnCascadeComplete?.Invoke(newStonePositions, cascadeMoves);
            yield return _waitCascade; // cached — no GC alloc

            // Trigger evolution selection when threshold is reached AFTER cascade settles
            PlayerState activePlayer = Players[ActivePlayerIndex];
            if (activePlayer.EvolutionStones >= PlayerState.EvolutionRequired && !activePlayer.HasPromptedEvolution)
            {
                activePlayer.HasPromptedEvolution = true; // Mark as prompted
                if (OnRequestEvolutionSelection != null)
                {
                    IsWaitingForEvolutionSelection = true;
                    OnRequestEvolutionSelection.Invoke(ActivePlayerIndex, (selectedCreature) =>
                    {
                        if (selectedCreature != null)
                        {
                            int oldVal = selectedCreature.BaseValue + selectedCreature.EvolutionDamageBonus;
                            
                            selectedCreature.IsEvolved = true;
                            selectedCreature.EvolutionDamageBonus += 5; // +5 damage bonus

                            int newVal = selectedCreature.BaseValue + selectedCreature.EvolutionDamageBonus;

                            activePlayer.EvolutionStones = 0; // Set to 0 after evolution completes
                            activePlayer.HasPromptedEvolution = false; // Reset prompted flag
                            activePlayer.MovesRemaining--; // Evolving charges 1 move!
                            OnMovesChanged?.Invoke();

                            OnEvolved?.Invoke(ActivePlayerIndex);
                            OnEvolutionStonesChanged?.Invoke(ActivePlayerIndex);

                            if (OnShowEvolutionSuccessPopup != null)
                            {
                                OnShowEvolutionSuccessPopup.Invoke(selectedCreature, oldVal, newVal, () =>
                                {
                                    IsWaitingForEvolutionSelection = false;
                                });
                            }
                            else
                            {
                                IsWaitingForEvolutionSelection = false;
                            }
                        }
                        else
                        {
                            // Player canceled the evolution selection
                            IsWaitingForEvolutionSelection = false;
                        }
                    });

                    yield return new WaitUntil(() => !IsWaitingForEvolutionSelection);
                }
                else
                {
                    // Fallback: auto-evolve first unevolved Creature
                    foreach (var poke in activePlayer.Creatures)
                    {
                        if (!poke.IsEvolved)
                        {
                            poke.IsEvolved = true;
                            poke.EvolutionDamageBonus += 5;
                            activePlayer.EvolutionStones = 0;
                            activePlayer.HasPromptedEvolution = false; // Reset prompted flag
                            activePlayer.MovesRemaining--; // Evolving charges 1 move!
                            OnMovesChanged?.Invoke();
                            OnEvolved?.Invoke(ActivePlayerIndex);
                            OnEvolutionStonesChanged?.Invoke(ActivePlayerIndex);
                            break;
                        }
                    }
                }
            }

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

        // Find the Creature that owns this gem type so we can read its evolution bonus and individual base value
        string creatureName = "";
        int evolutionBonus = 0;
        bool evolvedLabel  = false;
        int creatureBaseValue = 10;
        int collectLimit = 5;
        foreach (var p in active.Creatures)
        {
            if (p.Type == type)
            {
                creatureName    = p.Name;
                evolutionBonus = p.EvolutionDamageBonus;
                evolvedLabel   = p.IsEvolved;
                creatureBaseValue = p.BaseValue;
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
            int damage = creatureBaseValue + evolutionBonus;
            ApplyDamage(opponentIdx, damage);
            OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
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
            if (creatureName == "Tide Serpent") removeLimit = 4;
            else if (creatureName == "Coral Guardian") removeLimit = 3;

            int actualRemove = Mathf.Min(removeLimit, allTiles.Count);
            for (int i = 0; i < actualRemove; i++)
            {
                int randIdx = UnityEngine.Random.Range(i, allTiles.Count);
                Vector2Int temp = allTiles[i]; allTiles[i] = allTiles[randIdx]; allTiles[randIdx] = temp;
            }
            if (actualRemove > 0) Grid.RemoveGems(allTiles.GetRange(0, actualRemove));

            int waterDamage = creatureBaseValue + evolutionBonus;
            ApplyDamage(opponentIdx, waterDamage);
            OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
                "! Dealt " + waterDamage + " dmg & removed " + actualRemove + " opponent stones!" + EvolvedTag(evolutionBonus));
            return actualRemove > 0;
        }
        else if (type == GemType.Nature)
        {
            int heal = creatureBaseValue + evolutionBonus;
            ApplyHeal(activeIdx, heal);
            ApplyDamage(opponentIdx, heal);

            OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName + 
                "! Healed " + heal + " HP & dealt " + heal + " damage!" + EvolvedTag(evolutionBonus));
            return false;
        }
        else if (type == GemType.Electric)
        {
            int damage = creatureBaseValue + evolutionBonus;
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

                    OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
                        "! Dealt " + damage + " dmg & cleared Row " + (randRow + 1) + "!" + EvolvedTag(evolutionBonus));
                }
                else
                {
                    int randCol = UnityEngine.Random.Range(0, GridModel.COLS);
                    List<Vector2Int> colTiles = new List<Vector2Int>();
                    for (int r = 0; r < GridModel.ROWS; r++)
                        colTiles.Add(new Vector2Int(randCol, r));
                    Grid.RemoveGems(colTiles);

                    OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
                        "! Dealt " + damage + " dmg & cleared Column " + (randCol + 1) + "!" + EvolvedTag(evolutionBonus));
                }
                return true;
            }
            else
            {
                OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
                    "! Dealt " + damage + " dmg!" + EvolvedTag(evolutionBonus));
                return false;
            }
        }
        else if (type == GemType.Psychic)
        {
            int damage = creatureBaseValue + evolutionBonus;
            int shield = 8;
            ApplyDamage(opponentIdx, damage);
            active.Shield += shield;
            OnHPChanged?.Invoke();
            OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName +
                "! Dealt " + damage + " dmg & got " + shield + " Shield!" + EvolvedTag(evolutionBonus));
            return false;
        }
        else if (type == GemType.Healing)
        {
            int heal = creatureBaseValue + evolutionBonus;
            ApplyHeal(activeIdx, heal);
            ApplyDamage(opponentIdx, heal);

            OnShowMessage?.Invoke(active.Name + "'s " + creatureName + " used " + attackName + 
                "! Healed " + heal + " HP & dealt " + heal + " damage!" + EvolvedTag(evolutionBonus));
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
            OnGameOver?.Invoke(targetIdx);

            // Record battle result in the persistent profile.
            // Player 1 (index 0) wins → isWin = true for the profile player.
            var profile = PlayerProfileManager.GetInstance();
            if (profile != null)
            {
                bool playerWon = (targetIdx != 0); // targetIdx is the LOSER
                profile.RecordBattleResult(playerWon);
            }
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

    private bool HasMatchGroupOfSizeFourOrMore(List<Vector2Int> matches)
    {
        if (matches == null || matches.Count < 4) return false;

        // Group matches into connected components of the same GemType
        HashSet<Vector2Int> unvisited = new HashSet<Vector2Int>(matches);

        while (unvisited.Count > 0)
        {
            // Pick any unvisited element
            Vector2Int start = default;
            foreach (var pos in unvisited)
            {
                start = pos;
                break;
            }

            // Start BFS for this component
            List<Vector2Int> component = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            
            GemType targetType = Grid.Grid[start.y, start.x];
            
            queue.Enqueue(start);
            unvisited.Remove(start);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                component.Add(current);

                // Check 4-way neighbors
                Vector2Int[] neighbors = {
                    new Vector2Int(current.x + 1, current.y),
                    new Vector2Int(current.x - 1, current.y),
                    new Vector2Int(current.x, current.y + 1),
                    new Vector2Int(current.x, current.y - 1)
                };

                foreach (var nb in neighbors)
                {
                    if (unvisited.Contains(nb))
                    {
                        // Check if it has the same type
                        if (Grid.Grid[nb.y, nb.x] == targetType)
                        {
                            queue.Enqueue(nb);
                            unvisited.Remove(nb);
                        }
                    }
                }
            }

            if (component.Count >= 4)
            {
                return true;
            }
        }

        return false;
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
            _cachedAttackConfig = CreatureAttackConfig.Load();
            _attackConfigLoaded = true;
        }
        if (_cachedAttackConfig != null) return _cachedAttackConfig.GetRule(type);
        // Hard-coded fallback
        switch (type)
        {
            case GemType.Fire:     return new AttackRule { Type = type, StonesRequired = 6, Damage = 15, AttackName = "Ember",       EffectDescription = "Deals 15 dmg" };
            case GemType.Water:    return new AttackRule { Type = type, StonesRequired = 4, Damage = 10, AttackName = "Water Gun",   EffectDescription = "10 dmg + remove 3 stones" };
            case GemType.Nature:   return new AttackRule { Type = type, StonesRequired = 5, Damage = 15, AttackName = "Mega Drain",  EffectDescription = "Heals HP & deals equal damage to opponent" };
            case GemType.Electric: return new AttackRule { Type = type, StonesRequired = 5, Damage = 10, AttackName = "Spark",       EffectDescription = "10 dmg + clear row" };
            case GemType.Psychic:  return new AttackRule { Type = type, StonesRequired = 5, Damage = 8,  AttackName = "Psybeam",     EffectDescription = "8 dmg + 8 shield" };
            case GemType.Healing:  return new AttackRule { Type = type, StonesRequired = 4, Damage = 20, AttackName = "Soft-Boiled", EffectDescription = "Heals HP & deals equal damage to opponent" };
            default:               return new AttackRule { Type = type, StonesRequired = 5, Damage = 5,  AttackName = "Tackle",      EffectDescription = "5 dmg" };
        }
    }

    public bool TryManualEvolve(int playerIdx, CreatureState poke)
    {
        if (IsProcessing || IsWaitingForEvolutionSelection) return false;
        PlayerState player = Players[playerIdx];
        if (player.EvolutionStones < PlayerState.EvolutionRequired) return false;
        if (player.MovesRemaining <= 0) return false;

        int oldVal = poke.BaseValue + poke.EvolutionDamageBonus;
        poke.IsEvolved = true;
        poke.EvolutionDamageBonus += 5;
        int newVal = poke.BaseValue + poke.EvolutionDamageBonus;

        player.EvolutionStones = 0;
        player.MovesRemaining--;
        OnMovesChanged?.Invoke();
        OnEvolved?.Invoke(playerIdx);
        OnEvolutionStonesChanged?.Invoke(playerIdx);

        if (OnShowEvolutionSuccessPopup != null)
        {
            OnShowEvolutionSuccessPopup.Invoke(poke, oldVal, newVal, () => { });
        }
        
        EndPlayerMove();
        return true;
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

    /// <summary>
    /// Returns the GemType associated with a Creature by name.
    /// Used when constructing CreatureState from a saved team.
    /// </summary>
    public static GemType GetTypeForCreature(string name)
    {
        switch (name)
        {
            // Fire
            case "Ember Dragon":
            case "Lava Hound":        return GemType.Fire;
            
            // Water
            case "Tide Serpent":
            case "Coral Guardian":    return GemType.Water;
            
            // Nature
            case "Thorn Wolf":
            case "Ancient Treant":    return GemType.Nature;
            
            // Electric
            case "Thunder Roc":
            case "Storm Drake":       return GemType.Electric;
            
            // Psychic
            case "Astral Fox":
            case "Void Raven":        return GemType.Psychic;
            
            // Healing
            case "Celestial Unicorn":
            case "Light Phoenix":     return GemType.Healing;
            
            default:                  return GemType.Fire;
        }
    }
}
