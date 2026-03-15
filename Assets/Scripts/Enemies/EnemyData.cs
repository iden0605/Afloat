using UnityEngine;

// ── Enemy effect types ────────────────────────────────────────────────────────

public enum EnemyEffectType
{
    None,
    ImmuneMelee,        // Immune to Melee attacks
    ImmuneRanged,       // Immune to Ranged attacks
    DamageReduction,    // effectValue: 0–1 fraction of each hit blocked
    MaxDamagePerHit,    // effectValue: maximum damage allowed per single hit
    DodgeChance,        // effectValue: 0–1 probability to fully dodge any attack
    SpeedBurst,         // effectValue: HP threshold — doubles speed once when health drops to/below it
                        //   (effectValue = 0 → defaults to 50% of max health)
    SpeedDoubleOnHit,   // Doubles movement speed the first time it is hit
    SpawnOnDeath,       // Spawns spawnEnemyData.prefab × spawnCount when killed
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

    [Tooltip("Effect-specific value:\n" +
             "• DamageReduction: fraction blocked (0–1, e.g. 0.3 = 30%)\n" +
             "• MaxDamagePerHit: max damage allowed per hit (e.g. 5)\n" +
             "• DodgeChance: 0–1 dodge probability (e.g. 0.3 = 30%)\n" +
             "• SpeedBurst: HP threshold that triggers the speed double (0 = use 50% of max health)")]
    public float effectValue = 0f;

    [Header("Spawn on Death  (SpawnOnDeath effect only)")]
    [Tooltip("The EnemyData whose prefab is spawned when this enemy dies.")]
    public EnemyData spawnEnemyData;
    [Tooltip("How many enemies to spawn on death.")]
    public int spawnCount = 1;
}
