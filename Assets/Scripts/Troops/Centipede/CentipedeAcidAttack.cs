using UnityEngine;

/// <summary>
/// Acid-spit attack for the Centipede troop.
///
/// Sequence per attack:
///   1. Idle     — Animator plays the idle animation
///   2. Spitting — freezes the Animator for a brief windup moment,
///                 spawns an AcidProjectile toward the target, then resumes
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class CentipedeAcidAttack : MonoBehaviour
{
    [Header("Spit")]
    [Tooltip("Local +Y offset from centre where the acid ball spawns (the mouth)")]
    [SerializeField] private float mouthOffset       = 0.28f;
    [Tooltip("World-units per second the projectile travels")]
    [SerializeField] private float projectileSpeed   = 7f;
    [Tooltip("Seconds the idle animation is frozen during the spit windup")]
    [SerializeField] private float spitPauseDuration = 0.18f;

    [Header("Projectile Visuals")]
    [SerializeField] private float  projectileRadius = 0.05f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 7;

    // ── Internal state ───────────────────────────────────────

    private enum Phase { Idle, Spitting }

    private TroopBehavior _behavior;
    private TroopInstance _instance;
    private Animator      _animator;

    private Phase _phase      = Phase.Idle;
    private float _phaseTimer = 0f;
    private float _cooldown   = 0f;

    // ── Lifecycle ────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        _animator = GetComponent<Animator>();
    }

    void OnDisable()
    {
        if (_animator != null) _animator.speed = 1f;
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        if (_phase == Phase.Idle)
        {
            if (_cooldown <= 0f && _behavior.CurrentTarget != null)
                BeginSpit();
        }
        else // Spitting: hold pose briefly, then resume
        {
            _phaseTimer += Time.deltaTime;
            if (_phaseTimer >= spitPauseDuration)
            {
                _phase = Phase.Idle;
                if (_animator != null) _animator.speed = 1f;
                _cooldown = _instance.CurrentAttackInterval;
            }
        }
    }

    void BeginSpit()
    {
        _phase      = Phase.Spitting;
        _phaseTimer = 0f;

        if (_animator != null) _animator.speed = 0f; // freeze idle anim during spit windup

        // Spawn at the centipede's mouth (local +Y = facing direction set by TroopBehavior)
        Vector3 spawnPos = transform.TransformPoint(Vector3.up * mouthOffset);

        var go   = new GameObject("AcidProjectile");
        go.transform.position = spawnPos;

        var proj = go.AddComponent<AcidProjectile>();
        proj.Launch(
            target       : _behavior.CurrentTarget,
            damage       : _instance.CurrentAttack,
            speed        : projectileSpeed,
            radius       : projectileRadius,
            sortingLayer : sortingLayerName,
            sortingOrder : sortingOrder,
            onHit        : OnProjectileHit
        );
    }

    // ── Hit callback (called by AcidProjectile) ───────────────

    void OnProjectileHit(EnemyMovement enemy)
    {
        Debug.Log($"[CentipedeAcid] Hit {enemy.name} — damage pending: {_instance.CurrentAttack}");
        // TODO: enemy.TakeDamage(_instance.CurrentAttack);
    }
}
