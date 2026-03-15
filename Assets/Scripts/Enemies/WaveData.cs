using UnityEngine;

/// <summary>
/// One group of enemies within a wave.
/// Groups spawn sequentially: the next group begins after groupDelay seconds
/// following the last enemy of the previous group.
/// </summary>
[System.Serializable]
public class WaveEntry
{
    [Tooltip("Which enemy type to spawn in this group.")]
    public EnemyData enemyData;

    [Tooltip("How many enemies to spawn in this group.")]
    [Min(1)] public int count = 1;

    [Tooltip("Seconds between spawning each enemy in this group.")]
    [Min(0f)] public float spawnInterval = 1.0f;

    [Tooltip("Extra seconds to wait before this group starts, after the previous group's last enemy has spawned.")]
    [Min(0f)] public float groupDelay = 0f;
}

/// <summary>
/// Defines all the enemies in a single wave.
/// Create one asset per wave via:
/// Right-click in Project → Create → Click n Claw → Wave Data
///
/// Assign your WaveData assets to WaveManager.waves in order.
/// </summary>
[CreateAssetMenu(fileName = "NewWaveData", menuName = "Click n Claw/Wave Data")]
public class WaveData : ScriptableObject
{
    [Tooltip("Groups of enemies to spawn, processed in order from top to bottom.")]
    public WaveEntry[] entries;

    [Tooltip("Seconds to wait after the previous wave is fully cleared before auto-starting this wave " +
             "(only used when WaveManager.autoPlayBetweenWaves is true).")]
    [Min(0f)] public float preWaveDelay = 3f;
}
