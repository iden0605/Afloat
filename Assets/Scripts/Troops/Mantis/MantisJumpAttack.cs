using UnityEngine;

/// <summary>
/// Jump attack for the Praying Mantis troop.
///
/// Sequence per attack:
///   1. JumpOut   — Mantis leaps toward the tracked enemy along a quadratic arc
///                  (scales up at the apex — cartoon "in-air" feel). Idle anim frozen.
///                  Soft-tracks the enemy position each frame so the landing stays accurate.
///   2. Strike    — Plays attack animation for a short hold near the enemy.
///                  Damage is applied immediately on landing.
///                  ConditionalAttackBuff: damage × 3 if multiple enemies are in range.
///   3. JumpBack  — Arcs back to the original resting position.
///                  Rotation is overridden in LateUpdate to face home during this phase.
///   4. Recovering — Brief land-settle pause; idle resumes; attack cooldown starts.
///
/// The mantis cannot jump again until it has fully returned home (Phase.Idle).
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

    private enum Phase { Idle, JumpOut, Strike, JumpBack, Recovering }

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

    // Jump-out arc
    private Vector3 _jumpOutStart;   // position when JumpOut began
    private Vector3 _jumpTarget;     // enemy position, soft-updated each frame during JumpOut

    // Jump-back arc
    private Vector3 _returnStart;    // where the mantis landed near the enemy

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
            case Phase.JumpBack:   TickJumpBack();   break;
            case Phase.Recovering: TickRecover();    break;
        }
    }

    /// <summary>
    /// During JumpBack, override the rotation that TroopBehavior sets in Update()
    /// so the mantis faces toward home instead of facing the (now behind-it) enemy.
    /// </summary>
    void LateUpdate()
    {
        if (_phase != Phase.JumpBack) return;

        Vector2 dir = _homePos - transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // -90° matches the convention used in TroopBehavior (sprite "up" = forward)
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    // ── JumpOut ──────────────────────────────────────────────────────────────

    void BeginJumpOut()
    {
        _phase        = Phase.JumpOut;
        _phaseTimer   = 0f;
        _jumpOutStart = transform.position;
        _jumpTarget   = _behavior.CurrentTarget.transform.position;

        if (_animator != null) _animator.speed = 0f; // freeze idle during air-time
        _trail?.StartTrail(); // leave a motion trail during the leap
    }

    void TickJumpOut()
    {
        // Soft-track: update landing point every frame so the mantis adjusts to a moving enemy
        if (_behavior.CurrentTarget != null)
            _jumpTarget = _behavior.CurrentTarget.transform.position;

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

    // ── Strike ───────────────────────────────────────────────────────────────

    void BeginStrike()
    {
        _phase        = Phase.Strike;
        _phaseTimer   = 0f;
        _returnStart  = transform.position; // record landing spot for the return arc

        // Play the attack animation from the beginning
        if (_animator != null)
        {
            _animator.speed = 1f;
            _animator.Play("PrayingMantisAttack", 0, 0f);
        }

        _trail?.StopTrail();           // stop trail on land
        _squash?.PunchLand();          // wide squash on landing impact

        SpawnLandingDust(transform.position);

        // Register damage immediately on landing
        RegisterHit();
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
                                   new Color(0.70f, 0.88f, 0.55f),   // lime-green puff
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

    void TickStrike()
    {
        // Gently follow the enemy so the mantis doesn't drift too far if it's fast
        if (_behavior.CurrentTarget != null)
        {
            Vector3 toward = _behavior.CurrentTarget.transform.position - transform.position;
            if (toward.magnitude > 0.2f)
                transform.position += toward * (10f * Time.deltaTime);
        }

        _phaseTimer += Time.deltaTime;

        if (_phaseTimer >= strikeHoldDuration)
        {
            _returnStart = transform.position; // update for accurate return arc
            BeginJumpBack();
        }
    }

    void RegisterHit()
    {
        // Spawn slash effect at the landing position, oriented in the attack direction.
        // transform.up points toward the enemy because TroopBehavior uses a -90° offset
        // when it sets rotation (sprite "up" = forward).
        MantisSlashEffect.Spawn(transform.position, transform.up);

        if (_behavior.CurrentTarget == null) return;

        _instance.DealDamage(_behavior.CurrentTarget,
            _instance.Data?.attackType ?? AttackType.Melee, transform.position);
    }

    // ── JumpBack ─────────────────────────────────────────────────────────────

    void BeginJumpBack()
    {
        _phase      = Phase.JumpBack;
        _phaseTimer = 0f;

        if (_animator != null) _animator.speed = 0f; // freeze during return flight
    }

    void TickJumpBack()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / jumpBackDuration);

        // Return arc is slightly lower than the outbound arc (0.7× height)
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
            _animator.Play("PrayingMantisIdle", 0, 0f); // return to idle anim on landing
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    void CancelAttack()
    {
        transform.position   = _homePos;
        transform.localScale = _baseScale;
        if (_animator != null) _animator.speed = 1f;
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    /// <summary>
    /// Quadratic bezier arc that always bulges upward in world space.
    /// The control point sits above the midpoint of the straight path by <paramref name="height"/> units.
    /// </summary>
    static Vector3 ArcPosition(Vector3 from, Vector3 to, float height, float t)
    {
        Vector3 mid = (from + to) * 0.5f + Vector3.up * height;
        Vector3 a   = Vector3.Lerp(from, mid, t);
        Vector3 b   = Vector3.Lerp(mid,  to,  t);
        return Vector3.Lerp(a, b, t);
    }

    /// <summary>
    /// Uniform scale that peaks at <see cref="peakScaleBoost"/> × base at t = 0.5,
    /// using a parabola (4t(1−t)) so it's normal at take-off and landing.
    /// </summary>
    Vector3 ArcScale(float t)
    {
        float parabola  = 4f * t * (1f - t);          // 0→1→0 across [0,1]
        float scaleMult = 1f + (peakScaleBoost - 1f) * parabola;
        return _baseScale * scaleMult;
    }

    // ── Easing ───────────────────────────────────────────────────────────────

    /// <summary>Smooth start and end — natural parabolic arc feel.</summary>
    static float EaseInOutQuad(float t)
        => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
}
