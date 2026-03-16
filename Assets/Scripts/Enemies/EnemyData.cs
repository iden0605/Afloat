using UnityEngine;

// ── Enemy effect types ────────────────────────────────────────────────────────

public enum EnemyEffectType
{
    None,
    ImmuneMelee,            // Immune to Melee attacks
    ImmuneRanged,           // Immune to Ranged attacks
    DamageReduction,        // effectValue: 0–1 fraction of each hit blocked
    MaxDamagePerHit,        // effectValue: maximum damage allowed per single hit
    DodgeChance,            // effectValue: 0–1 probability to fully dodge any attack
    SpeedBurst,             // effectValue: HP threshold — doubles speed once when HP drops to/below it
                            //   (effectValue = 0 → defaults to 50% of max health)
    SpeedDoubleOnHit,       // Doubles movement speed the first time it is hit
    SpawnOnDeath,           // Spawns spawnEnemyData.prefab × spawnCount when killed
    ReactiveSpeedOnHit,     // On each hit: boosts speed by effectValue fraction for effectValue2 seconds
                            //   e.g. effectValue=0.6, effectValue2=2 → +60% speed for 2s (refreshes on every hit)
    DesperationDash,        // When HP drops to or below effectValue fraction of max HP:
                            //   permanently multiplies base speed by (1 + effectValue2)
                            //   e.g. effectValue=0.3, effectValue2=1.5 → at 30% HP gains +150% speed (×2.5)
    SpawnAtHPThresholds,    // Every time HP drops by effectValue, spawn one enemy at the current waypoint
                            //   Alternates between spawnEnemyData and spawnEnemyData2 on each threshold
}

// ── EnemyData ScriptableObject ────────────────────────────────────────────────

/// <summary>
/// Create one EnemyData asset per enemy type via:
/// Right-click in Project → Create → Click n Claw → Enemy Data
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Click n Claw/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;
    [TextArea(2, 4)]
    public string description;
    public Sprite portrait;
    [Tooltip("Must be a variant of the base Enemy prefab (has EnemyMovement + EnemyInstance + EnemyHealthBar).")]
    public GameObject prefab;

    [Header("Stats")]
    [Tooltip("Total hit points.")]
    public float baseHealth = 10f;
    [Tooltip("Movement speed in world units per second.")]
    public float speed = 3f;

    [Header("Economy")]
    [Tooltip("Gold awarded to the player when this enemy is defeated.")]
    public int goldDrop = 5;

    [Header("Special Effect")]
    public EnemyEffectType effectType = EnemyEffectType.None;

    [Tooltip("Primary effect value:\n" +
             "• DamageReduction: fraction blocked (0–1, e.g. 0.3 = 30%)\n" +
             "• MaxDamagePerHit: max damage per hit (e.g. 30)\n" +
             "• DodgeChance: 0–1 dodge probability (e.g. 0.25 = 25%)\n" +
             "• SpeedBurst: HP threshold that triggers speed double (0 = 50% of max HP)\n" +
             "• ReactiveSpeedOnHit: speed boost fraction (e.g. 0.6 = +60%)\n" +
             "• DesperationDash: HP fraction threshold (e.g. 0.3 = 30% HP)\n" +
             "• SpawnAtHPThresholds: HP interval between spawns (e.g. 500)")]
    public float effectValue = 0f;

    [Tooltip("Secondary effect value:\n" +
             "• ReactiveSpeedOnHit: duration of speed boost in seconds (e.g. 2)\n" +
             "• DesperationDash: speed multiplier added to base (e.g. 1.5 = +150%)")]
    public float effectValue2 = 0f;

    [Header("Spawn  (SpawnOnDeath / SpawnAtHPThresholds)")]
    [Tooltip("Primary spawn type — used by SpawnOnDeath and odd threshold hits (1st, 3rd, 5th…).")]
    public EnemyData spawnEnemyData;
    [Tooltip("Alternating spawn type for SpawnAtHPThresholds (even threshold hits: 2nd, 4th, 6th…).")]
    public EnemyData spawnEnemyData2;
    [Tooltip("How many enemies to spawn per event (SpawnOnDeath only).")]
    public int spawnCount = 1;
}
