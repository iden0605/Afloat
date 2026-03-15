using UnityEngine;

/// <summary>
/// Classifies the origin of an attack so immunity/resistance effects can respond correctly.
/// Passed into EnemyInstance.TakeDamage from each attack script.
/// </summary>
public enum AttackType
{
    Generic, // default — bypasses all immunity checks
    Melee,   // Ant, Mantis — blocked by EnemyEffectType.ImmuneMelee
    Ranged,  // Frog tongue, Centipede acid
    Splash,  // Beetle shockwave
}

/// <summary>
/// Attach to the base Enemy prefab. Tracks health, applies special effects on hit, and handles death.
/// Initialized by WaveManager immediately after instantiating an enemy prefab.
///
/// Attack scripts should call EnemyMovement.TakeDamage() (pass-through), which delegates here.
/// </summary>
[RequireComponent(typeof(EnemyMovement))]
public class EnemyInstance : MonoBehaviour
{
    public EnemyData Data          { get; private set; }
    public float     CurrentHealth { get; private set; }
    public float     MaxHealth     { get; private set; }
    public bool      IsDead        { get; private set; }

    private EnemyHealthBar _healthBar;
    private EnemyMovement  _movement;
    private bool           _speedBurstTriggered;

    /// <summary>
    /// Called by WaveManager right after Instantiate. Sets all stats from the data asset.
    /// </summary>
    public void Initialize(EnemyData data)
    {
        Data                 = data;
        MaxHealth            = data.baseHealth;
        CurrentHealth        = data.baseHealth;
        IsDead               = false;
        _speedBurstTriggered = false;

        // Propagate speed to EnemyMovement so no separate speed field needs to be set per prefab
        _movement = GetComponent<EnemyMovement>();
        _movement.speed = data.speed;

        _healthBar = GetComponent<EnemyHealthBar>();
        _healthBar?.Initialize(MaxHealth);
    }

    /// <summary>
    /// Apply damage from a troop attack. The attackType allows effects like ImmuneMelee to filter.
    /// </summary>
    public void TakeDamage(float amount, AttackType attackType = AttackType.Generic)
    {
        if (IsDead) return;

        if (Data != null)
        {
            switch (Data.effectType)
            {
                case EnemyEffectType.ImmuneMelee when attackType == AttackType.Melee:
                    return; // completely immune — no damage, no feedback

                case EnemyEffectType.DamageReduction:
                    amount *= Mathf.Clamp01(1f - Data.effectValue);
                    break;
            }
        }

        ApplyRaw(amount);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ApplyRaw(float amount)
    {
        float prevHealth = CurrentHealth;
        CurrentHealth   -= amount;

        _healthBar?.SetFill(CurrentHealth / MaxHealth);

        // SpeedBurst: triggers once when health first crosses below 50%
        if (Data != null && Data.effectType == EnemyEffectType.SpeedBurst && !_speedBurstTriggered)
        {
            if (prevHealth > MaxHealth * 0.5f && CurrentHealth <= MaxHealth * 0.5f)
            {
                _speedBurstTriggered = true;
                if (_movement != null) _movement.speed *= 2f;
                Debug.Log($"[EnemyInstance] {Data.enemyName} triggered SpeedBurst!");
            }
        }

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        if (Data != null)
            GoldManager.Instance?.AddGold(Data.goldDrop);

        WaveManager.Instance?.OnEnemyDefeated(this);
        Destroy(gameObject);
    }
}
