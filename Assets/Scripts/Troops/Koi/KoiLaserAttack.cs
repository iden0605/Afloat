using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Piercing laser-beam attack for the Koi troop.
///
/// Attack sequence per cycle:
///   1. Idle        — waits until cooldown expires and a target enters range.
///   2. ChargingUp  — plays the "Koi Attack" animation and crackles charge particles
///                    at the mouth. Direction is updated live until the animation ends.
///   3. Firing      — fires a neon-blue laser beam that grows to peak thickness,
///                    holds briefly, then shrinks away. Piercing damage is applied
///                    (via OverlapBoxAll) to every enemy the beam touches, once per shot.
///   4. Recovering  — brief pause before returning to Idle.
///
/// Laser visual: five stacked LineRenderers (outer glow → mid glow → core blue →
/// core white → animated jitter layer) that scale width with a grow/hold/shrink
/// envelope. The Perlin-noise jitter layer gives the beam a live, electric texture.
///
/// --- Inspector notes ---
/// • idleStateName / attackStateName must match the states in the Koi Animator Controller.
///   Defaults: "Koi Idle" / "Koi Attack".
/// • chargeDuration should match the length of the Koi Attack animation clip.
/// • sortingLayerName / sortingOrder should sit above the troop sprite.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class KoiLaserAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation State Names")]
    [Tooltip("Animator state name for idle on this variant.")]
    [SerializeField] private string idleStateName   = "Koi Idle";

    [Tooltip("Animator state name for the charge-up on this variant.")]
    [SerializeField] private string attackStateName = "Koi Attack";

    [Header("Timing")]
    [Tooltip("How long the charge animation plays before the beam fires. Match to clip length.")]
    [SerializeField] private float chargeDuration  = 0.55f;

    [Tooltip("Seconds the beam width grows from zero to peak.")]
    [SerializeField] private float growDuration    = 0.18f;

    [Tooltip("Seconds the beam holds at peak width.")]
    [SerializeField] private float holdDuration    = 0.10f;

    [Tooltip("Seconds the beam width shrinks back to zero.")]
    [SerializeField] private float shrinkDuration  = 0.32f;

    [Tooltip("Brief pause after the beam fades before re-entering Idle.")]
    [SerializeField] private float recoverDuration = 0.15f;

    [Header("Laser Geometry")]
    [Tooltip("Local +Y offset from troop centre where the beam originates (the mouth).")]
    [SerializeField] private float mouthOffset    = 0.20f;

    [Tooltip("How far the beam reaches across the map (world units).")]
    [SerializeField] private float laserLength    = 35f;

    [Tooltip("Half-width of the overlap box used to detect enemy colliders along the beam.")]
    [SerializeField] private float castHalfWidth  = 0.10f;

    [Header("Laser Peak Widths")]
    [SerializeField] private float outerGlowWidth = 0.56f;
    [SerializeField] private float midGlowWidth   = 0.26f;
    [SerializeField] private float coreBlueWidth  = 0.11f;
    [SerializeField] private float coreWhiteWidth = 0.045f;
    [SerializeField] private float jitterWidth    = 0.038f;

    [Header("Laser Colors")]
    [SerializeField] private Color outerGlowColor = new Color(0.00f, 0.20f, 0.95f, 0.30f);
    [SerializeField] private Color midGlowColor   = new Color(0.10f, 0.55f, 1.00f, 0.68f);
    [SerializeField] private Color coreBlueColor  = new Color(0.30f, 0.82f, 1.00f, 0.95f);
    [SerializeField] private Color coreWhiteColor = new Color(0.88f, 0.97f, 1.00f, 1.00f);
    [SerializeField] private Color jitterColor    = new Color(1.00f, 1.00f, 1.00f, 0.50f);

    [Header("Jitter Layer (energy texture)")]
    [Tooltip("Number of segments in the jitter path. More = smoother noise.")]
    [SerializeField] private int   jitterSegments  = 32;

    [Tooltip("Maximum perpendicular displacement of the jitter path (world units).")]
    [SerializeField] private float jitterAmplitude = 0.026f;

    [Tooltip("Speed of the Perlin-noise animation along the beam.")]
    [SerializeField] private float jitterSpeed     = 14f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder      = 7;

    // ── Internal state ────────────────────────────────────────────────────────

    private enum Phase { Idle, ChargingUp, Firing, Recovering }

    private TroopBehavior _behavior;
    private TroopInstance _instance;
    private Animator      _animator;

    private Phase         _phase      = Phase.Idle;
    private float         _phaseTimer = 0f;
    private float         _cooldown   = 0f;

    // Committed target and locked fire direction
    private EnemyMovement _lockedTarget;
    private Vector3       _lockedFireDir;

    // Laser LineRenderers built in Awake
    private LineRenderer  _lrOuter;
    private LineRenderer  _lrMid;
    private LineRenderer  _lrCoreBlue;
    private LineRenderer  _lrCoreWhite;
    private LineRenderer  _lrJitter;

    // Per-instance noise seed so multiple kois don't jitter in sync
    private float _jitterSeed;

    // Whether damage has been dealt this firing phase
    private bool _damageDealt;

    // HashSet prevents double-hitting an enemy in one beam shot
    private readonly HashSet<EnemyMovement> _hitThisShot = new();

    // Handle to the charge-particle system so we can stop it when the beam fires
    private ParticleSystem _chargePs;

    // ── Awake ─────────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior   = GetComponent<TroopBehavior>();
        _instance   = GetComponent<TroopInstance>();
        _animator   = GetComponent<Animator>();
        _jitterSeed = Random.value * 100f;
        BuildLaserRenderers();
    }

    void OnDisable()
    {
        // Troop was sold or disabled mid-attack — clean up immediately.
        SetLaserVisible(false);
        StopChargeParticles(clearImmediate: true);
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    // ── LineRenderer construction ─────────────────────────────────────────────

    void BuildLaserRenderers()
    {
        // Layers drawn back-to-front: outermost glow first, jitter on top.
        _lrOuter     = MakeStraightLR("Laser_OuterGlow",  outerGlowColor, outerGlowWidth, sortingOrder);
        _lrMid       = MakeStraightLR("Laser_MidGlow",    midGlowColor,   midGlowWidth,   sortingOrder + 1);
        _lrCoreBlue  = MakeStraightLR("Laser_CoreBlue",   coreBlueColor,  coreBlueWidth,  sortingOrder + 2);
        _lrCoreWhite = MakeStraightLR("Laser_CoreWhite",  coreWhiteColor, coreWhiteWidth, sortingOrder + 3);

        // Jitter layer: many segments for a jagged energy path
        _lrJitter = MakeStraightLR("Laser_Jitter", jitterColor, jitterWidth, sortingOrder + 4);
        _lrJitter.positionCount = jitterSegments + 1;
    }

    LineRenderer MakeStraightLR(string goName, Color color, float width, int order)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = true;
        lr.positionCount     = 2;
        lr.loop              = false;
        lr.numCapVertices    = 8;
        lr.numCornerVertices = 4;
        lr.startWidth        = width;
        lr.endWidth          = width;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color             = color;
        lr.material           = mat;
        lr.startColor         = color;
        lr.endColor           = color;
        lr.sortingLayerName   = sortingLayerName;
        lr.sortingOrder       = order;
        lr.enabled            = false;
        return lr;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:       TickIdle();       break;
            case Phase.ChargingUp: TickCharging();   break;
            case Phase.Firing:     TickFiring();     break;
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
        PlayAttack();
        SpawnChargeParticles();
    }

    void TickCharging()
    {
        _phaseTimer += Time.deltaTime;

        // Keep updating the target so the fire direction is as current as possible
        if (_behavior.CurrentTarget != null)
            _lockedTarget = _behavior.CurrentTarget;

        if (_phaseTimer >= chargeDuration)
            BeginFiring();
    }

    // ── Phase: Firing ─────────────────────────────────────────────────────────

    void BeginFiring()
    {
        _phase       = Phase.Firing;
        _phaseTimer  = 0f;
        _damageDealt = false;
        _hitThisShot.Clear();

        // TroopBehavior has been rotating the koi toward the target throughout charge,
        // so transform.up already points at the target. Lock that direction now.
        _lockedFireDir = transform.up;

        StopChargeParticles(clearImmediate: false);
        SetLaserVisible(true);
        PlayIdle();
    }

    void TickFiring()
    {
        _phaseTimer += Time.deltaTime;

        float totalDuration = growDuration + holdDuration + shrinkDuration;

        // ── Compute width envelope ────────────────────────────────────────────
        float widthT;
        if (_phaseTimer <= growDuration)
        {
            float g = _phaseTimer / growDuration;
            widthT = EaseOutCubic(g);
        }
        else if (_phaseTimer <= growDuration + holdDuration)
        {
            widthT = 1f;
        }
        else
        {
            float s = (_phaseTimer - growDuration - holdDuration) / shrinkDuration;
            widthT = 1f - EaseInCubic(Mathf.Clamp01(s));
        }

        // ── Deal piercing damage once when the beam reaches peak ──────────────
        if (!_damageDealt && _phaseTimer >= growDuration)
        {
            _damageDealt = true;
            DealPiercingDamage();
        }

        // ── Draw all laser layers ─────────────────────────────────────────────
        DrawLaser(widthT);

        // ── Transition out ────────────────────────────────────────────────────
        if (_phaseTimer >= totalDuration)
        {
            SetLaserVisible(false);
            BeginRecovering();
        }
    }

    void DealPiercingDamage()
    {
        Vector3 origin   = LaserOrigin();
        Vector2 dir2d    = new Vector2(_lockedFireDir.x, _lockedFireDir.y).normalized;
        Vector2 mid2d    = (Vector2)origin + dir2d * laserLength * 0.5f;
        float   angleDeg = Mathf.Atan2(dir2d.y, dir2d.x) * Mathf.Rad2Deg;

        // A thin box spanning the full laser length detects all overlapping enemies.
        var cols = Physics2D.OverlapBoxAll(
            point: mid2d,
            size:  new Vector2(laserLength, castHalfWidth * 2f),
            angle: angleDeg
        );

        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (!_hitThisShot.Add(em)) continue;
            _instance.DealDamage(em, _instance.Data?.attackType ?? AttackType.Ranged, transform.position);
        }

        // Muzzle-flash VFX at beam origin
        SpawnMuzzleFlash(origin);
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

    // ── Laser drawing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Thickness multiplier based on upgrade level, always relative to the base inspector widths.
    /// U1 = +50%, U2 = +100%, U3 = +150%.
    /// </summary>
    float ThicknessMultiplier() => _instance.UpgradeLevel switch
    {
        1 => 1.5f,
        2 => 2.0f,
        3 => 2.5f,
        _ => 1.0f
    };

    void DrawLaser(float widthT)
    {
        Vector3 origin = LaserOrigin();
        Vector3 end    = origin + _lockedFireDir * laserLength;

        float thick = ThicknessMultiplier();

        DrawStraightLR(_lrOuter,     origin, end, outerGlowWidth * widthT * thick, outerGlowColor, widthT);
        DrawStraightLR(_lrMid,       origin, end, midGlowWidth   * widthT * thick, midGlowColor,   widthT);
        DrawStraightLR(_lrCoreBlue,  origin, end, coreBlueWidth  * widthT * thick, coreBlueColor,  widthT);
        DrawStraightLR(_lrCoreWhite, origin, end, coreWhiteWidth * widthT * thick, coreWhiteColor, widthT);
        DrawJitterLR(origin, end, widthT, thick);
    }

    void DrawStraightLR(LineRenderer lr, Vector3 from, Vector3 to, float width, Color col, float alphaMult)
    {
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = width;
        lr.endWidth   = width;
        lr.startColor = new Color(col.r, col.g, col.b, col.a * alphaMult);
        lr.endColor   = new Color(col.r, col.g, col.b, col.a * alphaMult);
    }

    void DrawJitterLR(Vector3 origin, Vector3 end, float widthT, float thickMult = 1f)
    {
        // Compute a perpendicular axis to the beam in 2D
        Vector3 dir  = (end - origin).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);

        float w    = jitterWidth * widthT * thickMult;  // thickness scales with upgrade
        Color jCol = new Color(jitterColor.r, jitterColor.g, jitterColor.b, jitterColor.a * widthT); // alpha envelope unchanged
        _lrJitter.startWidth = w;
        _lrJitter.endWidth   = w;
        _lrJitter.startColor = jCol;
        _lrJitter.endColor   = jCol;

        for (int i = 0; i <= jitterSegments; i++)
        {
            float   s      = (float)i / jitterSegments;
            // Perlin noise: varies along beam (s) and animates over time
            float   noise  = (Mathf.PerlinNoise(s * 5.5f + _jitterSeed, Time.time * jitterSpeed) - 0.5f) * 2f;
            // Amplitude peaks in the middle and tapers to zero at the ends (clean cap)
            float   taper  = Mathf.Sin(s * Mathf.PI);
            float   offset = noise * jitterAmplitude * taper * widthT;
            _lrJitter.SetPosition(i, Vector3.Lerp(origin, end, s) + perp * offset);
        }
    }

    void SetLaserVisible(bool on)
    {
        _lrOuter.enabled     = on;
        _lrMid.enabled       = on;
        _lrCoreBlue.enabled  = on;
        _lrCoreWhite.enabled = on;
        _lrJitter.enabled    = on;
    }

    // ── Charge particles ──────────────────────────────────────────────────────

    /// <summary>
    /// Crackling blue energy sparks at the koi's mouth during the charge-up phase.
    /// Parented to the troop so they follow it if it moves.
    /// </summary>
    void SpawnChargeParticles()
    {
        var go = new GameObject("KoiCharge_Particles");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * mouthOffset;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.30f, 0.70f, 1.00f),
                                   new Color(0.85f, 0.97f, 1.00f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 60;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.07f;

        // Fade out over lifetime
        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.30f, 0.70f, 1.00f), 0f),
                new GradientColorKey(new Color(0.85f, 0.97f, 1.00f), 0.5f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,  0f),
                new GradientAlphaKey(0.7f, 0.4f),
                new GradientAlphaKey(0f,  1f),
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
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder - 1;

        ps.Play();
        _chargePs = ps;
    }

    void StopChargeParticles(bool clearImmediate)
    {
        if (_chargePs == null) return;
        var stopBehavior = clearImmediate
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;
        _chargePs.Stop(true, stopBehavior);
        _chargePs = null;
    }

    // ── Muzzle flash VFX ─────────────────────────────────────────────────────

    void SpawnMuzzleFlash(Vector3 pos)
    {
        SpawnMuzzleParticles(pos);
        SpawnMuzzleRing(pos);
    }

    void SpawnMuzzleParticles(Vector3 pos)
    {
        var go = new GameObject("KoiLaser_MuzzleFlash");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.28f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.25f, 0.70f, 1.00f),
                                   Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 22;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.05f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.25f, 0.70f, 1.00f), 0f),
                new GradientColorKey(Color.white,                     0.4f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f),
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
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder + 5;

        ps.Play();
    }

    void SpawnMuzzleRing(Vector3 pos)
    {
        var go = new GameObject("KoiLaser_MuzzleRing");
        go.transform.position = pos;
        go.AddComponent<KoiLaserMuzzleRing>().Init(
            new Color(0.20f, 0.72f, 1.00f), sortingLayerName, sortingOrder + 4);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>World-space position of the koi's laser mouth (local +Y axis).</summary>
    Vector3 LaserOrigin() => transform.TransformPoint(Vector3.up * mouthOffset);

    void PlayIdle()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.Play(idleStateName);
    }

    void PlayAttack()
    {
        if (_animator != null && _animator.runtimeAnimatorController != null)
            _animator.Play(attackStateName);
    }

    // ── Easing functions ─────────────────────────────────────────────────────

    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
    static float EaseInCubic(float t)  => t * t * t;
}

// ── Muzzle ring VFX ───────────────────────────────────────────────────────────

/// <summary>
/// A fast-expanding neon ring that bursts at the laser origin when the beam fires.
/// Self-contained MonoBehaviour — destroys itself after ~0.28 s.
/// </summary>
public class KoiLaserMuzzleRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.28f;
    private const float MaxRadius  = 0.42f;
    private const int   Segments   = 28;
    private const float StartWidth = 0.07f;

    public void Init(Color color, string sortingLayer, int sortingOrder)
    {
        _color = color;
        _ring  = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = Segments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = StartWidth;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color              = color;
        _ring.material         = mat;
        _ring.sortingLayerName = sortingLayer;
        _ring.sortingOrder     = sortingOrder;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t      = Mathf.Clamp01(_timer / Duration);
        float radius = Mathf.Lerp(0f, MaxRadius, EaseOutQuart(t));
        float alpha  = Mathf.Lerp(1.0f, 0f, t);
        float width  = Mathf.Lerp(StartWidth, 0.004f, t);

        _ring.widthMultiplier = width;
        _ring.startColor = new Color(_color.r, _color.g, _color.b, alpha);
        _ring.endColor   = new Color(_color.r, _color.g, _color.b, alpha);

        Vector3 c = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _ring.SetPosition(i, c + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        if (t >= 1f) Destroy(gameObject);
    }

    static float EaseOutQuart(float t) { float f = 1f - t; return 1f - f * f * f * f; }
}
