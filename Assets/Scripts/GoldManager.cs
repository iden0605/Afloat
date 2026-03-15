using UnityEngine;

/// <summary>
/// Singleton that tracks the player's gold.
/// Gold is earned by defeating enemies (via EnemyInstance.Die) and spent on
/// placing troops (connect to TroopDragController).
///
/// Add this component to a persistent scene GameObject.
/// Connect the current gold value to your UI by reading GoldManager.Instance.Gold.
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [Header("Economy")]
    [Tooltip("Starting gold at the beginning of the game.")]
    [SerializeField] private int startingGold = 100;

    /// <summary>Current gold balance.</summary>
    public int Gold { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Gold = startingGold;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Increases gold. Called by EnemyInstance when an enemy is defeated.</summary>
    public void AddGold(int amount)
    {
        Gold += amount;
        Debug.Log($"[GoldManager] +{amount} gold. Total: {Gold}");
        // TODO: fire an event / update UI here
    }

    /// <summary>
    /// Attempts to spend gold. Returns false (and does nothing) if the player
    /// cannot afford it.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (Gold < amount) return false;
        Gold -= amount;
        Debug.Log($"[GoldManager] -{amount} gold. Total: {Gold}");
        // TODO: fire an event / update UI here
        return true;
    }

    /// <summary>Returns true if the player can afford the given cost.</summary>
    public bool CanAfford(int amount) => Gold >= amount;
}
