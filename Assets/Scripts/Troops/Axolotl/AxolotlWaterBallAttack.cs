using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water-ball ricochet attack for the Axolotl troop.
///
/// Attack cycle:
///   1. Idle       — waits for cooldown + a target in range.
///   2. ChargingUp — plays "Axolotl Attack" animation; a water ball slowly forms
///                   at the axolotl's mouth, growing from nothing to full size with
///                   a satisfying over-shoot pop.
///   3. Firing     — the ball detaches and travels to the first target, then
///                   ricochets to up to 2 more enemies (3 total). Each hit deals
///                   full damage and spawns a water-ripple impact. The ball
///                   dissolves after its last bounce.
///   4. Recovering — brief pause before the next cycle.
///
/// Water ball visual:
///   • Swirling blue/white particle core (world-space, trails behind in flight).
///   • Three revolving Saturn-style rings — each tilted at a different inclination
///     and spinning at a different speed. The rings are proper 3D circles projected
///     to 2D so they appear as rotating ellipses.
///
/// No prefab required — everything is built procedurally at runtime.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class AxolotlWaterBallAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation State Names")]
    [SerializeField] private string idleStateName   = "Axolotl Idle";
    [SerializeField] private string attackStateName = "Axolotl Attack";

    [Header("Timing")]
    [Tooltip("How long the charge animation plays before the ball launches. Match to clip length.")]
    [SerializeField] private float chargeDuration  = 0.65f;
    [Tooltip("Brief pause after the ball is released before returning to Idle.")]
    [SerializeField] private float recoverDuration = 0.20f;

    [Header("Water Ball")]
    [Tooltip("Local +Y offset from troop centre where the ball forms (the mouth area).")]
    [SerializeField] private float ballMouthOffset = 0.22f;
    [Tooltip("Outer radius of the Saturn rings at full size.")]
    [SerializeField] private float ballRadius      = 0.16f;
    [Tooltip("World-units per second the ball travels between targets.")]
    [SerializeField] private float ballTravelSpeed = 5.5f;
    [Tooltip("Search radius for finding the next ricochet target.")]
    [SerializeField] private float ricochetRadius  = 3.0f;
    [Tooltip("Base number of enemies the ball hits at upgrade level 0. Upgrade 1 adds +1, upgrade 3 adds +2.")]
    [SerializeField] private int   baseBounces     = 3;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 7;

    // ── Internal ──────────────────────────────────────────────────────────────

    private enum Phase { Idle, ChargingUp, Firing, Recovering }

    private TroopBehavior    _behavior;
    private TroopInstance    _instance;
    private Animator         _animator;
    private Phase            _phase      = Phase.Idle;
    private float            _phaseTimer = 0f;
    private float            _cooldown   = 0f;
    private EnemyMovement    _lockedTarget;
    private AxolotlWaterBall _chargeBall;    // ball forming during charge
    private Transform        _ballAnchor;    // mouth anchor, destroyed after firing

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        _animator = GetComponent<Animator>();
    }

    void OnDisable()
    {
        if (_ballAnchor != null) { Destroy(_ballAnchor.gameObject); _ballAnchor = null; }
        _chargeBall = null;
        _phase      = Phase.Idle;
        _cooldown   = 0f;
    }

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:       TickIdle();       break;
            case Phase.ChargingUp: TickCharging();   break;
            case Phase.Recovering: TickRecovering(); break;
        }
    }

    // ── Phase: Idle ───────────────────────────────────────────────────────────

    void TickIdle()
    {
        if (_cooldown > 0f || _behavior.CurrentTarget == null) return;
        BeginCharge();
    }

    // ── Phase: ChargingUp ─────────────────────────────────────────────────────

    void BeginCharge()
    {
        _phase        = Phase.ChargingUp;
        _phaseTimer   = 0f;
        _lockedTarget = _behavior.CurrentTarget;

        PlayAnim(attackStateName);

        // Anchor at the mouth — follows troop rotation automatically
        var anchorGo = new GameObject("WaterBallAnchor");
        anchorGo.transform.SetParent(transform, false);
        anchorGo.transform.localPosition = Vector3.up * ballMouthOffset;
        _ballAnchor = anchorGo.transform;

        // Build the ball as a child of the anchor so it stays at the mouth
        var ballGo = new GameObject("AxolotlWaterBall_Charge");
        ballGo.transform.SetParent(_ballAnchor, false);
        ballGo.transform.localPosition = Vector3.zero;

        _chargeBall = ballGo.AddComponent<AxolotlWaterBall>();
        _chargeBall.Build(ballRadius, sortingLayerName, sortingOrder);
        _chargeBall.SetVisualScale(0f);
    }

    void TickCharging()
    {
        _phaseTimer += Time.deltaTime;

        // Keep tracking target so the troop rotation (done by TroopBehavior) stays accurate
        if (_behavior.CurrentTarget != null)
            _lockedTarget = _behavior.CurrentTarget;

        // Grow the ball 0 → 1 with an overshoot ease so it "pops" into existence
        float t = Mathf.Clamp01(_phaseTimer / chargeDuration);
        _chargeBall?.SetVisualScale(EaseOutBack(t));

        if (_phaseTimer >= chargeDuration)
            BeginFiring();
    }

    // ── Phase: Firing ─────────────────────────────────────────────────────────

    void BeginFiring()
    {
        _phase = Phase.Firing;
        PlayAnim(idleStateName);

        if (_chargeBall == null || _lockedTarget == null)
        {
            CleanupAnchor();
            BeginRecovering();
            return;
        }

        // Detach ball from anchor and let it fly — world position is preserved
        _chargeBall.transform.SetParent(null, true);
        CleanupAnchor();

        // Hand off to the ball — it manages travel, ricochets, and self-destruction
        _chargeBall.Launch(
            _lockedTarget, _instance,
            BouncesForUpgradeLevel(_instance.UpgradeLevel, baseBounces),
            ricochetRadius, ballTravelSpeed,
            sortingLayerName, sortingOrder);

        _chargeBall = null; // no longer our responsibility

        BeginRecovering();
    }

    void CleanupAnchor()
    {
        if (_ballAnchor != null) { Destroy(_ballAnchor.gameObject); _ballAnchor = null; }
    }

    // ── Phase: Recovering ─────────────────────────────────────────────────────

    void BeginRecovering()
    {
        _phase      = Phase.Recovering;
        _phaseTimer = 0f;
        _cooldown   = _instance.GetEffectiveAttackInterval();
    }

    void TickRecovering()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= recoverDuration)
            _phase = Phase.Idle;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void PlayAnim(string stateName)
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.Play(stateName);
    }

    // ── Bounce count helpers (used by attack + sidebar) ───────────────────────

    /// <summary>
    /// Returns total bounce count for the given upgrade level.
    ///   UL 0   → base
    ///   UL 1-2 → base + 1
    ///   UL 3+  → base + 2
    /// </summary>
    public static int BouncesForUpgradeLevel(int upgradeLevel, int baseCount = 3) =>
        upgradeLevel >= 3 ? baseCount + 2 :
        upgradeLevel >= 1 ? baseCount + 1 : baseCount;

    /// <summary>Current bounce count for this troop instance.</summary>
    public int CurrentBounces => BouncesForUpgradeLevel(
        _instance != null ? _instance.UpgradeLevel : 0, baseBounces);

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Overshoot ease — the ball springs slightly past full size then settles.</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float f = t - 1f;
        return 1f + c3 * f * f * f + c1 * f * f;
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The water ball itself: three revolving Saturn-style rings projected from 3D to 2D,
/// surrounding a glowing swirling particle core.
///
/// Lifecycle:
///   • Created by AxolotlWaterBallAttack, parented to a mouth anchor during charge.
///   • SetVisualScale() is called each frame to grow it from 0 → 1.
///   • Launch() is called when it fires — it detaches, flies, bounces, then dissolves.
/// </summary>
public class AxolotlWaterBall : MonoBehaviour
{
    // ── Ring constants ────────────────────────────────────────────────────────

    private const int RingSegments = 36;

    // Ring index 0 = outermost, 2 = innermost
    private static readonly float[] RingInclinations = { 20f,  55f,  82f  }; // φ°: tilt from vertical
    private static readonly float[] RingRevSpeeds    = { 75f, -52f,  38f  }; // θ°/s: revolution speed
    private static readonly float[] RingRadiusMult   = { 1.0f, 0.82f, 0.65f };
    private static readonly float[] RingWidths       = { 0.024f, 0.018f, 0.013f };
    private static readonly Color[] RingColors =
    {
        new Color(0.35f, 0.82f, 1.00f, 0.90f),  // vivid sky-blue
        new Color(0.60f, 0.93f, 1.00f, 0.78f),  // pale cyan
        new Color(0.88f, 0.99f, 1.00f, 0.62f),  // near-white
    };

    // ── Visual state ──────────────────────────────────────────────────────────

    private float          _visualScale = 0f;
    private float          _baseRadius;
    private float[]        _revAngles = new float[3];
    private LineRenderer[] _rings     = new LineRenderer[3];
    private ParticleSystem _corePs;

    // ── Travel state ──────────────────────────────────────────────────────────

    private bool          _launched       = false;
    private TroopInstance _owner;
    private EnemyMovement _currentTarget;
    private int           _bouncesLeft;
    private float         _travelSpeed;
    private float         _ricochetRadius;
    private string        _sortLayer;
    private int           _sortOrder;

    private readonly HashSet<EnemyMovement> _hit = new();

    private const float SlowDuration = 1.5f;
    private const float SlowFactor   = 0.45f;

    // ── Build ─────────────────────────────────────────────────────────────────

    public void Build(float baseRadius, string sortLayer, int sortOrder)
    {
        _baseRadius = baseRadius;
        _sortLayer  = sortLayer;
        _sortOrder  = sortOrder;

        BuildRings(sortLayer, sortOrder);
        BuildCoreParticles(sortLayer, sortOrder);
    }

    void BuildRings(string sortLayer, int sortOrder)
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject($"Ring_{i}");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = true;
            lr.positionCount     = RingSegments;
            lr.numCapVertices    = 0;
            lr.numCornerVertices = 0;
            lr.widthMultiplier   = RingWidths[i];
            lr.material          = new Material(Shader.Find("Sprites/Default"));
            lr.startColor        = RingColors[i];
            lr.endColor          = RingColors[i];
            lr.sortingLayerName  = sortLayer;
            lr.sortingOrder      = sortOrder + i;

            _rings[i] = lr;
        }
    }

    void BuildCoreParticles(string sortLayer, int sortOrder)
    {
        var go = new GameObject("WaterCore");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.50f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.02f, 0.10f);  // barely moving — fill the centre
        main.startSize       = new ParticleSystem.MinMaxCurve(0.030f, 0.070f); // bigger, denser droplets
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.30f, 0.78f, 1.00f, 0.95f),
                                   new Color(0.78f, 0.97f, 1.00f, 0.85f));
        main.gravityModifier = 0f;
        main.maxParticles    = 120;

        var emission = ps.emission;
        emission.rateOverTime = 95f;  // high rate so the core looks solid

        var shape = ps.shape;
        shape.enabled      = true;
        shape.shapeType    = ParticleSystemShapeType.Sphere;
        shape.radius       = 0.055f;
        shape.radiusThickness = 1f;  // emit from the full volume, not just the surface

        // Gentle swirl — all three axes must share TwoConstants mode
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(-0.12f,  0.12f);
        vel.y       = new ParticleSystem.MinMaxCurve(-0.12f,  0.12f);
        vel.z       = new ParticleSystem.MinMaxCurve(-0.01f,  0.01f);

        // Colour: bright cyan → deep blue → transparent
        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.78f, 0.97f, 1.00f), 0.00f),
                new GradientColorKey(new Color(0.30f, 0.78f, 1.00f), 0.50f),
                new GradientColorKey(new Color(0.10f, 0.45f, 0.90f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(0.90f, 0.10f),
                new GradientAlphaKey(0.55f, 0.60f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f,    0.25f),
                                   new Keyframe(0.30f, 1.00f),
                                   new Keyframe(1f,    0.00f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortLayer;
        psr.sortingOrder     = sortOrder + 3;

        ps.Play();
        _corePs = ps;
    }

    // ── Visual scale (driven by charge phase) ─────────────────────────────────

    /// <summary>
    /// t = 0: invisible. t = 1: full size.
    /// The attack script calls this every frame during charge.
    /// </summary>
    public void SetVisualScale(float t)
    {
        _visualScale = Mathf.Clamp(t, 0f, 1.1f); // allow slight overshoot from EaseOutBack

        if (_corePs != null)
        {
            float clamped = Mathf.Clamp01(t);
            var shape = _corePs.shape;
            shape.radius = 0.055f * clamped;

            var main = _corePs.main;
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.030f * clamped,
                0.070f * clamped);
        }
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once by the attack script when the charge finishes.
    /// After this, the ball is fully self-managing.
    /// </summary>
    public void Launch(EnemyMovement firstTarget, TroopInstance owner,
                       int maxBounces, float ricochetRadius, float travelSpeed,
                       string sortLayer, int sortOrder)
    {
        _owner          = owner;
        _currentTarget  = firstTarget;
        _bouncesLeft    = maxBounces;
        _ricochetRadius = ricochetRadius;
        _travelSpeed    = travelSpeed;
        _sortLayer      = sortLayer;
        _sortOrder      = sortOrder;
        _launched       = true;
        _visualScale    = 1f;

        if (firstTarget != null) _hit.Add(firstTarget);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        // Advance ring revolution angles
        for (int i = 0; i < 3; i++)
            _revAngles[i] += RingRevSpeeds[i] * Time.deltaTime;

        DrawAllRings();

        if (_launched) TickTravel();
    }

    // ── Travel & ricochet ─────────────────────────────────────────────────────

    void TickTravel()
    {
        if (_bouncesLeft <= 0)         { Dissolve(); return; }
        if (_currentTarget == null)    { Dissolve(); return; } // target died mid-flight

        Vector3 targetPos = _currentTarget.transform.position;
        float   step      = _travelSpeed * Time.deltaTime;
        float   dist      = Vector3.Distance(transform.position, targetPos);

        if (dist <= step)
        {
            transform.position = targetPos;
            HitTarget(_currentTarget);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
        }
    }

    void HitTarget(EnemyMovement target)
    {
        if (target != null)
        {
            _owner?.DealDamage(target, _owner.Data?.attackType ?? AttackType.Ranged,
                               transform.position);
            EnemyStatusEffects.ApplyFreeze(target.gameObject, SlowDuration, SlowFactor);
            EnemySlowIndicator.Apply(target.gameObject, SlowDuration);
        }

        SpawnHitRipple(transform.position);

        _bouncesLeft--;

        if (_bouncesLeft <= 0) { Dissolve(); return; }

        EnemyMovement next = FindNextTarget();
        if (next == null)     { Dissolve(); return; }

        _hit.Add(next);
        _currentTarget = next;
    }

    EnemyMovement FindNextTarget()
    {
        var   cols  = Physics2D.OverlapCircleAll(transform.position, _ricochetRadius);
        EnemyMovement best  = null;
        float         bestD = float.MaxValue;

        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (_hit.Contains(em)) continue;

            float d = (em.transform.position - transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = em; }
        }

        return best;
    }

    // ── Ring drawing ──────────────────────────────────────────────────────────

    void DrawAllRings()
    {
        Vector3 centre = transform.position;
        float   scale  = Mathf.Max(0f, _visualScale);

        for (int i = 0; i < 3; i++)
        {
            float r = _baseRadius * RingRadiusMult[i] * scale;
            DrawRing(_rings[i], centre, r,
                     RingInclinations[i], _revAngles[i],
                     RingColors[i], scale, RingWidths[i]);
        }
    }

    /// <summary>
    /// Projects a 3D tilted circle onto the 2D screen (XY plane).
    ///
    /// The circle's plane normal is n = (sin φ cos θ, sin φ sin θ, cos φ).
    /// Two orthonormal basis vectors spanning the plane:
    ///   e1 = (−sin θ,  cos θ,  0)
    ///   e2 = (−cos φ cos θ, −cos φ sin θ, sin φ)
    /// Projecting to 2D (ignoring Z) gives a rotating ellipse as θ advances.
    /// </summary>
    void DrawRing(LineRenderer lr, Vector3 centre, float r,
                  float phiDeg, float thetaDeg, Color color,
                  float alphaMult, float baseWidth)
    {
        float phi   = phiDeg   * Mathf.Deg2Rad;
        float theta = thetaDeg * Mathf.Deg2Rad;

        float sinPhi = Mathf.Sin(phi), cosPhi = Mathf.Cos(phi);
        float sinTheta = Mathf.Sin(theta), cosTheta = Mathf.Cos(theta);

        // 2D projections of the plane basis vectors
        var e1 = new Vector2(-sinTheta,            cosTheta);
        var e2 = new Vector2(-cosPhi * cosTheta,  -cosPhi * sinTheta);

        for (int i = 0; i < RingSegments; i++)
        {
            float a      = 2f * Mathf.PI * i / RingSegments;
            var   offset = r * (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a));
            lr.SetPosition(i, centre + new Vector3(offset.x, offset.y, 0f));
        }

        float clampedAlpha = Mathf.Clamp01(alphaMult);
        lr.widthMultiplier = baseWidth * Mathf.Max(0.001f, clampedAlpha);
        lr.startColor = new Color(color.r, color.g, color.b, color.a * clampedAlpha);
        lr.endColor   = new Color(color.r, color.g, color.b, color.a * clampedAlpha);
    }

    // ── Hit ripple ────────────────────────────────────────────────────────────

    void SpawnHitRipple(Vector3 pos)
    {
        // Burst of water droplet particles
        var go = new GameObject("WaterHit_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 3.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.30f, 0.78f, 1.00f),
                                   Color.white);
        main.gravityModifier = 0.3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 18;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white,                     0.00f),
                new GradientColorKey(new Color(0.30f, 0.78f, 1.00f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.00f, 0.00f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 1f),
                                   new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = _sortLayer;
        psr.sortingOrder     = _sortOrder + 5;

        ps.Play();

        // Expanding water-ripple ring
        var ringGo = new GameObject("WaterHit_Ring");
        ringGo.transform.position = pos;
        ringGo.AddComponent<WaterRippleRing>().Init(
            new Color(0.25f, 0.72f, 1.00f, 0.85f), _sortLayer, _sortOrder + 4);
    }

    // ── Dissolve ──────────────────────────────────────────────────────────────

    void Dissolve()
    {
        _launched      = false;
        _currentTarget = null;

        // Hide all rings immediately
        foreach (var lr in _rings)
            if (lr != null) lr.enabled = false;

        // Let existing core particles live out naturally
        if (_corePs != null)
            _corePs.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Final dissolve burst at current position
        SpawnHitRipple(transform.position);

        Destroy(gameObject, 1.2f); // wait for particles to finish
    }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// An expanding water-ripple ring used as hit feedback for the axolotl water ball.
/// Identical in structure to the frog shockwave ring but tuned for impact scale.
/// </summary>
public class WaterRippleRing : MonoBehaviour
{
    private LineRenderer _lr;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.32f;
    private const float MaxRadius  = 0.36f;
    private const int   Segments   = 24;
    private const float StartWidth = 0.042f;

    public void Init(Color color, string sortingLayer, int sortingOrder)
    {
        _color = color;
        _lr    = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace     = true;
        _lr.loop              = true;
        _lr.positionCount     = Segments;
        _lr.numCapVertices    = 0;
        _lr.numCornerVertices = 0;
        _lr.widthMultiplier   = StartWidth;
        _lr.material          = new Material(Shader.Find("Sprites/Default"));
        _lr.sortingLayerName  = sortingLayer;
        _lr.sortingOrder      = sortingOrder;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t      = Mathf.Clamp01(_timer / Duration);
        float radius = Mathf.Lerp(0f, MaxRadius, EaseOutCubic(t));
        float alpha  = Mathf.Lerp(0.85f, 0f, t);
        float width  = Mathf.Lerp(StartWidth, 0.005f, t);

        _lr.widthMultiplier = width;
        _lr.startColor      = new Color(_color.r, _color.g, _color.b, alpha);
        _lr.endColor        = new Color(_color.r, _color.g, _color.b, alpha);

        Vector3 c = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _lr.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        if (t >= 1f) Destroy(gameObject);
    }

    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
}
