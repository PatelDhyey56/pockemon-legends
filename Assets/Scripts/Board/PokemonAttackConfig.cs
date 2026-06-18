// PokemonAttackConfig.cs
// ScriptableObject that describes the stone-collection rules for each Pokemon type.
// Attack limit = how many matching stones must be collected before the Pokemon can fire.
// If RequiresDamage is true the ability ALWAYS deals damage to the opponent.
// Resources.Load<PokemonAttackConfig>("PokemonAttackConfig") in BoardManager.

using UnityEngine;

[System.Serializable]
public class AttackRule
{
    public GemType Type;

    [Tooltip("Number of matching stones the player must collect to charge this attack.")]
    public int StonesRequired;

    [Tooltip("Damage dealt to the opponent when the attack fires. 0 = no damage (e.g. pure heal / board effect).")]
    public int Damage;

    [Tooltip("Short human-readable description shown in the charge-bar tooltip.")]
    public string AttackName;

    [Tooltip("Short description of the secondary effect.")]
    public string EffectDescription;
}

[CreateAssetMenu(fileName = "PokemonAttackConfig", menuName = "Pokemon/Attack Config")]
public class PokemonAttackConfig : ScriptableObject
{
    public AttackRule[] Rules;

    private static PokemonAttackConfig _cache;

    /// <summary>
    /// Returns the cached instance loaded from Resources, or null if not found.
    /// </summary>
    public static PokemonAttackConfig Load()
    {
        if (_cache == null)
            _cache = Resources.Load<PokemonAttackConfig>("PokemonAttackConfig");
        return _cache;
    }

    /// <summary>
    /// Returns the AttackRule for the given GemType, or a safe default.
    /// </summary>
    public AttackRule GetRule(GemType type)
    {
        if (Rules == null) return MakeDefault(type);
        foreach (var r in Rules)
            if (r.Type == type) return r;
        return MakeDefault(type);
    }

    private static AttackRule MakeDefault(GemType type)
    {
        // Fallback hard-coded defaults (mirrors BoardManager.GetMaxEnergyForType)
        return type switch
        {
            GemType.Fire     => new AttackRule { Type = type, StonesRequired = 6, Damage = 15, AttackName = "Ember",       EffectDescription = "Deals 15 damage" },
            GemType.Water    => new AttackRule { Type = type, StonesRequired = 4, Damage = 10, AttackName = "Water Gun",   EffectDescription = "Deals 10 dmg & removes 3 random stones" },
            GemType.Nature   => new AttackRule { Type = type, StonesRequired = 5, Damage = 0,  AttackName = "Mega Drain",  EffectDescription = "Heals 15 HP" },
            GemType.Electric => new AttackRule { Type = type, StonesRequired = 5, Damage = 10, AttackName = "Spark",       EffectDescription = "Deals 10 dmg & clears a row" },
            GemType.Psychic  => new AttackRule { Type = type, StonesRequired = 5, Damage = 8,  AttackName = "Psybeam",     EffectDescription = "Deals 8 dmg & gains 8 shield" },
            GemType.Healing  => new AttackRule { Type = type, StonesRequired = 4, Damage = 0,  AttackName = "Soft-Boiled", EffectDescription = "Heals 20 HP" },
            _                => new AttackRule { Type = type, StonesRequired = 5, Damage = 5,  AttackName = "Tackle",      EffectDescription = "Deals 5 damage" }
        };
    }
}
