using UnityEngine;

/// <summary>
/// Jump attack for the Praying Mantis troop.
///
/// Normal sequence (1 enemy in range):
///   JumpOut → Strike → JumpBack → Recovering
///
/// Double-leap sequence (2+ enemies in range when attack begins):
///   JumpOut → Strike → JumpOut2 → Strike2 → JumpBack → Recovering
///   The mantis leaps to a second enemy before returning home.
///   Both leaps are clamped to the troop's attack range so the mantis
///   never drifts outside its placement area.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class MantisJumpAttack : MonoBehaviour
{
    [Header("Jump Timing")]
    [Tooltip("Seconds to leap from home to enemy")]
    [SerializeField] private float jumpOutDuration    = 0.32f;
    [Tooltip("Seconds spent near the enemy (attack anim plays here)")]
    [SerializeField] private float strikeHoldDuration = 0.22f;
    [Tooltip("Seconds to return home")]
    [SerializeField] private float jumpBackDuration   = 0.26f;
    [Tooltip("Landing settle pause before cooldown begins")]
    [SerializeField] private float recoverDuration    = 0.12f;

    [Header("Jump Feel")]
    [Tooltip("How high (world units) the arc rises above the straight path")]
    [SerializeField] private float arcHeight          = 0.6f;
    [Tooltip("Scale multiplier at the apex of the jump (1.35 = 35% bigger)")]
    [SerializeField] private float peakScaleBoost     = 1.35f;

    // ── Phase state machine ──────────────────────────────────────────────────

    private enum Phase { Idle, JumpOut, Strike, JumpOut2, Strike2, JumpBack, Recovering }

    private TroopBehavior _behavior;
    private TroopInstance _instance;
    private Animator      _animator;
    private Vector3       _baseScale;
    private SquashStretch _squash;
    private AttackTrail   _trail;

    private Phase _phase      = Phase.Idle;
    private float _phaseTimer = 0f;
    private float _cooldown   = 0f;

    /// <summary>Mantis resting position in world space. Set once from transform on Awake.</summary>
    private Vector3 _homePos;

    // Jump-out arc (first leap)
    private Vector3       _jumpOutStart;
    private Vector3       _jumpTarget;     // soft-tracked toward first enemy

    // Jump-out arc (second leap)
    private Vector3       _jumpOut2Start;
    private Vector3       _jumpTarget2;    // soft-tracked toward second enemy
    private EnemyMovement _secondTarget;   // second enemy — found at BeginJumpOut

    // Jump-back arc
    private Vector3 _returnStart;          // where the mantis was when JumpBack began

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior  = GetComponent<TroopBehavior>();
        _instance  = GetComponent<TroopInstance>();
        _animator  = GetComponent<Animator>();
        _baseScale = transform.localScale;
        _homePos   = transform.position;
        _squash    = GetComponent<SquashStretch>();
        _trail     = GetComponent<AttackTrail>();

        if (GetComponent<MantisTroopAura>() == null)
            gameObject.AddComponent<MantisTroopAura>();
    }

    void OnDisable()
    {
        // Troop moved / sold mid-attack — snap back cleanly
        CancelAttack();
    }

    // ── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:
                if (_cooldown <= 0f && _behavior.CurrentTarget != null)
                    BeginJumpOut();
                break;

            case Phase.JumpOut:    TickJumpOut();    break;
            case Phase.Strike:     TickStrike();     break;
            case Phase.JumpOut2:   TickJumpOut2();   break;
            case Phase.Strike2:    TickStrike2();    break;
            case Phase.JumpBack:   TickJumpBack();   break;
            case Phase.Recovering: TickRecover();    break;
        }
    }

    /// <summary>
    /// During JumpBack phases, override the rotation that TroopBehavior sets in Update()
    /// so the mantis faces toward home rather than the (now behind-it) enemy.
    /// </summary>
    void LateUpdate()
    {
        if (_phase != Phase.JumpBack) return;

        Vector2 dir = _homePos - transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    // ── JumpOut (first leap) ─────────────────────────────────────────────────

    void BeginJumpOut()
    {
        _phase        = Phase.JumpOut;
        _phaseTimer   = 0f;
        _jumpOutStart = transform.position;
        _jumpTarget   = ClampToRange(_behavior.CurrentTarget.transform.position);

        // Look for a second enemy now so we know whether to double-leap
        _secondTarget = FindSecondTarget(_behavior.CurrentTarget);

        if (_animator != null) _animator.speed = 0f;
        _trail?.StartTrail();
    }

    void TickJumpOut()
    {
        if (_behavior.CurrentTarget != null)
            _jumpTarget = ClampToRange(_behavior.CurrentTarget.transform.position);

        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / jumpOutDuration);

        transform.position   = ArcPosition(_jumpOutStart, _jumpTarget, arcHeight, EaseInOutQuad(t));
        transform.localScale = ArcScale(t);

        if (t >= 1f)
        {
            transform.position   = _jumpTarget;
            transform.localScale = _baseScale;
            BeginStrike();
        }
    }

    // ── Strike (first hit) ───────────────────────────────────────────────────

    void BeginStrike()
    {
        _phase       = Phase.Strike;
        _phaseTimer  = 0f;
        _returnStart = transform.position;

        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Play("PrayingMantisAttack", 0, 0f);
        }

        _trail?.StopTrail();
        _squash?.PunchLand();
        SpawnLandingDust(transform.position);
        RegisterHit(_behavior.CurrentTarget);
    }

    void TickStrike()
    {
        if (_behavior.CurrentTarget != null)
        {
            Vector3 toward = _behavior.CurrentTarget.transform.position - transform.position;
            if (toward.magnitude > 0.2f)
                transform.position += toward * (10f * Time.deltaTime);
        }

        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= strikeHoldDuration)
        {
            _returnStart = transform.position;

            // Double-leap: only if the second target is still alive and in range
            if (_secondTarget != null && IsTargetValid(_secondTarget))
                BeginJumpOut2();
            else
                BeginJumpBack();
        }
    }

    // ── JumpOut2 (second leap) ────────────────────────────────────────────────

    void BeginJumpOut2()
    {
        _phase         = Phase.JumpOut2;
        _phaseTimer    = 0f;
        _jumpOut2Start = transform.position;
        _jumpTarget2   = ClampToRange(_secondTarget.transform.position);

        if (_animator != null) _animator.speed = 0f;
        _trail?.StartTrail();
    }

    void TickJumpOut2()
    {
        if (_secondTarget != null)
            _jumpTarget2 = ClampToRange(_secondTarget.transform.position);

        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / jumpOutDuration);

        transform.position   = ArcPosition(_jumpOut2Start, _jumpTarget2, arcHeight * 0.85f, EaseInOutQuad(t));
        transform.localScale = ArcScale(t);

        if (t >= 1f)
        {
            transform.position   = _jumpTarget2;
            transform.localScale = _baseScale;
            BeginStrike2();
        }
    }

    // ── Strike2 (second hit) ─────────────────────────────────────────────────

    void BeginStrike2()
    {
        _phase      = Phase.Strike2;
        _phaseTimer = 0f;
        _returnStart = transform.position;

        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Play("PrayingMantisAttack", 0, 0f);
        }

        _trail?.StopTrail();
        _squash?.PunchLand();
        SpawnLandingDust(transform.position);
        RegisterHit(_secondTarget);
    }

    void TickStrike2()
    {
        if (_secondTarget != null)
        {
            Vector3 toward = _secondTarget.transform.position - transform.position;
            if (toward.magnitude > 0.2f)
                transform.position += toward * (10f * Time.deltaTime);
        }

        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= strikeHoldDuration)
        {
            _returnStart  = transform.position;
            _secondTarget = null;
            BeginJumpBack();
        }
    }

    // ── JumpBack ─────────────────────────────────────────────────────────────

    void BeginJumpBack()
    {
        _phase      = Phase.JumpBack;
        _phaseTimer = 0f;

        if (_animator != null) _animator.speed = 0f;
    }

    void TickJumpBack()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / jumpBackDuration);

        transform.position   = ArcPosition(_returnStart, _homePos, arcHeight * 0.7f, EaseInOutQuad(t));
        transform.localScale = ArcScale(t);

        if (t >= 1f)
        {
            transform.position   = _homePos;
            transform.localScale = _baseScale;
            BeginRecover();
        }
    }

    // ── Recovering ───────────────────────────────────────────────────────────

    void BeginRecover()
    {
        _phase      = Phase.Recovering;
        _phaseTimer = 0f;

        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Play("PrayingMantisIdle", 0, 0f);
        }
    }

    void TickRecover()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= recoverDuration)
            EndAttack();
    }

    void EndAttack()
    {
        _phase    = Phase.Idle;
        _cooldown = _instance.GetEffectiveAttackInterval();
    }

    // ── Hit registration ─────────────────────────────────────────────────────

    void RegisterHit(EnemyMovement target)
    {
        MantisSlashEffect.Spawn(transform.position, transform.up);

        if (target == null) return;

        _instance.DealDamage(target,
            _instance.Data?.attackType ?? AttackType.Melee, transform.position);
    }

    // ── Second-target search ─────────────────────────────────────────────────

    /// <summary>
    /// Finds an enemy in range that is NOT <paramref name="firstTarget"/>.
    /// Returns null if no second valid target exists.
    /// Only called when EnemiesInRange >= 2 is already confirmed.
    /// </summary>
    EnemyMovement FindSecondTarget(EnemyMovement firstTarget)
    {
        if (_behavior.EnemiesInRange < 2) return null;

        var hits = Physics2D.OverlapCircleAll(_homePos, _instance.CurrentRange,
            LayerMask.GetMask("Enemy"));

        EnemyMovement best   = null;
        int           bestWP = -1;

        foreach (var col in hits)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (em == firstTarget) continue;
            if (!IsTargetValid(em)) continue;
            if (em.currentWaypointIndex > bestWP)
            {
                bestWP = em.currentWaypointIndex;
                best   = em;
            }
        }
        return best;
    }

    /// <summary>True when an enemy target is non-null, alive, and within home range.</summary>
    bool IsTargetValid(EnemyMovement em)
    {
        if (em == null) return false;
        if (em.TryGetComponent<EnemyInstance>(out var inst) && inst.IsDead) return false;
        return Vector2.Distance(_homePos, em.transform.position) <= _instance.CurrentRange + 0.1f;
    }

    /// <summary>
    /// Clamps a world position to within <see cref="TroopInstance.CurrentRange"/> of home,
    /// so the mantis never leaps outside its placement area.
    /// </summary>
    Vector3 ClampToRange(Vector3 worldPos)
    {
        Vector2 offset = (Vector2)(worldPos - _homePos);
        if (offset.magnitude > _instance.CurrentRange)
            worldPos = _homePos + (Vector3)(offset.normalized * _instance.CurrentRange);
        return worldPos;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    void CancelAttack()
    {
        _secondTarget        = null;
        transform.position   = _homePos;
        transform.localScale = _baseScale;
        if (_animator != null) _animator.speed = 1f;
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    // ── Landing dust VFX ─────────────────────────────────────────────────────

    static void SpawnLandingDust(Vector3 pos)
    {
        var go   = new GameObject("MantisLanding_Dust");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 2.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.70f, 0.88f, 0.55f),
                                   new Color(0.90f, 1.00f, 0.75f));
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 18;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(0.90f, 1.00f, 0.75f), 0f), new(new Color(0.70f, 0.88f, 0.55f), 1f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 6;

        ps.Play();
    }

    // ── Arc math ─────────────────────────────────────────────────────────────

    static Vector3 ArcPosition(Vector3 from, Vector3 to, float height, float t)
    {
        Vector3 mid = (from + to) * 0.5f + Vector3.up * height;
        Vector3 a   = Vector3.Lerp(from, mid, t);
        Vector3 b   = Vector3.Lerp(mid,  to,  t);
        return Vector3.Lerp(a, b, t);
    }

    Vector3 ArcScale(float t)
    {
        float parabola  = 4f * t * (1f - t);
        float scaleMult = 1f + (peakScaleBoost - 1f) * parabola;
        return _baseScale * scaleMult;
    }

    // ── Easing ───────────────────────────────────────────────────────────────

    static float EaseInOutQuad(float t)
        => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
}
