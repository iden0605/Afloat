using UnityEngine;

/// <summary>
/// Classifies the origin of an attack so immunity/resistance effects can respond correctly.
/// Passed into EnemyInstance.TakeDamage from each attack script (via TroopInstance.DealDamage).
/// </summary>
public enum AttackType
{
    Generic, // default — bypasses all immunity checks
    Melee,   // Ant, Mantis, Worm — blocked by EnemyEffectType.ImmuneMelee
    Ranged,  // Frog tongue, Centipede acid — blocked by EnemyEffectType.ImmuneRanged
    Splash,  // Beetle shockwave — not blocked by either immunity
}

/// <summary>
/// Attach to the base Enemy prefab. Tracks health, applies special effects on hit, and handles death.
/// Initialized by WaveManager immediately after instantiating an enemy prefab.
///
/// Attack scripts should call TroopInstance.DealDamage() which delegates here.
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
    private EnemyHitFlash  _hitFlash;
    private HitStop        _hitStop;

    // SpeedBurst: one-time trigger
    private bool _speedBurstTriggered;
    // SpeedDoubleOnHit: one-time trigger
    private bool _hitSpeedDoubled;
    // ReactiveSpeedOnHit: refreshable timed boost
    private float _baseSpeed;
    private float _reactiveSpeedTimer;
    // DesperationDash: one-time permanent boost
    private bool _desperationTriggered;
    // SpawnAtHPThresholds: tracks how many threshold spawns have fired
    private int _thresholdSpawnCount;

    // DoubleGoldDrop: accumulate multiplier from attacking troops
    private float _goldMultiplier = 1f;

    /// <summary>True once any troop has marked this enemy for bonus gold on death.</summary>
    public bool HasDoubleGold => _goldMultiplier > 1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Initialize(EnemyData data)
    {
        Data                 = data;
        MaxHealth            = data.baseHealth;
        CurrentHealth        = data.baseHealth;
        IsDead               = false;
        _speedBurstTriggered = false;
        _hitSpeedDoubled     = false;
        _goldMultiplier      = 1f;

        _movement = GetComponent<EnemyMovement>();
        _movement.speed = data.speed;
        _baseSpeed = data.speed;

        _speedBurstTriggered  = false;
        _hitSpeedDoubled      = false;
        _reactiveSpeedTimer   = 0f;
        _desperationTriggered = false;
        _thresholdSpawnCount  = 0;

        _healthBar = GetComponent<EnemyHealthBar>();
        _healthBar?.Initialize(MaxHealth);

        _hitFlash = GetComponent<EnemyHitFlash>();
        _hitStop  = GetComponent<HitStop>();

        if (GetComponent<SpriteDepthEffect>() == null && GetComponent<SpriteRenderer>() != null)
            gameObject.AddComponent<SpriteDepthEffect>();
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        // ReactiveSpeedOnHit: restore base speed when the timed boost expires
        if (_reactiveSpeedTimer > 0f)
        {
            _reactiveSpeedTimer -= Time.deltaTime;
            if (_reactiveSpeedTimer <= 0f && _movement != null)
                _movement.speed = _baseSpeed;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks this enemy to drop <paramref name="multiplier"/>× gold on death.
    /// Multiple calls keep the highest multiplier.
    /// </summary>
    public void MarkDoubleGold(float multiplier)
    {
        if (multiplier > _goldMultiplier) _goldMultiplier = multiplier;

        // Trigger the gold shimmer visual on first (or upgraded) mark
        var vis = GetComponent<EnemyVisualEffects>()
                  ?? gameObject.AddComponent<EnemyVisualEffects>();
        vis.ApplyDoubleGold();
    }

    /// <summary>
    /// Apply damage from a troop attack.
    /// Returns true if damage was applied; false if the hit was blocked or dodged
    /// (the caller can then show a miss indicator).
    /// </summary>
    public bool TakeDamage(float amount, AttackType attackType = AttackType.Generic, Vector3 attackerPos = default)
    {
        if (IsDead) return false;

        if (Data != null)
        {
            switch (Data.effectType)
            {
                // ── Immunities ────────────────────────────────────────────
                case EnemyEffectType.ImmuneMelee when attackType == AttackType.Melee:
                    AttackMissIndicator.Spawn(transform, attackerPos);
                    return false;

                case EnemyEffectType.ImmuneRanged when attackType == AttackType.Ranged:
                    AttackMissIndicator.Spawn(transform, attackerPos);
                    return false;

                // ── Dodge ─────────────────────────────────────────────────
                case EnemyEffectType.DodgeChance:
                    if (UnityEngine.Random.value < Data.effectValue)
                    {
                        AttackMissIndicator.Spawn(transform, attackerPos);
                        return false;
                    }
                    break;

                // ── Damage modifiers ──────────────────────────────────────
                case EnemyEffectType.DamageReduction:
                    amount *= Mathf.Clamp01(1f - Data.effectValue);
                    break;

                case EnemyEffectType.MaxDamagePerHit:
                    if (Data.effectValue > 0f && amount > Data.effectValue)
                    {
                        amount = Data.effectValue;
                        DamageCapIndicator.Spawn(transform, attackerPos);
                    }
                    break;

                // ── Speed double on hit ────────────────────────────────────
                case EnemyEffectType.SpeedDoubleOnHit:
                    if (_movement != null && !_hitSpeedDoubled)
                    {
                        _hitSpeedDoubled = true;
                        _movement.speed *= 2f;
                    }
                    break;

                // ── Reactive speed on hit (Wasp) ───────────────────────────
                case EnemyEffectType.ReactiveSpeedOnHit:
                    if (_movement != null)
                    {
                        float dur   = Data.effectValue2 > 0f ? Data.effectValue2 : 2f;
                        _movement.speed     = _baseSpeed * (1f + Data.effectValue);
                        _reactiveSpeedTimer = dur; // refreshes on every hit
                        var vis = GetComponent<EnemyVisualEffects>()
                                  ?? gameObject.AddComponent<EnemyVisualEffects>();
                        vis.TriggerSpeedBurst();
                    }
                    break;
            }
        }

        ApplyRaw(amount);
        return true;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void ApplyRaw(float amount)
    {
        float prevHealth = CurrentHealth;
        CurrentHealth   -= amount;

        _healthBar?.SetFill(CurrentHealth / MaxHealth);
        _hitFlash?.Flash();
        _hitStop?.TriggerStop(); // 3-frame animator freeze for impact weight
        AudioManager.Instance?.PlaySFX(AudioManager.Instance?.sfxEnemyHit, 0.3f);

        // SpeedBurst: triggers once when health first crosses the threshold
        if (Data != null && Data.effectType == EnemyEffectType.SpeedBurst && !_speedBurstTriggered)
        {
            float threshold = Data.effectValue > 0f ? Data.effectValue : MaxHealth * 0.5f;
            if (prevHealth > threshold && CurrentHealth <= threshold)
            {
                _speedBurstTriggered = true;
                if (_movement != null) _movement.speed *= 2f;
            }
        }

        // DesperationDash: one-time permanent speed boost at HP threshold
        if (Data != null && Data.effectType == EnemyEffectType.DesperationDash && !_desperationTriggered)
        {
            float threshold = Data.effectValue * MaxHealth;
            if (prevHealth > threshold && CurrentHealth <= threshold)
            {
                _desperationTriggered = true;
                if (_movement != null)
                    _movement.speed = _baseSpeed * (1f + Data.effectValue2);

                var vis = GetComponent<EnemyVisualEffects>()
                          ?? gameObject.AddComponent<EnemyVisualEffects>();
                vis.TriggerDesperationDash();
            }
        }

        // SpawnAtHPThresholds: fire once per effectValue HP interval lost
        if (Data != null && Data.effectType == EnemyEffectType.SpawnAtHPThresholds && Data.effectValue > 0f)
        {
            int expectedSpawns = Mathf.FloorToInt((MaxHealth - Mathf.Max(CurrentHealth, 0f)) / Data.effectValue);
            while (_thresholdSpawnCount < expectedSpawns)
            {
                SpawnThresholdEnemy(_thresholdSpawnCount);
                _thresholdSpawnCount++;
            }
        }

        if (CurrentHealth <= 0f)
            Die();
    }

    // ── Threshold spawn (SpawnAtHPThresholds) ────────────────────────────────

    private void SpawnThresholdEnemy(int spawnIndex)
    {
        // Alternate between spawnEnemyData (odd spawns) and spawnEnemyData2 (even spawns)
        bool   useSecondary = (spawnIndex % 2 == 1);
        var    spawnData    = useSecondary ? Data.spawnEnemyData2 : Data.spawnEnemyData;
        if (spawnData == null || spawnData.prefab == null) return;

        int waypointIdx = _movement != null ? _movement.currentWaypointIndex : 0;
        Vector2 offset  = UnityEngine.Random.insideUnitCircle * 0.3f;
        var go = Instantiate(spawnData.prefab,
                     transform.position + new Vector3(offset.x, offset.y),
                     Quaternion.identity);

        if (go.TryGetComponent<EnemyInstance>(out var ei))  ei.Initialize(spawnData);
        if (go.TryGetComponent<EnemyMovement>(out var em))  em.currentWaypointIndex = waypointIdx;
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        SpawnDeathVFX(transform.position);
        SpawnGoldCoins(transform.position, Mathf.RoundToInt((Data?.goldDrop ?? 0) * _goldMultiplier));

        // SpawnOnDeath: instantiate child enemies at current waypoint
        if (Data != null && Data.effectType == EnemyEffectType.SpawnOnDeath
            && Data.spawnEnemyData != null && Data.spawnEnemyData.prefab != null)
        {
            int waypointIdx = _movement != null ? _movement.currentWaypointIndex : 0;
            for (int i = 0; i < Data.spawnCount; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * 0.25f;
                var     go     = Instantiate(Data.spawnEnemyData.prefab,
                                     transform.position + new Vector3(offset.x, offset.y),
                                     Quaternion.identity);

                if (go.TryGetComponent<EnemyInstance>(out var ei))
                    ei.Initialize(Data.spawnEnemyData);

                if (go.TryGetComponent<EnemyMovement>(out var em))
                    em.currentWaypointIndex = waypointIdx;
            }
        }

        WaveManager.Instance?.OnEnemyDefeated(this);

        // Dissolve-out before destroying — falls back to immediate destroy when
        // SpriteEffectsController is not on the prefab.
        var effects = GetComponent<SpriteEffectsController>();
        if (effects != null)
            effects.Dissolve(() => Destroy(gameObject));
        else
            Destroy(gameObject);
    }

    // ── Gold coins ────────────────────────────────────────────────────────────

    private static void SpawnGoldCoins(Vector3 pos, int goldAmount)
    {
        if (goldAmount <= 0) return;

        int count = Mathf.Clamp(Mathf.CeilToInt(goldAmount / 5f), 1, 5);
        int each  = goldAmount / count;
        int extra = goldAmount % count;

        for (int i = 0; i < count; i++)
        {
            int amount = each + (i == count - 1 ? extra : 0);

            float angle  = (360f / count) * i + UnityEngine.Random.Range(-20f, 20f);
            float radius = UnityEngine.Random.Range(0.05f, 0.15f);
            var scatter  = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius + 0.45f,
                0f);

            var go = new GameObject("GoldCoin");
            go.transform.position = pos;
            go.AddComponent<GoldCoin>().Initialize(amount, scatter);
        }
    }

    // ── Death VFX ─────────────────────────────────────────────────────────────

    private static void SpawnDeathVFX(Vector3 pos)
    {
        var go   = new GameObject("EnemyDeath_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.6f, 3.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.88f, 0.88f, 0.88f),
                                   new Color(1.00f, 1.00f, 1.00f));
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 24;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.06f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(Color.white, 0f), new(new Color(0.7f, 0.7f, 0.7f), 1f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(UnityEngine.Shader.Find("Sprites/Default"));
        psr.sortingOrder = 10;

        ps.Play();
    }
}
