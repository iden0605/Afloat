using UnityEngine;

/// <summary>Where this troop type is allowed to be placed.</summary>
public enum PlacementType
{
    LandOnly,       // Centipede, Beetle, Praying Mantis
    WaterOnly,      // future water-only troops
    LandAndWater,   // Frog
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
/// Create one TroopData asset per troop type via:
/// Right-click in Project → Create → Click n Claw → Troop Data
/// </summary>
[CreateAssetMenu(fileName = "NewTroopData", menuName = "Click n Claw/Troop Data")]
public class TroopData : ScriptableObject
{
    [Header("Identity")]
    public string troopName;
    public Sprite portrait;
    public GameObject prefab;

    [Header("Placement")]
    public PlacementType placementType = PlacementType.LandOnly;

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
    public UpgradeTier[] upgrades;

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
