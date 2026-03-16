using UnityEngine;

/// <summary>
/// Dive-bomb attack for the Eagle troop.
///
/// In this top-down view, altitude is represented by scale:
///   • Larger sprite  = higher altitude (eagle circling overhead).
///   • Smaller sprite = lower altitude (eagle diving toward ground level).
///
/// Attack sequence per cycle:
///   1. Idle       — waits until cooldown expires and a target is in range.
///   2. Ascending  — eagle grows in scale while circling at home position,
///                   simulating it climbing to strike altitude. Wing-beat
///                   wind particles scatter from the body.
///   3. Diving     — eagle locks the target, then moves rapidly toward it
///                   while shrinking in scale (diving downward). A golden
///                   streak trail marks the dive path.
///   4. Impact     — eagle arrives at the target, deals damage, spawns a
///                   burst of feather/star particles and an expanding ring.
///   5. Returning  — eagle flies back to its home position, growing back
///                   to normal scale as it rises. Wing-beat particles resume.
///
/// A new attack cannot begin until the eagle has fully returned home.
///
/// --- Inspector notes ---
/// • ascendScale: scale multiplier at peak altitude before the dive (default 1.5).
/// • diveEndScale: scale multiplier at the moment of impact (default 0.5).
/// • All duration fields control phase lengths in seconds.
/// • sortingLayerName / sortingOrder should sit above the troop sprite.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class EagleAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Timing")]
    [Tooltip("Seconds the eagle grows in scale before diving.")]
    [SerializeField] private float ascendDuration      = 0.50f;

    [Tooltip("Seconds the eagle takes to reach the target.")]
    [SerializeField] private float diveDuration        = 0.35f;

    [Tooltip("Brief freeze at impact before flying home.")]
    [SerializeField] private float impactPauseDuration = 0.12f;

    [Tooltip("Seconds the eagle takes to return to its home position.")]
    [SerializeField] private float returnDuration      = 0.55f;

    [Header("Scale (altitude simulation)")]
    [Tooltip("Scale multiplier at the peak of the ascent (larger = higher up).")]
    [SerializeField] private float ascendScale  = 1.50f;

    [Tooltip("Scale multiplier at the moment of impact (smaller = lower / deep dive).")]
    [SerializeField] private float diveEndScale = 0.50f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 8;

    // ── Private state ─────────────────────────────────────────────────────────

    private enum Phase { Idle, Ascending, Diving, Impact, Returning }

    private TroopBehavior _behavior;
    private TroopInstance _instance;

    private Phase _phase      = Phase.Idle;
    private float _phaseTimer = 0f;
    private float _cooldown   = 0f;

    // Positions stored as world-space vectors
    private Vector3 _homePosition;       // placed position; set once in Start
    private Vector3 _homeScale;          // original localScale; set once in Start
    private Vector3 _diveTargetPosition; // world pos of locked target when dive begins
    private Vector3 _returnStartPosition;// world pos when the return leg begins

    private EnemyMovement _lockedTarget; // target committed to when diving starts

    // Live particle handles
    private ParticleSystem _wingParticles;  // wind feathers during ascent / return
    private ParticleSystem _trailParticles; // golden streak during dive

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
    }

    void Start()
    {
        // Capture home position after the troop has been fully placed.
        _homePosition = transform.position;
        _homeScale    = transform.localScale;

        // Start with a random partial cooldown so simultaneous placements
        // don't all attack in the same frame.
        _cooldown = Random.Range(0f, _instance.GetEffectiveAttackInterval());
    }

    void OnDisable()
    {
        // Sold or disabled mid-attack — snap back to home state cleanly.
        transform.position   = _homePosition;
        transform.localScale = _homeScale;
        _behavior.suppressRotation = false;
        StopWingParticles(clearImmediate: true);
        StopDiveTrail(clearImmediate: true);
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    // ── Update loop ───────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:      TickIdle();      break;
            case Phase.Ascending: TickAscending(); break;
            case Phase.Diving:    TickDiving();    break;
            case Phase.Impact:    TickImpact();    break;
            case Phase.Returning: TickReturning(); break;
        }
    }

    // ── Phase: Idle ───────────────────────────────────────────────────────────

    void TickIdle()
    {
        if (_cooldown > 0f || _behavior.CurrentTarget == null) return;
        BeginAscending();
    }

    // ── Phase: Ascending ──────────────────────────────────────────────────────

    void BeginAscending()
    {
        _phase      = Phase.Ascending;
        _phaseTimer = 0f;
        // TroopBehavior rotates the eagle toward the target while it circles.
        _behavior.suppressRotation = false;
        StartWingParticles();
    }

    void TickAscending()
    {
        _phaseTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_phaseTimer / ascendDuration);

        // Grow: eagle climbs to strike altitude.
        float s = Mathf.Lerp(1f, ascendScale, EaseOutCubic(t));
        transform.localScale = new Vector3(_homeScale.x * s, _homeScale.y * s, _homeScale.z);

        // Keep tracking the best target so the locked direction is as fresh as possible.
        if (_behavior.CurrentTarget != null)
            _lockedTarget = _behavior.CurrentTarget;

        if (_phaseTimer >= ascendDuration)
            BeginDiving();
    }

    // ── Phase: Diving ─────────────────────────────────────────────────────────

    void BeginDiving()
    {
        // From here on we control rotation manually.
        _behavior.suppressRotation = true;
        StopWingParticles(clearImmediate: false);

        // If the target disappeared before we could dive, skip straight home.
        if (_lockedTarget == null)
        {
            BeginReturning();
            return;
        }

        _phase              = Phase.Diving;
        _phaseTimer         = 0f;
        _diveTargetPosition = _lockedTarget.transform.position;

        RotateToward(_diveTargetPosition - transform.position);
        StartDiveTrail();
    }

    void TickDiving()
    {
        _phaseTimer += Time.deltaTime;
        float t     = Mathf.Clamp01(_phaseTimer / diveDuration);
        float eased = EaseInQuart(t);

        // Fly toward target (acceleration curve gives the feel of a sudden strike).
        transform.position = Vector3.Lerp(_homePosition, _diveTargetPosition, eased);

        // Shrink: eagle descends toward ground level.
        float s = Mathf.Lerp(ascendScale, diveEndScale, eased);
        transform.localScale = new Vector3(_homeScale.x * s, _homeScale.y * s, _homeScale.z);

        if (_phaseTimer >= diveDuration)
            BeginImpact();
    }

    // ── Phase: Impact ─────────────────────────────────────────────────────────

    void BeginImpact()
    {
        _phase      = Phase.Impact;
        _phaseTimer = 0f;

        StopDiveTrail(clearImmediate: false);

        // Deal damage — guard against the target dying mid-dive.
        if (_lockedTarget != null)
            _instance.DealDamage(
                _lockedTarget,
                _instance.Data?.attackType ?? AttackType.Melee,
                transform.position);

        SpawnImpactVFX(transform.position);
    }

    void TickImpact()
    {
        _phaseTimer += Time.deltaTime;
        if (_phaseTimer >= impactPauseDuration)
            BeginReturning();
    }

    // ── Phase: Returning ──────────────────────────────────────────────────────

    void BeginReturning()
    {
        _phase               = Phase.Returning;
        _phaseTimer          = 0f;
        _returnStartPosition = transform.position;

        RotateToward(_homePosition - transform.position);
        StartWingParticles();
    }

    void TickReturning()
    {
        _phaseTimer += Time.deltaTime;
        float t     = Mathf.Clamp01(_phaseTimer / returnDuration);
        float eased = EaseOutCubic(t);

        // Fly home (ease-out so the landing feels smooth).
        transform.position = Vector3.Lerp(_returnStartPosition, _homePosition, eased);

        // Grow: eagle climbs back to cruising altitude.
        float s = Mathf.Lerp(diveEndScale, 1f, eased);
        transform.localScale = new Vector3(_homeScale.x * s, _homeScale.y * s, _homeScale.z);

        if (_phaseTimer >= returnDuration)
            FinishReturn();
    }

    void FinishReturn()
    {
        // Snap to exact home state to avoid any floating-point drift.
        transform.position   = _homePosition;
        transform.localScale = _homeScale;

        _behavior.suppressRotation = false;
        StopWingParticles(clearImmediate: false);

        _cooldown = _instance.GetEffectiveAttackInterval();
        _phase    = Phase.Idle;
    }

    // ── Wing-beat particles (ascent & return) ─────────────────────────────────
    // Cream/white wind particles scatter outward from the eagle body, evoking
    // powerful wingbeats as it climbs or descends through the air.

    void StartWingParticles()
    {
        if (_wingParticles != null) return; // already running

        var go = new GameObject("Eagle_WingParticles");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.50f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.96f, 0.88f, 0.90f),  // warm cream
                                   new Color(1.00f, 1.00f, 1.00f, 0.70f)); // white
        main.gravityModifier = -0.08f; // slight lift
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.20f; // roughly spans the wing tips

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 0.95f, 0.80f), 0.0f),
                new GradientColorKey(new Color(1.00f, 1.00f, 1.00f), 0.3f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.8f, 0.0f),
                new GradientAlphaKey(0.4f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 0.4f),
                                   new Keyframe(0.2f, 1.0f),
                                   new Keyframe(1f, 0.0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder - 1;

        ps.Play();
        _wingParticles = ps;
    }

    void StopWingParticles(bool clearImmediate)
    {
        if (_wingParticles == null) return;
        _wingParticles.Stop(true, clearImmediate
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting);
        _wingParticles = null;
    }

    // ── Dive trail particles ───────────────────────────────────────────────────
    // Short-lived gold/white streaks emitted at the eagle's position during the
    // dive. Because simulationSpace is World the particles hang in the air,
    // leaving a visible streak behind the plummeting eagle.

    void StartDiveTrail()
    {
        var go = new GameObject("Eagle_DiveTrail");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.20f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.88f, 0.35f, 1.00f),  // bright gold
                                   new Color(1.00f, 1.00f, 0.80f, 0.80f)); // pale yellow
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 90f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 0.90f, 0.45f), 0.0f),
                new GradientColorKey(new Color(1.00f, 1.00f, 0.90f), 0.3f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0.0f),
                new GradientAlphaKey(0.0f, 1.0f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 1.0f),
                                   new Keyframe(1f, 0.0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder - 1;

        ps.Play();
        _trailParticles = ps;
    }

    void StopDiveTrail(bool clearImmediate)
    {
        if (_trailParticles == null) return;
        _trailParticles.Stop(true, clearImmediate
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting);
        _trailParticles = null;
    }

    // ── Impact VFX ────────────────────────────────────────────────────────────

    void SpawnImpactVFX(Vector3 pos)
    {
        SpawnImpactBurst(pos);
        SpawnImpactRing(pos);
    }

    // Burst of golden feather/star sparks that scatter on impact.
    void SpawnImpactBurst(Vector3 pos)
    {
        var go = new GameObject("Eagle_ImpactBurst");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.50f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.82f, 0.25f, 1.00f),  // deep gold
                                   new Color(1.00f, 0.97f, 0.78f, 1.00f)); // pale cream
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 32;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.06f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 0.90f, 0.35f), 0.0f),
                new GradientColorKey(new Color(1.00f, 1.00f, 0.80f), 0.3f),
                new GradientColorKey(new Color(0.85f, 0.65f, 0.30f), 1.0f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.7f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 1.0f),
                                   new Keyframe(1f, 0.0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder + 2;

        ps.Play();
    }

    // Spawns an EagleImpactRing component that expands and fades on its own.
    void SpawnImpactRing(Vector3 pos)
    {
        var go = new GameObject("Eagle_ImpactRing");
        go.transform.position = pos;
        go.AddComponent<EagleImpactRing>().Init(
            new Color(1.00f, 0.86f, 0.30f), sortingLayerName, sortingOrder + 1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Rotates the eagle to face a direction vector (sprite forward = local +Y).
    void RotateToward(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    // ── Easing functions ─────────────────────────────────────────────────────

    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
    static float EaseInQuart(float t)  => t * t * t * t;
}

// ── EagleImpactRing ───────────────────────────────────────────────────────────

/// <summary>
/// A fast-expanding golden ring that bursts outward on eagle impact.
/// Self-contained MonoBehaviour — destroys itself after ~0.40 s.
/// </summary>
public class EagleImpactRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.40f;
    private const float MaxRadius  = 0.60f;
    private const int   Segments   = 28;
    private const float StartWidth = 0.10f;

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
        float width  = Mathf.Lerp(StartWidth, 0.005f, t);

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
