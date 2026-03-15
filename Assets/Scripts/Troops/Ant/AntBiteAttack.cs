using UnityEngine;

/// <summary>
/// Melee lunge-and-bite attack for Ant-family troops (Ant, Fire Ant, Bullet Ant).
///
/// Attack sequence per cycle:
///   1. Idle       — waits until cooldown expires and a target enters range.
///   2. Approaching — lunges quickly toward the locked-on target; aborts if the
///                    target is destroyed or strays too far from the ant's home tile.
///   3. Biting      — freezes in place, plays the attack animation, fires a particle
///                    burst + impact ring at the bite point, deals damage once.
///   4. Returning   — dashes back to the home position; immediately re-engages if a
///                    new target is in range AND the attack cooldown has already expired.
///
/// --- Animation / Evolution notes ---
/// Each ant prefab variant exposes its own animator state names as serialized fields:
///   • Ant Variant          → idleStateName = "AntIdle",       attackStateName = "AntAttack"
///   • Fire Ant Variant     → idleStateName = "FireAntIdle",   attackStateName = "FireAntAttack"
///   • Bullet Ant Variant   → idleStateName = "BulletAntIdle", attackStateName = "BulletAntAttack"
///
/// --- VFX / Evolution notes ---
/// biteColor is serialized per-prefab variant so each ant form can have its own impact look:
///   • Ant         → warm yellow-white  (generic mandible impact)
///   • Fire Ant    → orange-red         (fiery bite)
///   • Bullet Ant  → cyan-white         (powerful sting)
///
/// TroopInstance.Evolve() destroys the old prefab and instantiates the evolved one at
/// the same world position, so Start() correctly captures _homePosition for all forms.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class AntBiteAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation State Names (set per prefab variant)")]
    [Tooltip("Animator state name for idle on this variant (e.g. AntIdle, FireAntIdle, BulletAntIdle)")]
    [SerializeField] private string idleStateName   = "AntIdle";

    [Tooltip("Animator state name for attack on this variant (e.g. AntAttack, FireAntAttack, BulletAntAttack)")]
    [SerializeField] private string attackStateName = "AntAttack";

    [Header("Movement")]
    [Tooltip("Speed the ant lunges toward the enemy (units/sec)")]
    [SerializeField] private float lungeSpeed       = 7f;

    [Tooltip("Speed the ant walks back to its home tile (units/sec)")]
    [SerializeField] private float returnSpeed      = 5f;

    [Tooltip("Maximum distance (world units) the ant may travel from home before forcing a bite")]
    [SerializeField] private float maxLungeDistance = 0.4f;

    [Tooltip("Distance from the enemy at which the bite triggers (world units)")]
    [SerializeField] private float biteReach        = 0.15f;

    [Header("Bite")]
    [Tooltip("Seconds the attack animation plays before the ant starts returning. Match your clip length.")]
    [SerializeField] private float biteDuration     = 0.5f;

    [Header("Bite VFX")]
    [Tooltip("Primary colour of the impact burst. Set per prefab variant for each ant evolution.")]
    [SerializeField] private Color biteColor        = new Color(1.00f, 0.92f, 0.40f); // warm yellow default

    [Tooltip("Sorting layer name used for all VFX spawned by this ant.")]
    [SerializeField] private string vfxSortingLayer = "Default";

    [Tooltip("Sorting order for VFX (should sit above the ant sprite).")]
    [SerializeField] private int    vfxSortingOrder = 5;

    // ── Internal ──────────────────────────────────────────────────────────────

    private enum Phase { Idle, Approaching, Biting, Returning }

    private TroopBehavior _behavior;
    private TroopInstance _instance;
    private Animator      _animator;

    private Phase         _phase       = Phase.Idle;
    private float         _cooldown    = 0f;    // seconds until next attack may start
    private float         _biteTimer   = 0f;
    private bool          _damageDealt = false;

    private Vector3       _homePosition;         // world-space tile the ant was placed on
    private EnemyMovement _target;               // committed target for the current lunge

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        // Capture home position here — works for both fresh placement and post-evolution spawns
        // because Evolve() instantiates the new prefab at the same world position before Start() runs.
        _homePosition = transform.position;
        // Do not call PlayIdle() here — the Animator is not fully initialized yet in Start()
        // and the controller's default state (e.g. AntIdle) already plays automatically.
    }

    void OnDisable()
    {
        // Sold or otherwise disabled mid-attack — snap back cleanly so no ghost position exists.
        // Do NOT call PlayIdle() here: the Animator is already inactive when OnDisable fires
        // and Play() on an inactive animator generates a harmless but noisy Unity warning.
        transform.position = _homePosition;
        _phase             = Phase.Idle;
        _cooldown          = 0f;
    }

    // ── Per-frame ─────────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:        TickIdle();        break;
            case Phase.Approaching: TickApproaching(); break;
            case Phase.Biting:      TickBiting();      break;
            case Phase.Returning:   TickReturning();   break;
        }
    }

    void LateUpdate()
    {
        // TroopBehavior only rotates toward a CurrentTarget. During Returning the target is null,
        // so we rotate toward home ourselves in LateUpdate to guarantee this wins over Update order.
        if (_phase != Phase.Returning) return;

        Vector2 toHome = _homePosition - transform.position;
        if (toHome.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(toHome.y, toHome.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }

    // ── Phase: Idle ───────────────────────────────────────────────────────────

    void TickIdle()
    {
        if (_cooldown > 0f || _behavior.CurrentTarget == null) return;
        BeginApproach(_behavior.CurrentTarget);
    }

    // ── Phase: Approaching ────────────────────────────────────────────────────

    void BeginApproach(EnemyMovement target)
    {
        _phase  = Phase.Approaching;
        _target = target;
        PlayIdle();   // no separate walk clip; idle plays while lunging
    }

    void TickApproaching()
    {
        // --- Target validity check ---
        // Abort if the target was destroyed or has moved clearly out of the troop's range.
        if (!TargetStillValid())
        {
            // A different enemy may still be in range — re-lock if cooldown is ready.
            if (_behavior.CurrentTarget != null && _cooldown <= 0f)
            {
                BeginApproach(_behavior.CurrentTarget);
            }
            else
            {
                BeginReturn();
            }
            return;
        }

        // --- Trigger bite ---
        float distToTarget = Vector3.Distance(transform.position, _target.transform.position);
        float distFromHome = Vector3.Distance(transform.position, _homePosition);

        if (distToTarget <= biteReach || distFromHome >= maxLungeDistance)
        {
            BeginBite();
            return;
        }

        // --- Advance ---
        Vector3 dir = (_target.transform.position - transform.position).normalized;
        transform.position += dir * lungeSpeed * Time.deltaTime;
    }

    // ── Phase: Biting ─────────────────────────────────────────────────────────

    void BeginBite()
    {
        _phase       = Phase.Biting;
        _biteTimer   = 0f;
        _damageDealt = false;
        // Start the attack cooldown NOW so it ticks down during the bite+return.
        // This keeps attack rate predictable and lets the ant re-engage the moment
        // it gets back to its tile if the cooldown has already expired.
        _cooldown    = _instance.GetEffectiveAttackInterval();
        PlayAttack();
    }

    void TickBiting()
    {
        _biteTimer += Time.deltaTime;

        // Deal damage and fire VFX once on the first frame of the bite
        if (!_damageDealt)
        {
            _damageDealt = true;
            if (_target != null)
            {
                _instance.DealDamage(_target, _instance.Data?.attackType ?? AttackType.Melee, transform.position);
            }
            SpawnBiteVFX(transform.position);
        }

        if (_biteTimer >= biteDuration)
            BeginReturn();
    }

    // ── Phase: Returning ──────────────────────────────────────────────────────

    void BeginReturn()
    {
        _phase  = Phase.Returning;
        _target = null;
        PlayIdle();
    }

    void TickReturning()
    {
        // Re-engage immediately if a target is available and the cooldown has expired.
        // This fulfils the "attack while still walking back" requirement.
        if (_cooldown <= 0f && _behavior.CurrentTarget != null)
        {
            BeginApproach(_behavior.CurrentTarget);
            return;
        }

        // Move toward home
        Vector3 toHome = _homePosition - transform.position;
        if (toHome.sqrMagnitude <= 0.001f)
        {
            // Arrived — enter Idle (cooldown continues ticking from when the bite landed)
            transform.position = _homePosition;
            _phase             = Phase.Idle;
            return;
        }

        transform.position += toHome.normalized * returnSpeed * Time.deltaTime;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the committed target still exists and is within the troop's
    /// attack range as measured from the ant's home tile (not its lunged position).
    /// A small buffer prevents flickering at the exact range boundary.
    /// </summary>
    bool TargetStillValid()
    {
        if (_target == null) return false;
        float homeDist = Vector2.Distance(_homePosition, _target.transform.position);
        return homeDist <= _instance.CurrentRange * 1.15f;
    }

    // ── Animation ─────────────────────────────────────────────────────────────

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

    // ── Bite VFX ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a short-lived particle burst and a tiny impact ring at <paramref name="pos"/>.
    /// Both GameObjects are standalone (no parent) and destroy themselves when done.
    /// </summary>
    void SpawnBiteVFX(Vector3 pos)
    {
        SpawnBiteParticles(pos);
        SpawnImpactRing(pos);
    }

    /// <summary>
    /// Eight small particles burst outward in a circle — like cartoon mandible impact sparks.
    /// Lifetime and size are deliberately tiny so the effect reads as a quick, sharp bite.
    /// </summary>
    void SpawnBiteParticles(Vector3 pos)
    {
        var go = new GameObject("AntBite_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.2f, 3.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(biteColor, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 12;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        // Single burst of 8 particles on frame 0
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        // Emit from a tiny circle so particles don't all originate from the same pixel
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.03f;

        // Fade from biteColor → transparent as they travel outward
        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(biteColor, 0f),
                new GradientColorKey(Color.white, 0.5f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink to nothing as they fly
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(
                           1f, new AnimationCurve(
                               new Keyframe(0f, 1f),
                               new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = vfxSortingLayer;
        psr.sortingOrder     = vfxSortingOrder;

        ps.Play();
    }

    /// <summary>
    /// A very small ring that expands and fades over ~0.18 s, reinforcing the impact point.
    /// Kept tighter than the acid ring so it reads as a sharp bite rather than an explosion.
    /// </summary>
    void SpawnImpactRing(Vector3 pos)
    {
        var go = new GameObject("AntBite_Ring");
        go.transform.position = pos;
        go.AddComponent<AntBiteImpactRing>().Init(biteColor, vfxSortingLayer, vfxSortingOrder);
    }
}

// ── Impact ring helper ────────────────────────────────────────────────────────

/// <summary>
/// Standalone MonoBehaviour that draws a small expanding ring at the bite point
/// then destroys itself. Kept as a nested-file class (same .cs) so no extra asset is needed.
/// </summary>
public class AntBiteImpactRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.18f;
    private const float MaxRadius  = 0.22f;
    private const int   Segments   = 20;
    private const float StartWidth = 0.045f;

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
        float radius = Mathf.Lerp(0f, MaxRadius, t);
        float alpha  = Mathf.Lerp(0.9f, 0f, t);
        float width  = Mathf.Lerp(StartWidth, 0.005f, t);

        _ring.widthMultiplier = width;
        _ring.startColor      = new Color(_color.r, _color.g, _color.b, alpha);
        _ring.endColor        = new Color(_color.r, _color.g, _color.b, alpha);

        Vector3 centre = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _ring.SetPosition(i,
                centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
