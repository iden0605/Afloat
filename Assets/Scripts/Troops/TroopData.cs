using UnityEngine;

/// <summary>
/// One step in a troop's evolution chain.
/// Each entry in TroopData.evolutions defines a distinct evolved form with its own prefab.
/// </summary>
[System.Serializable]
public class EvolutionData
{
    public string evolutionName;
    [TextArea(1, 3)]
    public string description;

    [Header("Prefab")]
    [Tooltip("The prefab that replaces the current troop when this evolution triggers.")]
    public GameObject prefab;

    [Header("Requirement")]
    [Tooltip("Total upgrade tiers that must be purchased before this evolution unlocks.")]
    public int upgradesRequired = 1;

    [Header("Stat Boosts on Evolve")]
    public float attackBoost;
    public float attackSpeedBoost;
    public float rangeBoost;
}

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

public enum TroopEffectType
{
    None,
    DoubleGoldDrop,        // Anchovies: hit enemies drop double gold
    ConditionalAttackBuff, // Praying Mantis: attack → 3 when >1 enemy in range
    ConditionalSpeedBuff,  // Dragonfly: attackSpeed → 1.2 when only 1 enemy in range
    AllyProximityBuff,     // Ants: +0.5 attack per other Ant troop in range
}

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
    [Tooltip("If true, land troops can be placed on top of this power (e.g. Lily Pad)")]
    public bool isLandPlatform = false;

    [Header("Economy")]
    public int baseCost = 50;

    [Header("Combat Stats")]
    public float attack = 1f;
    public float attackSpeed = 1f;        // attacks per second (for future use)
    public float range = 1.5f;            // detection radius in world units
    public ProjectileType projectileType = ProjectileType.Single;
    public float splashRadius = 0.5f;     // only used when projectileType == Splash
    public TroopEffectType effectType = TroopEffectType.None;

    [Header("Upgrades — add one entry per upgrade tier")]
    [Tooltip("Each tier's attackDelta/attackSpeedDelta/rangeDelta are added to current stats on purchase.")]
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
    }
}
