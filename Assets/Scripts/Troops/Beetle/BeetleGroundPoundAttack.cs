using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground-pound attack for the Beetle troop.
///
/// Sequence per attack:
///   1. WindUp  — pauses idle anim, scales beetle up (cartoon "jump charge")
///   2. Slam    — scales back down fast; launches shockwave at slam moment
///   3. Recover — waits for shockwave to finish, then resumes idle anim + starts cooldown
///
/// The shockwave ring and its hit-collider are standalone scene GameObjects
/// (not children of the beetle) so the beetle's scale animation never corrupts
/// the collider radius.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class BeetleGroundPoundAttack : MonoBehaviour
{
    [Header("Jump / Slam Animation")]
    [Tooltip("Uniform scale multiplier at the peak of the jump — 1.3 = 30% bigger")]
    [SerializeField] private float windUpScale    = 1.30f;
    [Tooltip("Seconds to reach peak scale")]
    [SerializeField] private float windUpDuration = 0.28f;
    [Tooltip("Seconds to slam back down to base scale")]
    [SerializeField] private float slamDuration   = 0.10f;

    [Header("Shockwave Visuals")]
    [SerializeField] private Color  shockwaveColor    = new Color(0.55f, 0.35f, 0.10f, 1f);
    [SerializeField] private float  shockwaveWidth    = 0.12f;
    [SerializeField] private float  shockwaveDuration = 0.50f;
    [SerializeField] private int    shockwaveSegments = 48;
    [SerializeField] private string sortingLayerName  = "Default";
    [SerializeField] private int    sortingOrder      = 4;

    // ── Internal state ───────────────────────────────────────

    private enum Phase { Idle, WindUp, Slam, Recovering }

    private TroopBehavior _behavior;
    private TroopInstance _instance;
    private Animator      _animator;
    private Vector3       _baseScale;
    private SquashStretch _squash;
    private AttackTrail   _trail;

    private Phase _phase      = Phase.Idle;
    private float _phaseTimer = 0f;
    private float _cooldown   = 0f;

    // Shockwave — separate scene GOs so beetle scale doesn't affect collider radius
    private GameObject       _ringGO;
    private GameObject       _colliderGO;
    private LineRenderer     _ring;
    private CircleCollider2D _shockwaveCollider;

    private Vector3 _slamPos;
    private bool    _shockwaveActive = false;
    private float   _shockwaveTimer  = 0f;

    private readonly HashSet<EnemyMovement> _hitThisStrike = new();

    // ── Lifecycle ────────────────────────────────────────────

    void Awake()
    {
        _behavior  = GetComponent<TroopBehavior>();
        _instance  = GetComponent<TroopInstance>();
        _animator  = GetComponent<Animator>();
        _baseScale = transform.localScale;
        _squash    = GetComponent<SquashStretch>();
        _trail     = GetComponent<AttackTrail>();

        BuildRing();
        BuildCollider();
    }

    void OnDisable()
    {
        // Beetle was moved or sold mid-attack — clean up immediately
        EndShockwave();
        transform.localScale = _baseScale;
        if (_animator != null) _animator.speed = 1f;
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    void OnDestroy()
    {
        if (_ringGO     != null) Destroy(_ringGO);
        if (_colliderGO != null) Destroy(_colliderGO);
    }

    // ── Construction ─────────────────────────────────────────

    void BuildRing()
    {
        _ringGO = new GameObject("BeetleShockwaveRing");
        // No parent — lives in scene root so beetle scale has no effect on the visual

        _ring = _ringGO.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = shockwaveSegments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = shockwaveWidth;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = shockwaveColor;
        _ring.material         = mat;
        _ring.sortingLayerName = sortingLayerName;
        _ring.sortingOrder     = sortingOrder;

        _ringGO.SetActive(false);
    }

    void BuildCollider()
    {
        _colliderGO = new GameObject("BeetleShockwaveCollider");
        // No parent — radius must stay in world-space, independent of beetle scale

        // Kinematic Rigidbody2D required for OnTrigger callbacks to fire
        var rb = _colliderGO.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        _shockwaveCollider = _colliderGO.AddComponent<CircleCollider2D>();
        _shockwaveCollider.isTrigger = true;
        _shockwaveCollider.radius    = 0.01f;

        var trigger = _colliderGO.AddComponent<ShockwaveHitTrigger>();
        trigger.Init(this);

        _colliderGO.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;
        UpdatePhase();
        if (_shockwaveActive) UpdateShockwave();
    }

    void UpdatePhase()
    {
        if (_phase == Phase.Idle)
        {
            if (_cooldown <= 0f && _behavior.CurrentTarget != null)
                BeginWindUp();
            return;
        }

        _phaseTimer += Time.deltaTime;

        switch (_phase)
        {
            case Phase.WindUp:
            {
                float t = Mathf.Clamp01(_phaseTimer / windUpDuration);
                // EaseOutBack gives a slight overshoot — cartoon "charge" puff
                transform.localScale = Vector3.Lerp(_baseScale, _baseScale * windUpScale, EaseOutBack(t));
                if (t >= 1f) BeginSlam();
                break;
            }

            case Phase.Slam:
            {
                float t = Mathf.Clamp01(_phaseTimer / slamDuration);
                // EaseInCubic accelerates — snappy slam back to ground
                transform.localScale = Vector3.Lerp(_baseScale * windUpScale, _baseScale, EaseInCubic(t));
                if (t >= 1f) BeginRecover();
                break;
            }

            case Phase.Recovering:
            {
                // Hold until shockwave finishes, then idle + cooldown
                if (!_shockwaveActive)
                {
                    _phase    = Phase.Idle;
                    _cooldown = _instance.GetEffectiveAttackInterval();
                    if (_animator != null) _animator.speed = 1f;
                }
                break;
            }
        }
    }

    void BeginWindUp()
    {
        _phase      = Phase.WindUp;
        _phaseTimer = 0f;
        if (_animator != null) _animator.speed = 0f; // freeze idle animation
        _trail?.StartTrail(); // brief trail during the charged wind-up
    }

    void BeginSlam()
    {
        _phase      = Phase.Slam;
        _phaseTimer = 0f;
        _trail?.StopTrail();     // stop trail on slam
        _squash?.PunchLand();    // wide squash on impact
        LaunchShockwave();           // shockwave fires the instant the beetle starts slamming
    }

    void BeginRecover()
    {
        _phase = Phase.Recovering;
        transform.localScale = _baseScale; // snap to exact base — no float drift
    }

    // ── Shockwave ─────────────────────────────────────────────

    void LaunchShockwave()
    {
        _slamPos = new Vector3(transform.position.x, transform.position.y, 0f);
        _ringGO.transform.position     = _slamPos;
        _colliderGO.transform.position = _slamPos;

        _shockwaveActive = true;
        _shockwaveTimer  = 0f;
        _hitThisStrike.Clear();

        _shockwaveCollider.radius = 0.01f;
        _ringGO.SetActive(true);
        _colliderGO.SetActive(true);

        SpawnSlamDust(_slamPos);
    }

    // ── Slam dust VFX ─────────────────────────────────────────

    void SpawnSlamDust(Vector3 pos)
    {
        // Dirt/debris burst radiating outward from the impact point
        var go   = new GameObject("BeetleSlam_Dust");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.2f, 4.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.55f, 0.38f, 0.14f),   // earthy brown
                                   new Color(0.72f, 0.60f, 0.35f));  // sandy tan
        main.gravityModifier = 0.5f;  // debris falls under gravity
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 32;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(0.72f, 0.60f, 0.35f), 0f), new(new Color(0.55f, 0.38f, 0.14f), 1f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder + 1;

        ps.Play();
    }

    void UpdateShockwave()
    {
        _shockwaveTimer += Time.deltaTime;
        float t         = Mathf.Clamp01(_shockwaveTimer / shockwaveDuration);
        float curRadius = Mathf.Lerp(0f, _instance.CurrentRange, EaseOutCubic(t));
        float alpha     = Mathf.Lerp(0.85f, 0f, t);
        float widthMult = Mathf.Lerp(1f, 0.25f, t);

        // Animate the visual ring
        _ring.widthMultiplier = shockwaveWidth * widthMult;
        var c = shockwaveColor;
        _ring.startColor = new Color(c.r, c.g, c.b, alpha);
        _ring.endColor   = new Color(c.r, c.g, c.b, alpha);
        DrawRing(curRadius);

        // Grow collider to match wave frontier
        // OnTriggerEnter2D fires as each new enemy is engulfed; HashSet prevents double-hits
        _shockwaveCollider.radius = Mathf.Max(0.01f, curRadius);

        if (t >= 1f) EndShockwave();
    }

    void EndShockwave()
    {
        _shockwaveActive = false;
        if (_ringGO     != null) _ringGO.SetActive(false);
        if (_colliderGO != null) _colliderGO.SetActive(false);
    }

    void DrawRing(float radius)
    {
        for (int i = 0; i < shockwaveSegments; i++)
        {
            float a = 2f * Mathf.PI * i / shockwaveSegments;
            _ring.SetPosition(i, _slamPos + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    // ── Hit callback (called by ShockwaveHitTrigger) ──────────

    /// <summary>
    /// Called when the growing shockwave trigger first overlaps an enemy collider.
    /// Each enemy is registered at most once per slam via the HashSet.
    /// Damage application is ready for when health is implemented.
    /// </summary>
    public void OnShockwaveHit(EnemyMovement enemy)
    {
        if (!_shockwaveActive) return;
        if (!_hitThisStrike.Add(enemy)) return; // already hit this strike

        _instance.DealDamage(enemy, _instance.Data?.attackType ?? AttackType.Splash, transform.position);
    }

    // ── Easing ────────────────────────────────────────────────

    // Slight overshoot — cartoon springy wind-up feel
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Accelerating slam — fast at the end (heavy impact feel)
    static float EaseInCubic(float t) => t * t * t;

    // Decelerating shockwave expansion — fast burst, slows as it spreads
    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
}
