// CreatureAttackConfig.cs
// ScriptableObject that describes the stone-collection rules for each Creature type.
// Attack limit = how many matching stones must be collected before the Creature can fire.
// If RequiresDamage is true the ability ALWAYS deals damage to the opponent.
// Resources.Load<CreatureAttackConfig>("CreatureAttackConfig") in BoardManager.

using UnityEngine;

[System.Serializable]
public class AttackRule
{
    public GemType Type;

    [Tooltip("Number of matching gems the player must collect to charge this attack.")]
    public int StonesRequired;

    [Tooltip("Damage dealt to the opponent when the attack fires. 0 = no damage (e.g. pure heal / board effect).")]
    public int Damage;

    [Tooltip("Short human-readable description shown in the charge-bar tooltip.")]
    public string AttackName;

    [Tooltip("Short description of the secondary effect.")]
    public string EffectDescription;
}

[CreateAssetMenu(fileName = "CreatureAttackConfig", menuName = "Creature/Attack Config")]
public class CreatureAttackConfig : ScriptableObject
{
    public AttackRule[] Rules;

    private static CreatureAttackConfig _cache;

    /// <summary>
    /// Returns the cached instance loaded from Resources, or null if not found.
    /// </summary>
    public static CreatureAttackConfig Load()
    {
        if (_cache == null)
        {
            _cache = Resources.Load<CreatureAttackConfig>("CreatureAttackConfig");
            if (_cache == null)
            {
                _cache = ScriptableObject.CreateInstance<CreatureAttackConfig>();
            }
        }
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
            GemType.Fire     => new AttackRule { Type = type, StonesRequired = 6, Damage = 15, AttackName = "Ember Burst",  EffectDescription = "Deals 15 damage" },
            GemType.Water    => new AttackRule { Type = type, StonesRequired = 4, Damage = 10, AttackName = "Tidal Surge",  EffectDescription = "Deals 10 dmg & removes 3 random gems" },
            GemType.Nature   => new AttackRule { Type = type, StonesRequired = 5, Damage = 15, AttackName = "Thorn Strike", EffectDescription = "Heals HP & deals equal damage to opponent" },
            GemType.Electric => new AttackRule { Type = type, StonesRequired = 5, Damage = 10, AttackName = "Storm Bolt",   EffectDescription = "Deals 10 dmg & clears a row" },
            GemType.Psychic  => new AttackRule { Type = type, StonesRequired = 5, Damage = 8,  AttackName = "Mind Blast",   EffectDescription = "Deals 8 dmg & gains 8 shield" },
            GemType.Healing  => new AttackRule { Type = type, StonesRequired = 4, Damage = 20, AttackName = "Holy Light",   EffectDescription = "Heals HP & deals equal damage to opponent" },
            _                => new AttackRule { Type = type, StonesRequired = 5, Damage = 5,  AttackName = "Slash",        EffectDescription = "Deals 5 damage" }
        };
    }
}
