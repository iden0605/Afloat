using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that drives the wave loop: spawns enemies from WaveData assets,
/// tracks how many are alive, and optionally auto-starts the next wave.
///
/// ── Scene setup ──
///   1. Add WaveManager to a scene GameObject.
///   2. Assign WaveData assets to the Waves list in the Inspector (in order).
///   3. Enemies always spawn at waypoints[0] from WaypointManager.
///   4. Call StartNextWave() from a UI button, or enable startOnAwake for automatic start.
///
/// ── Adding a new wave ──
///   1. Right-click in Project → Create → Click n Claw → Wave Data.
///   2. Add WaveEntry rows: pick an EnemyData, set count and spawnInterval.
///   3. Drag the new WaveData asset into WaveManager.waves.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    /// <summary>Fired when a wave is fully cleared (all enemies defeated or escaped).
    /// Passes the 0-based wave index that just finished.</summary>
    public static event Action<int> WaveCleared;

    /// <summary>Fired when a new wave begins spawning. Passes the 0-based wave index.</summary>
    public static event Action<int> WaveStarted;

    /// <summary>Fired once when the final wave is cleared and the player has won.</summary>
    public static event Action OnGameWon;

    [Header("Waves — add WaveData assets in order")]
    [SerializeField] private List<WaveData> waves = new();

    [Header("Options")]
    [Tooltip("Call StartNextWave() automatically when the scene loads.")]
    [SerializeField] private bool startOnAwake = false;

    [Tooltip("Seconds to count down between waves when no preWaveDelay is set on the next WaveData.")]
    [SerializeField] private float defaultCountdownDuration = 5f;

    // ── Internal ──────────────────────────────────────────────────────────────
    private Coroutine _countdownCoroutine;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Zero-based index of the wave currently running (or most recently run).</summary>
    public int  CurrentWaveIndex { get; private set; } = -1;

    /// <summary>True while enemies are still being spawned in this wave.</summary>
    public bool IsSpawning       { get; private set; } = false;

    /// <summary>True while at least one enemy from the current wave is still alive.</summary>
    public bool IsWaveActive     => IsSpawning || EnemiesAlive > 0;

    /// <summary>True when double-speed mode is enabled (Time.timeScale = 2).</summary>
    public bool IsDoubleSpeed    { get; private set; } = false;

    /// <summary>Number of enemies currently alive on the path.</summary>
    public int  EnemiesAlive     { get; private set; } = 0;

    /// <summary>True after the last wave has been cleared.</summary>
    public bool AllWavesComplete => CurrentWaveIndex >= waves.Count - 1 && !IsWaveActive;

    /// <summary>True while counting down to the next wave.</summary>
    public bool  IsCountingDown     { get; private set; } = false;

    /// <summary>Seconds remaining until the next wave starts. Only valid while IsCountingDown.</summary>
    public float CountdownRemaining { get; private set; } = 0f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (startOnAwake)
            StartNextWave();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Begins the next wave. Safe to call from a UI button.
    /// Does nothing if a wave is already active.
    /// </summary>
    public void StartNextWave()
    {
        if (IsWaveActive)
        {
            Debug.LogWarning("[WaveManager] Cannot start next wave — current wave is still active.");
            return;
        }

        int nextIndex = CurrentWaveIndex + 1;
        if (nextIndex >= waves.Count)
        {
            Debug.Log("[WaveManager] All waves complete!");
            return;
        }

        CurrentWaveIndex = nextIndex;
        StartCoroutine(RunWave(waves[CurrentWaveIndex]));
    }

    /// <summary>
    /// Enables or disables double-speed mode by adjusting Time.timeScale.
    /// </summary>
    public void SetDoubleSpeed(bool on)
    {
        IsDoubleSpeed  = on;
        Time.timeScale = on ? 2f : 1f;
    }

    /// <summary>
    /// Cancels the between-wave countdown and starts the next wave immediately.
    /// Does nothing if no countdown is running.
    /// </summary>
    public void SkipCountdown()
    {
        if (!IsCountingDown) return;
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        _countdownCoroutine = null;
        CountdownRemaining  = 0f;
        IsCountingDown      = false;
        StartNextWave();
    }

    /// <summary>
    /// Called by EnemyInstance.Die() when an enemy is defeated by troops.
    /// </summary>
    public void OnEnemyDefeated(EnemyInstance enemy)
    {
        DecrementAlive();
    }

    /// <summary>
    /// Called by EnemyMovement.ReachEndOfPath() when an enemy slips past all defences.
    /// </summary>
    public void OnEnemyEscaped(EnemyInstance enemy)
    {
        DecrementAlive();
        PlayerHealthManager.Instance?.LoseHealth((int)enemy.CurrentHealth);
        Debug.Log($"[WaveManager] Enemy escaped! ({Data(enemy)} reached the end of the path)");
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    IEnumerator RunWave(WaveData wave)
    {
        IsSpawning = true;
        WaveStarted?.Invoke(CurrentWaveIndex);
        AudioManager.Instance?.PlaySFX(AudioManager.Instance?.sfxWaveStart);
        Debug.Log($"[WaveManager] ── Wave {CurrentWaveIndex + 1} starting ──");

        foreach (var entry in wave.entries)
        {
            if (entry.enemyData == null || entry.enemyData.prefab == null)
            {
                Debug.LogWarning("[WaveManager] WaveEntry has no EnemyData or prefab — skipping.");
                continue;
            }

            // Optional delay before this group starts
            if (entry.groupDelay > 0f)
                yield return new WaitForSeconds(entry.groupDelay);

            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry.enemyData);

                // Wait between spawns, but not after the last one in the group
                if (i < entry.count - 1 && entry.spawnInterval > 0f)
                    yield return new WaitForSeconds(entry.spawnInterval);
            }
        }

        IsSpawning = false;
        Debug.Log($"[WaveManager] Wave {CurrentWaveIndex + 1} spawning done — {EnemiesAlive} enemies alive.");

        // If all enemies died before spawning finished (unlikely but possible with fast attackers),
        // check for wave clear right now.
        if (EnemiesAlive == 0)
            OnWaveCleared();
    }

    void SpawnEnemy(EnemyData data)
    {
        Vector3 spawnPos = Vector3.zero;
        if (WaypointManager.Instance != null && WaypointManager.Instance.waypoints.Length > 0)
            spawnPos = WaypointManager.Instance.waypoints[0].position;

        var go = Instantiate(data.prefab, spawnPos, Quaternion.identity);

        if (go.TryGetComponent<EnemyInstance>(out var inst))
            inst.Initialize(data);
        else
            Debug.LogWarning($"[WaveManager] Prefab '{data.prefab.name}' is missing an EnemyInstance component. " +
                             "Add EnemyInstance and EnemyHealthBar to the base Enemy prefab.");

        EnemiesAlive++;
    }

    void DecrementAlive()
    {
        EnemiesAlive = Mathf.Max(0, EnemiesAlive - 1);
        if (EnemiesAlive == 0 && !IsSpawning)
            OnWaveCleared();
    }

    void OnWaveCleared()
    {
        Debug.Log($"[WaveManager] ── Wave {CurrentWaveIndex + 1} cleared! ──");
        WaveCleared?.Invoke(CurrentWaveIndex);

        int nextIndex = CurrentWaveIndex + 1;
        if (nextIndex >= waves.Count)
        {
            Debug.Log("[WaveManager] All waves cleared — player wins!");
            OnGameWon?.Invoke();
            return;
        }

        float delay = waves[nextIndex].preWaveDelay > 0f
            ? waves[nextIndex].preWaveDelay
            : defaultCountdownDuration;

        _countdownCoroutine = StartCoroutine(CountdownToNextWave(delay));
    }

    IEnumerator CountdownToNextWave(float duration)
    {
        IsCountingDown     = true;
        CountdownRemaining = duration;

        // Yielding per-frame lets milestone popups (timeScale = 0) freeze the
        // countdown naturally — Time.deltaTime is 0 while paused.
        while (CountdownRemaining > 0f)
        {
            yield return null;
            CountdownRemaining -= Time.deltaTime;
        }

        CountdownRemaining = 0f;
        IsCountingDown     = false;
        StartNextWave();
    }

    static string Data(EnemyInstance e) =>
        e != null && e.Data != null ? e.Data.enemyName : "unknown";
}
