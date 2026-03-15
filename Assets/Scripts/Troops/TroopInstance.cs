using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to every placed troop. Tracks upgrade level, evolution state, total gold spent,
/// and all active effects. Also owns the central DealDamage() dispatcher used by all
/// attack scripts — this is the single place where troop effects are applied.
/// Requires a Collider2D so raycasts can detect it.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TroopInstance : MonoBehaviour
{
    // ── State ─────────────────────────────────────────────────────────────────

    public TroopData Data           { get; internal set; }
    public int       UpgradeLevel   { get; internal set; }
    public int       EvolutionLevel { get; internal set; }
    public int       TotalGoldSpent { get; internal set; }

    public float CurrentAttack      { get; internal set; }
    public float CurrentAttackSpeed { get; internal set; }
    public float CurrentRange       { get; internal set; }

    /// <summary>Seconds between attacks. Use this in all attack scripts.</summary>
    public float CurrentAttackInterval => 1f / Mathf.Max(0.01f, CurrentAttackSpeed);

    public int  SellValue       => Mathf.RoundToInt(TotalGoldSpent * 0.5f);
    public bool CanUpgrade      => Data != null && UpgradeLevel < Data.upgrades.Length;
    public int  NextUpgradeCost => CanUpgrade ? Data.upgrades[UpgradeLevel].cost : 0;

    public EvolutionData NextEvolution => (Data != null && Data.HasEvolutions && EvolutionLevel < Data.evolutions.Length)
        ? Data.evolutions[EvolutionLevel]
        : null;
    public bool CanEvolve => NextEvolution != null && UpgradeLevel >= NextEvolution.upgradesRequired;

    // ── Per-attack state (effects) ─────────────────────────────────────────

    private int          _attackCount = 0;              // for DoubleEveryFourth
    private readonly List<float> _rampingExpiries = new(); // for RampingDoubleBuff

    // ── Component refs ────────────────────────────────────────────────────────

    private TroopBehavior _behavior;

    void Start()
    {
        _behavior = GetComponent<TroopBehavior>();
    }

    void Update()
    {
        // Expire old ramping stacks
        _rampingExpiries.RemoveAll(t => t <= Time.time);
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public void Initialize(TroopData data)
    {
        Data           = data;
        UpgradeLevel   = 0;
        EvolutionLevel = 0;
        TotalGoldSpent = data.baseCost;

        CurrentAttack      = data.attack;
        CurrentAttackSpeed = data.attackSpeed;
        CurrentRange       = data.range;

        _attackCount = 0;
        _rampingExpiries.Clear();
    }

    // ── Upgrade / Evolve ──────────────────────────────────────────────────────

    public bool Upgrade()
    {
        if (!CanUpgrade) return false;
        var tier = Data.upgrades[UpgradeLevel];

        if (GoldManager.Instance != null && !GoldManager.Instance.SpendGold(tier.cost))
            return false;

        TotalGoldSpent     += tier.cost;
        UpgradeLevel++;
        CurrentAttack      += tier.attackDelta;
        CurrentAttackSpeed += tier.attackSpeedDelta;
        CurrentRange       += tier.rangeDelta;

        return true;
    }

    public TroopInstance Evolve()
    {
        if (!CanEvolve) return null;
        var evo = NextEvolution;
        if (evo.prefab == null)
        {
            Debug.LogWarning($"[TroopInstance] Evolution '{evo.evolutionName}' has no prefab assigned.");
            return null;
        }

        if (GoldManager.Instance != null && !GoldManager.Instance.SpendGold(evo.evolutionCost))
            return null;

        var go      = UnityEngine.Object.Instantiate(evo.prefab, transform.position, transform.rotation, transform.parent);
        var newInst = go.GetComponent<TroopInstance>() ?? go.AddComponent<TroopInstance>();

        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<CapsuleCollider2D>();
            col.isTrigger = true;
        }

        newInst.Data               = Data;
        newInst.UpgradeLevel       = UpgradeLevel;
        newInst.EvolutionLevel     = EvolutionLevel + 1;
        newInst.TotalGoldSpent     = TotalGoldSpent + evo.evolutionCost;
        newInst.CurrentAttack      = CurrentAttack      + evo.attackBoost;
        newInst.CurrentAttackSpeed = CurrentAttackSpeed + evo.attackSpeedBoost;
        newInst.CurrentRange       = CurrentRange       + evo.rangeBoost;

        TroopManager.Instance.Unregister(this);
        TroopManager.Instance.Register(newInst);

        Destroy(gameObject);
        return newInst;
    }

    public void Sell()
    {
        if (Data != null && Data.isLandPlatform)
            SellTroopsOnPlatform();

        GoldManager.Instance?.AddGold(SellValue);
        TroopManager.Instance.Unregister(this);
        Destroy(gameObject);
    }

    void SellTroopsOnPlatform()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        var onPad = new List<TroopInstance>();
        foreach (var troop in TroopManager.Instance.PlacedTroops)
        {
            var pos = new Vector2(troop.transform.position.x, troop.transform.position.y);
            if (col.OverlapPoint(pos))
                onPad.Add(troop);
        }

        foreach (var troop in onPad)
            troop.Sell();
    }

    // ── Active effects ────────────────────────────────────────────────────────

    /// <summary>
    /// All currently active effects:
    ///   • base effect (always)
    ///   + effects from the highest purchased upgrade tier (if any)
    ///   + effects from the current evolution tier (if any)
    /// Effects from a given tier REPLACE those from the previous tier (they don't stack across tiers).
    /// </summary>
    public IEnumerable<TroopEffectConfig> ActiveEffects
    {
        get
        {
            if (Data == null) yield break;

            // Base effect always applies
            if (Data.baseEffect != null && Data.baseEffect.effectType != TroopEffectType.None)
                yield return Data.baseEffect;

            // Upgrade effects: only the most recently purchased tier
            if (UpgradeLevel > 0)
            {
                var tierEffects = Data.upgrades[UpgradeLevel - 1].effects;
                if (tierEffects != null)
                    foreach (var e in tierEffects)
                        if (e != null && e.effectType != TroopEffectType.None) yield return e;
            }

            // Evolution effects: only the current evolution tier
            if (EvolutionLevel > 0)
            {
                var evoEffects = Data.evolutions[EvolutionLevel - 1].effects;
                if (evoEffects != null)
                    foreach (var e in evoEffects)
                        if (e != null && e.effectType != TroopEffectType.None) yield return e;
            }
        }
    }

    public bool HasEffect(TroopEffectType t)
    {
        foreach (var e in ActiveEffects)
            if (e.effectType == t) return true;
        return false;
    }

    public TroopEffectConfig GetEffectConfig(TroopEffectType t)
    {
        foreach (var e in ActiveEffects)
            if (e.effectType == t) return e;
        return null;
    }

    // ── Effective stats (considering conditional/proximity effects) ───────────

    /// <summary>
    /// Effective attack damage for the next hit.
    /// Considers ConditionalAttackBuff, AllyProximityBuff, and RampingDoubleBuff.
    /// Call this instead of CurrentAttack in attack scripts.
    /// </summary>
    public float GetEffectiveDamage()
    {
        float dmg     = CurrentAttack;
        int   enemies = _behavior != null ? _behavior.EnemiesInRange : 0;
        int   allies  = CountNearbyAllies();
        int   stacks  = _rampingExpiries.Count;

        foreach (var cfg in ActiveEffects)
        {
            switch (cfg.effectType)
            {
                case TroopEffectType.ConditionalAttackBuff when enemies > 1:
                    dmg = cfg.conditionalAttack;
                    break;
                case TroopEffectType.AllyProximityBuff:
                    dmg += cfg.allyBonus * allies;
                    break;
            }
        }

        if (stacks > 0) dmg *= Mathf.Pow(2f, stacks);
        return dmg;
    }

    /// <summary>
    /// Effective attack interval (seconds) for the current frame.
    /// Considers ConditionalSpeedBuff, AllySpeedBuff, and RampingDoubleBuff.
    /// </summary>
    public float GetEffectiveAttackInterval()
    {
        float spd     = CurrentAttackSpeed;
        int   enemies = _behavior != null ? _behavior.EnemiesInRange : 0;
        int   allies  = CountNearbyAllies();
        int   stacks  = _rampingExpiries.Count;

        foreach (var cfg in ActiveEffects)
        {
            switch (cfg.effectType)
            {
                case TroopEffectType.ConditionalSpeedBuff when enemies == 1:
                    spd = cfg.conditionalSpeed;
                    break;
                case TroopEffectType.AllySpeedBuff:
                    spd += cfg.allyBonus * allies;
                    break;
            }
        }

        if (stacks > 0) spd *= Mathf.Pow(2f, stacks);
        return 1f / Mathf.Max(0.01f, spd);
    }

    // ── Central damage dispatcher ─────────────────────────────────────────────

    /// <summary>
    /// Apply all troop effects and deal damage to <paramref name="target"/>.
    /// Call this from ALL attack scripts instead of calling enemy.TakeDamage directly.
    /// </summary>
    /// <param name="target">Primary hit target (EnemyMovement).</param>
    /// <param name="attackType">Attack classification; read from TroopData.attackType.</param>
    /// <param name="attackerPos">World position of the attacker (used for miss-indicator direction).</param>
    public void DealDamage(EnemyMovement target, AttackType attackType = AttackType.Generic, Vector3 attackerPos = default)
    {
        if (target == null) return;

        // ── Build final damage ─────────────────────────────────────────────
        float damage = GetEffectiveDamage();

        // DoubleEveryFourth
        if (HasEffect(TroopEffectType.DoubleEveryFourth))
        {
            _attackCount++;
            if (_attackCount % 4 == 0) damage *= 2f;
        }

        // RampingDoubleBuff: add a stack AFTER reading the current multiplier
        // (so stack 1 starts applying on the second hit)
        var rampCfg = GetEffectConfig(TroopEffectType.RampingDoubleBuff);
        if (rampCfg != null)
        {
            _rampingExpiries.RemoveAll(t => t <= Time.time);
            if (_rampingExpiries.Count < rampCfg.rampingMaxStacks)
                _rampingExpiries.Add(Time.time + rampCfg.rampingDuration);
        }

        // ── Determine all targets ──────────────────────────────────────────
        var targets = BuildTargetList(target, attackType);

        // ── Apply damage + post-hit effects to each target ─────────────────
        foreach (var t in targets)
        {
            if (t == null) continue;

            bool hit = t.TakeDamage(damage, attackType, attackerPos);
            if (!hit) continue;

            foreach (var cfg in ActiveEffects)
            {
                switch (cfg.effectType)
                {
                    case TroopEffectType.DoubleGoldDrop:
                        t.GetComponent<EnemyInstance>()?.MarkDoubleGold(cfg.goldMultiplier);
                        break;

                    case TroopEffectType.BurnOnHit:
                        EnemyStatusEffects.ApplyBurn(t.gameObject, damage, cfg.dotInterval);
                        break;

                    case TroopEffectType.PoisonOnHit:
                        EnemyStatusEffects.ApplyPoison(t.gameObject, damage, cfg.dotInterval);
                        break;

                    case TroopEffectType.PoisonSplash:
                        EnemyStatusEffects.ApplyPoison(t.gameObject, damage, cfg.dotInterval);
                        break;

                    case TroopEffectType.FreezeOnHit:
                        EnemyStatusEffects.ApplyFreeze(t.gameObject, cfg.freezeDuration, cfg.freezeSlowFactor);
                        break;

                    case TroopEffectType.StunOnHit:
                        EnemyStatusEffects.ApplyStun(t.gameObject, cfg.stunDuration);
                        break;
                }
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private List<EnemyMovement> BuildTargetList(EnemyMovement primary, AttackType attackType)
    {
        var list = new List<EnemyMovement> { primary };

        if (!HasEffect(TroopEffectType.PoisonSplash)) return list;

        // Non-splash troops gain a splash radius; inherently splash troops
        // already hit multiple enemies through their own mechanism.
        if (Data != null && Data.projectileType == ProjectileType.Splash) return list;

        float r    = Data != null && Data.splashRadius > 0 ? Data.splashRadius : 0.5f;
        var   cols = Physics2D.OverlapCircleAll(primary.transform.position, r);
        foreach (var col in cols)
        {
            if (col.TryGetComponent<EnemyMovement>(out var em) && em != primary)
                list.Add(em);
        }
        return list;
    }

    private int CountNearbyAllies()
    {
        if (Data == null || TroopManager.Instance == null) return 0;
        float rangeSq = CurrentRange * CurrentRange;
        int   count   = 0;
        foreach (var troop in TroopManager.Instance.PlacedTroops)
        {
            if (troop == this || troop.Data != Data) continue;
            if ((troop.transform.position - transform.position).sqrMagnitude <= rangeSq)
                count++;
        }
        return count;
    }
}
