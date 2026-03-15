using UnityEngine;

// ── Troop effect types ────────────────────────────────────────────────────────

public enum TroopEffectType
{
    None,

    // ── Debuffs (applied to hit enemies) ─────────────────────────────────────
    DoubleGoldDrop,         // Enemies hit drop X× gold on death
    BurnOnHit,              // Burns hit enemies; stackable DoT
    PoisonOnHit,            // Poisons hit enemies; stackable DoT (no splash)
    PoisonSplash,           // Attack splashes + applies poison DoT to the area
    FreezeOnHit,            // Slows hit enemy for X seconds
    StunOnHit,              // Stops hit enemy for X seconds

    // ── Conditional self-buffs ────────────────────────────────────────────────
    ConditionalAttackBuff,  // Sets ATK to X when >1 enemy in range
    ConditionalSpeedBuff,   // Sets SPD to X when only 1 enemy in range
    DoubleEveryFourth,      // Every 4th attack deals 2× damage
    RampingDoubleBuff,      // Each hit: ATK & SPD ×2, up to N stacks, each lasting X seconds

    // ── Proximity self-buffs ──────────────────────────────────────────────────
    AllyProximityBuff,      // +ATK per nearby ally of the same troop type
    AllySpeedBuff,          // +SPD per nearby ally of the same troop type
}

// ── Per-effect configuration ──────────────────────────────────────────────────

/// <summary>
/// Pairs an effect type with all of its configurable values.
/// Only the fields relevant to the chosen effectType matter — the rest are ignored.
/// </summary>
[System.Serializable]
public class TroopEffectConfig
{
    public TroopEffectType effectType = TroopEffectType.None;

    [Tooltip("DoubleGoldDrop: gold multiplier on enemy death (default 2).")]
    public float goldMultiplier = 2f;

    [Tooltip("ConditionalAttackBuff: attack value set when more than 1 enemy is in range.")]
    public float conditionalAttack = 3f;

    [Tooltip("ConditionalSpeedBuff: attack-speed value set when only 1 enemy is in range.")]
    public float conditionalSpeed = 1.2f;

    [Tooltip("AllyProximityBuff: ATK bonus per same-type ally in range.\n" +
             "AllySpeedBuff: SPD bonus per same-type ally in range.")]
    public float allyBonus = 0.5f;

    [Tooltip("BurnOnHit: seconds between burn ticks (default 0.5).\n" +
             "PoisonSplash: seconds between poison ticks (default 0.2).")]
    public float dotInterval = 0.5f;

    [Tooltip("RampingDoubleBuff: how many seconds each stack persists.")]
    public float rampingDuration = 2f;

    [Tooltip("RampingDoubleBuff: maximum number of stacks (damage multiplier = 2^stacks).")]
    public int rampingMaxStacks = 3;

    [Tooltip("FreezeOnHit: seconds the enemy is slowed on each hit.")]
    public float freezeDuration = 2f;

    [Tooltip("FreezeOnHit: speed multiplier while frozen (0.3 = 30% speed).")]
    public float freezeSlowFactor = 0.3f;

    [Tooltip("StunOnHit: seconds the enemy is fully stopped on each hit.")]
    public float stunDuration = 1f;
}

// ── Evolution data ────────────────────────────────────────────────────────────

/// <summary>One step in a troop's evolution chain.</summary>
[System.Serializable]
public class EvolutionData
{
    public string evolutionName;
    [TextArea(1, 3)]
    public string description;

    [Header("Visuals")]
    [Tooltip("Portrait sprite shown in the UI for this evolved form.")]
    public Sprite portrait;

    [Header("Prefab")]
    [Tooltip("The prefab that replaces the current troop when this evolution triggers.")]
    public GameObject prefab;

    [Header("Requirement")]
    [Tooltip("Total upgrade tiers that must be purchased before this evolution unlocks.")]
    public int upgradesRequired = 1;

    [Header("Economy")]
    [Tooltip("Gold cost to trigger this evolution.")]
    public int evolutionCost = 100;

    [Header("Stat Boosts on Evolve")]
    public float attackBoost;
    public float attackSpeedBoost;
    public float rangeBoost;

    [Header("Effects — active while at this evolution (replaces previous evolution's effects)")]
    [Tooltip("These effects are active when this is the current evolution tier. " +
             "They replace the previous evolution's effects. The base troop effect always persists.")]
    public TroopEffectConfig[] effects = System.Array.Empty<TroopEffectConfig>();
}

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Where this troop/power is allowed to be placed.</summary>
public enum PlacementType
{
    LandOnly,       // Centipede, Beetle, Praying Mantis
    WaterOnly,      // Lily Pad and future water-only units
    LandAndWater,   // Frog
}

/// <summary>Which sidebar section this item belongs to.</summary>
public enum TroopCategory
{
    Troop,  // combat unit — tracked in TroopManager.PlacedTroops
    Power,  // terrain / utility — tracked in TroopManager.PlacedPowers
}

public enum ProjectileType { Single, Splash }

// ── TroopData ScriptableObject ────────────────────────────────────────────────

/// <summary>
/// Create one TroopData asset per troop/power type via:
/// Right-click in Project → Create → Click n Claw → Troop Data
/// </summary>
[CreateAssetMenu(fileName = "NewTroopData", menuName = "Click n Claw/Troop Data")]
public class TroopData : ScriptableObject
{
    [Header("Identity")]
    public string troopName;
    [TextArea(2, 4)]
    public string description;
    public Sprite portrait;
    public GameObject prefab;

    [Header("Category")]
    public TroopCategory category = TroopCategory.Troop;

    [Header("Placement")]
    public PlacementType placementType = PlacementType.LandOnly;
    [Tooltip("If true, land troops can be placed on top of this power (e.g. Lily Pad).")]
    public bool isLandPlatform = false;

    [Header("Economy")]
    public int baseCost = 50;

    [Header("Combat Stats")]
    public float attack      = 1f;
    public float attackSpeed = 1f;
    public float range       = 1.5f;

    [Tooltip("Melee = close combat (blocked by ImmuneMelee).\n" +
             "Ranged = projectile (blocked by ImmuneRanged).\n" +
             "Splash = AOE (not blocked by either immunity).")]
    public AttackType attackType = AttackType.Melee;

    public ProjectileType projectileType = ProjectileType.Single;
    [Tooltip("Radius of splash damage/effects (world units). Used when projectileType is Splash or by PoisonSplash effect.")]
    public float splashRadius = 0.5f;

    [Header("Base Effect — always active at every upgrade and evolution level")]
    [Tooltip("This single effect persists regardless of upgrades or evolutions.")]
    public TroopEffectConfig baseEffect = new TroopEffectConfig();

    [Header("Upgrades — add one entry per upgrade tier")]
    [Tooltip("Each tier's effects REPLACE the previous tier's effects (base effect always persists).")]
    public UpgradeTier[] upgrades;

    [Header("Evolutions — add one entry per evolved form in order")]
    [Tooltip("Each entry is one step in the evolution chain. Leave empty for no evolutions.")]
    public EvolutionData[] evolutions;

    /// <summary>True if this troop has at least one evolution defined.</summary>
    public bool HasEvolutions => evolutions != null && evolutions.Length > 0;

    [System.Serializable]
    public struct UpgradeTier
    {
        public string description;
        public int    cost;
        public float  attackDelta;
        public float  attackSpeedDelta;
        public float  rangeDelta;

        [Tooltip("Effects active when this is the highest purchased tier. " +
                 "Replaces all previous tiers' effects. Base effect always persists.")]
        public TroopEffectConfig[] effects;
    }
}
