using UnityEngine;

/// <summary>
/// Singleton that tracks the player's gold.
///
/// Subscribe to the static OnGoldChanged event to be notified whenever gold changes:
///   GoldManager.OnGoldChanged += myCallback;   // int param = new total
///   GoldManager.OnGoldChanged -= myCallback;   // always unsubscribe in OnDisable
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    /// <summary>Fired whenever gold is added or spent. Carries the new total.</summary>
    public static event System.Action<int> OnGoldChanged;

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

    void Start()
    {
        // Broadcast the starting value so any HUD that missed Awake is still correct
        OnGoldChanged?.Invoke(Gold);
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds gold to the balance (e.g. enemy defeated, troop sold).
    /// Always succeeds.
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    /// <summary>
    /// Deducts gold. Returns false without changing balance if the player cannot afford it.
    /// </summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (Gold < amount) return false;
        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    /// <summary>Returns true if the player can afford the given cost.</summary>
    public bool CanAfford(int amount) => amount <= 0 || Gold >= amount;
}
