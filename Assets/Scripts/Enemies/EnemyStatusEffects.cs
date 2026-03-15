using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages stackable DoT (burn / poison) and timed crowd-control (freeze / stun) on a single enemy.
/// Added automatically when a troop's attack lands the relevant effect.
///
/// Freeze slows EnemyMovement.speedMultiplier to freezeSlowFactor for the duration.
/// Stun sets speedMultiplier to 0 for the duration.
/// Burn / Poison deal periodic damage until the enemy dies.
/// </summary>
public class EnemyStatusEffects : MonoBehaviour
{
    // ── DoT stacks ────────────────────────────────────────────────────────────

    private readonly List<BurnStack>   _burnStacks   = new();
    private readonly List<PoisonStack> _poisonStacks = new();

    // ── CC timers ─────────────────────────────────────────────────────────────

    private float _freezeEndTime    = 0f;
    private float _freezeSlowFactor = 1f; // lowest multiplier currently applied
    private float _stunEndTime      = 0f;

    // ── Component refs ────────────────────────────────────────────────────────

    private EnemyInstance _enemy;
    private EnemyMovement _movement;

    void Awake()
    {
        _enemy    = GetComponent<EnemyInstance>();
        _movement = GetComponent<EnemyMovement>();
    }

    // ── Public properties (read by EnemyVisualEffects) ────────────────────────

    public bool HasBurnStacks   => _burnStacks.Count   > 0;
    public bool HasPoisonStacks => _poisonStacks.Count > 0;
    public bool IsFrozen        => Time.time < _freezeEndTime;
    public bool IsStunned       => Time.time < _stunEndTime;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Adds a new independent burn stack to this enemy.</summary>
    public static void ApplyBurn(GameObject target, float damagePerTick, float tickInterval)
    {
        var fx = target.GetComponent<EnemyStatusEffects>()
                 ?? target.AddComponent<EnemyStatusEffects>();
        fx._burnStacks.Add(new BurnStack(damagePerTick, tickInterval));
        EnsureVisuals(target);
    }

    /// <summary>Adds a new independent poison stack to this enemy.</summary>
    public static void ApplyPoison(GameObject target, float damagePerTick, float tickInterval)
    {
        var fx = target.GetComponent<EnemyStatusEffects>()
                 ?? target.AddComponent<EnemyStatusEffects>();
        fx._poisonStacks.Add(new PoisonStack(damagePerTick, tickInterval));
        EnsureVisuals(target);
    }

    /// <summary>Applies or refreshes a freeze (slow) effect.</summary>
    public static void ApplyFreeze(GameObject target, float duration, float slowFactor)
    {
        var fx = target.GetComponent<EnemyStatusEffects>()
                 ?? target.AddComponent<EnemyStatusEffects>();
        fx._freezeEndTime    = Mathf.Max(fx._freezeEndTime, Time.time + duration);
        fx._freezeSlowFactor = Mathf.Min(fx._freezeSlowFactor, slowFactor); // keep strongest slow

        var vis = EnsureVisuals(target);
        vis?.ApplyFreeze(duration);
    }

    /// <summary>Applies or refreshes a stun (full stop) effect.</summary>
    public static void ApplyStun(GameObject target, float duration)
    {
        var fx = target.GetComponent<EnemyStatusEffects>()
                 ?? target.AddComponent<EnemyStatusEffects>();
        fx._stunEndTime = Mathf.Max(fx._stunEndTime, Time.time + duration);

        var vis = EnsureVisuals(target);
        vis?.ApplyStun(duration);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            Destroy(this);
            return;
        }

        TickBurn();
        TickPoison();
        UpdateSpeedMultiplier();
    }

    private void TickBurn()
    {
        for (int i = _burnStacks.Count - 1; i >= 0; i--)
        {
            var s = _burnStacks[i];
            s.timer -= Time.deltaTime;
            if (s.timer <= 0f)
            {
                _enemy.TakeDamage(s.damage, AttackType.Generic);
                s.timer += s.interval;
            }
            _burnStacks[i] = s;
        }
    }

    private void TickPoison()
    {
        for (int i = _poisonStacks.Count - 1; i >= 0; i--)
        {
            var s = _poisonStacks[i];
            s.timer -= Time.deltaTime;
            if (s.timer <= 0f)
            {
                _enemy.TakeDamage(s.damage, AttackType.Generic);
                s.timer += s.interval;
            }
            _poisonStacks[i] = s;
        }
    }

    private void UpdateSpeedMultiplier()
    {
        if (_movement == null) return;

        bool stunned = Time.time < _stunEndTime;
        bool frozen  = Time.time < _freezeEndTime;

        if (stunned)
            _movement.speedMultiplier = 0f;
        else if (frozen)
            _movement.speedMultiplier = _freezeSlowFactor;
        else
        {
            _movement.speedMultiplier = 1f;
            // Reset slow factor when freeze fully expires so next freeze uses inspector value
            _freezeSlowFactor = 1f;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EnemyVisualEffects EnsureVisuals(GameObject target)
    {
        return target.GetComponent<EnemyVisualEffects>()
               ?? target.AddComponent<EnemyVisualEffects>();
    }

    // ── Stack structs ─────────────────────────────────────────────────────────

    private struct BurnStack
    {
        public float damage;
        public float interval;
        public float timer;
        public BurnStack(float d, float i) { damage = d; interval = i; timer = i; }
    }

    private struct PoisonStack
    {
        public float damage;
        public float interval;
        public float timer;
        public PoisonStack(float d, float i) { damage = d; interval = i; timer = i; }
    }
}
