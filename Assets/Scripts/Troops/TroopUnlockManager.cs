using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that tracks which troops are currently unlocked and fires an event
/// whenever the unlock set changes (so the sidebar can rebuild).
///
/// ── Scene setup ──
///   1. Add a GameObject "TroopUnlockManager" to the Main scene.
///   2. Attach this script to it.
///   3. Drag TroopData assets that should be available from Wave 1 into Starting Troops.
///   4. Drag all MilestoneData assets into Milestones (same list as MilestonePopupController).
/// </summary>
public class TroopUnlockManager : MonoBehaviour
{
    public static TroopUnlockManager Instance { get; private set; }

    /// <summary>Fired whenever the unlocked troop set changes (new troops become available).</summary>
    public static event Action OnUnlocksChanged;

    [Header("Debug")]
    [Tooltip("Unlock every troop instantly for testing. Has no effect in builds.")]
    [SerializeField] private bool unlockAllForTesting = false;

    [Header("Troops available from the very start of the game")]
    [SerializeField] private TroopData[] startingTroops = Array.Empty<TroopData>();

    [Header("Milestone definitions — same list as MilestonePopupController")]
    [SerializeField] private MilestoneData[] milestones = Array.Empty<MilestoneData>();

    private readonly HashSet<TroopData> _unlocked = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        _unlocked.Clear();
        foreach (var t in startingTroops)
            if (t != null) _unlocked.Add(t);

        WaveManager.WaveCleared += OnWaveCleared;
    }

    void OnDisable()
    {
        WaveManager.WaveCleared -= OnWaveCleared;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns true if the given troop is currently unlocked.</summary>
    public bool IsUnlocked(TroopData data)
    {
#if UNITY_EDITOR
        if (unlockAllForTesting) return data != null;
#endif
        return data != null && _unlocked.Contains(data);
    }

    // ── Editor hot-reload ─────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnValidate()
    {
        // Notify the sidebar to rebuild whenever the checkbox is toggled in the inspector
        if (Application.isPlaying)
            OnUnlocksChanged?.Invoke();
    }
#endif

    // ── Internal ──────────────────────────────────────────────────────────────

    void OnWaveCleared(int waveIndex)
    {
        bool changed = false;
        foreach (var ms in milestones)
        {
            if (ms == null || ms.triggerAfterWave != waveIndex) continue;
            foreach (var t in ms.unlockedTroops)
                if (t != null && _unlocked.Add(t)) changed = true;
        }
        if (changed) OnUnlocksChanged?.Invoke();
    }
}
