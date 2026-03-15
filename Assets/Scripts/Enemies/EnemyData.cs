using UnityEngine;

/// <summary>
/// Special behaviours that modify how an enemy takes damage or moves.
/// </summary>
public enum EnemyEffectType
{
    None,
    ImmuneMelee,     // e.g. Plastic Bag: melee attacks (Ant, Mantis) deal no damage
    DamageReduction, // e.g. Armoured Mosquito: all damage reduced by effectValue (0–1 fraction)
    SpeedBurst,      // e.g. Baby Mosquito: speed doubles when health first drops below 50%
}

/// <summary>
/// Create one EnemyData asset per enemy type via:
/// Right-click in Project → Create → Click n Claw → Enemy Data
///
/// Each enemy prefab variant of the base Enemy prefab should have one corresponding asset.
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
    [Tooltip("Modifier used by certain effects.\n" +
             "• DamageReduction: fraction of damage blocked (0.5 = 50% reduction).")]
    public float effectValue = 0f;
}
