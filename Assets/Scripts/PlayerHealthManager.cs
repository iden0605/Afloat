using System;
using UnityEngine;

/// <summary>
/// Singleton that tracks player health.
/// Enemies deal damage equal to their current HP when they escape the path.
///
/// ── Scene setup ──
///   1. Add PlayerHealthManager to a scene GameObject.
///   2. Adjust startingHealth in the Inspector if needed (default 100).
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int startingHealth = 100;

    // ── State ─────────────────────────────────────────────────────────────────

    /// <summary>Current player health. Never goes below 0.</summary>
    public int CurrentHealth { get; private set; }

    /// <summary>True once health has reached zero.</summary>
    public bool IsGameOver { get; private set; } = false;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Fired whenever CurrentHealth changes (subscribe to update UI).</summary>
    public static event Action OnHealthChanged;

    /// <summary>Fired once when health reaches zero.</summary>
    public static event Action OnGameOver;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        CurrentHealth = startingHealth;
        OnHealthChanged?.Invoke();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reduce player health by <paramref name="amount"/>.
    /// Clamps to 0 and triggers game over if health reaches zero.
    /// Does nothing if the game is already over.
    /// </summary>
    public void LoseHealth(int amount)
    {
        if (IsGameOver) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke();
        AudioManager.Instance?.PlaySFX(AudioManager.Instance?.sfxPlayerHpLoss);

        Debug.Log($"[PlayerHealthManager] Player took {amount} damage — HP: {CurrentHealth}/{startingHealth}");

        if (CurrentHealth <= 0)
        {
            IsGameOver = true;
            Debug.Log("[PlayerHealthManager] Game Over!");
            OnGameOver?.Invoke();
        }
    }
}
