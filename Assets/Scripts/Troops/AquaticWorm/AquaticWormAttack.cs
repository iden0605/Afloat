using UnityEngine;

/// <summary>
/// Attack behaviour for the Aquatic Worm troop.
///
/// State machine:
///   Idle       — at home tile, waiting for an enemy to enter range.
///   Moving     — swimming toward the locked target; plays idle anim; faces movement direction.
///                Range is always measured from the HOME tile, not the worm's current position.
///   Attacking  — at the enemy; pauses motion; plays attack anim; deals damage + spawns VFX.
///   Returning  — no valid targets remain; swims back to home tile.
///
/// Chaining:
///   After finishing an attack the worm immediately looks for the next enemy
///   in HOME range and swims to it without returning first. If the cooldown
///   hasn't expired yet the worm waits at the enemy's position until it can fire.
///   Only when no enemy is within HOME range does it return to the tile.
///
/// Rotation / animation:
///   TroopBehavior.Update() rotates toward its CurrentTarget; LateUpdate() in this
///   script overrides that rotation during Moving and Returning so the worm always
///   faces its actual direction of travel.
///   The idle animation plays during movement; the attack animation plays on strike.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class AquaticWormAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Animation State Names (set per prefab variant)")]
    [SerializeField] private string idleStateName   = "AquaticWormIdle";
    [SerializeField] private string attackStateName = "AquaticWormAttack";

    [Header("Movement")]
    [Tooltip("Speed the worm swims toward or away from a target (world units/sec).")]
    [SerializeField] private float moveSpeed   = 3.5f;
    [Tooltip("Distance from the enemy centre at which the attack triggers.")]
    [SerializeField] private float attackReach = 0.18f;
    [Tooltip("Rotation offset (degrees) to correct for sprite facing direction.\n" +
             "-90 = sprite faces up (+Y). Adjust if your sprite faces a different way.")]
    [SerializeField] private float rotationOffset = -90f;

    [Header("Attack")]
    [Tooltip("How long the attack animation holds before the worm looks for a new target.")]
    [SerializeField] private float attackDuration = 0.45f;

    [Header("Layer")]
    [Tooltip("Must match the enemy layer set in TroopBehavior.")]
    [SerializeField] private LayerMask enemyLayer;
    [Tooltip("Sorting order for the worm's SpriteRenderer — set higher than enemy sprites so it renders on top.")]
    [SerializeField] private int sortingOrder = 10;

    [Header("VFX")]
    [Tooltip("Burst colour — default aqua/teal fits the aquatic theme.")]
    [SerializeField] private Color  particleColor    = new Color(0.25f, 0.90f, 0.75f);
    [SerializeField] private string vfxSortingLayer  = "Default";
    [SerializeField] private int    vfxSortingOrder  = 5;

    // ── State ─────────────────────────────────────────────────────────────────

    private enum Phase { Idle, Moving, Attacking, Returning }

    private TroopBehavior  _behavior;
    private TroopInstance  _instance;
    private Animator       _animator;
    private SpriteRenderer _spriteRenderer;

    private Phase         _phase       = Phase.Idle;
    private float         _cooldown    = 0f;
    private float         _attackTimer = 0f;
    private bool          _damageDealt = false;

    private Vector3       _homePosition;
    private EnemyMovement _target;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior       = GetComponent<TroopBehavior>();
        _instance       = GetComponent<TroopInstance>();
        _animator       = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _spriteRenderer.sortingOrder = sortingOrder;
    }

    void Start()
    {
        _homePosition = transform.position;
    }

    void OnDisable()
    {
        transform.position = _homePosition;
        transform.rotation = Quaternion.identity;
        _phase    = Phase.Idle;
        _cooldown = 0f;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Idle:      TickIdle();      break;
            case Phase.Moving:    TickMoving();    break;
            case Phase.Attacking: TickAttacking(); break;
            case Phase.Returning: TickReturning(); break;
        }
    }

    /// <summary>
    /// Overrides TroopBehavior's rotation during movement phases so the worm
    /// always faces the direction it is actually swimming.
    /// </summary>
    void LateUpdate()
    {
        switch (_phase)
        {
            case Phase.Moving:
            case Phase.Attacking:
                if (_target != null)
                    FaceToward(_target.transform.position);
                break;

            case Phase.Returning:
                FaceToward(_homePosition);
                break;
        }
    }

    // ── Phase: Idle ───────────────────────────────────────────────────────────

    void TickIdle()
    {
        // TroopBehavior detects enemies from the worm's current position.
        // In Idle the worm is at home, so CurrentTarget == nearest enemy in home range.
        if (_cooldown > 0f || _behavior.CurrentTarget == null) return;
        BeginMove(_behavior.CurrentTarget);
    }

    // ── Phase: Moving ─────────────────────────────────────────────────────────

    void BeginMove(EnemyMovement target)
    {
        _target = target;
        _phase  = Phase.Moving;
        PlayIdle();
    }

    void TickMoving()
    {
        // Re-validate: target must still exist and be within HOME range
        if (!IsInHomeRange(_target))
        {
            var next = FindBestInHomeRange();
            if (next != null) _target = next;
            else { BeginReturn(); return; }
        }

        float dist = Vector3.Distance(transform.position, _target.transform.position);

        if (dist <= attackReach)
        {
            // Arrived — attack immediately if cooldown is ready, otherwise wait here
            if (_cooldown <= 0f)
                BeginAttack();
            // If cooldown is still ticking we stay put (LateUpdate keeps us facing the target)
        }
        else
        {
            Vector3 dir = (_target.transform.position - transform.position).normalized;
            transform.position += dir * moveSpeed * Time.deltaTime;
        }
    }

    // ── Phase: Attacking ──────────────────────────────────────────────────────

    void BeginAttack()
    {
        _phase       = Phase.Attacking;
        _attackTimer = 0f;
        _damageDealt = false;
        _cooldown    = _instance.GetEffectiveAttackInterval();
        PlayAttack();
    }

    void TickAttacking()
    {
        _attackTimer += Time.deltaTime;

        // Deal damage + VFX once, on the first frame of the attack
        if (!_damageDealt)
        {
            _damageDealt = true;
            if (_target != null)
                _instance.DealDamage(_target, _instance.Data?.attackType ?? AttackType.Melee, transform.position);
            SpawnAttackVFX(transform.position);
        }

        if (_attackTimer >= attackDuration)
            EndAttack();
    }

    void EndAttack()
    {
        // Chain: find the best enemy in HOME range and swim to it directly,
        // skipping the return trip. Cooldown will expire during the swim.
        var next = FindBestInHomeRange();
        if (next != null)
            BeginMove(next);
        else
            BeginReturn();
    }

    // ── Phase: Returning ──────────────────────────────────────────────────────

    void BeginReturn()
    {
        _target = null;
        _phase  = Phase.Returning;
        PlayIdle();
    }

    void TickReturning()
    {
        // If an enemy enters home range and cooldown has expired, re-engage
        if (_cooldown <= 0f)
        {
            var next = FindBestInHomeRange();
            if (next != null) { BeginMove(next); return; }
        }

        // Swim home
        Vector3 toHome = _homePosition - transform.position;
        if (toHome.sqrMagnitude <= 0.001f)
        {
            transform.position = _homePosition;
            _phase = Phase.Idle;
            return;
        }

        transform.position += toHome.normalized * moveSpeed * Time.deltaTime;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// True if <paramref name="enemy"/> exists and is within the worm's range
    /// as measured from HOME — so the effective area never drifts as the worm moves.
    /// A 10% buffer prevents flickering at the exact boundary.
    /// </summary>
    bool IsInHomeRange(EnemyMovement enemy)
    {
        if (enemy == null) return false;
        return Vector2.Distance(_homePosition, enemy.transform.position)
               <= _instance.CurrentRange * 1.1f;
    }

    /// <summary>
    /// Scans enemies within HOME range and returns the one furthest along the
    /// path ("First" targeting — same logic as TroopBehavior).
    /// </summary>
    EnemyMovement FindBestInHomeRange()
    {
        var hits = Physics2D.OverlapCircleAll(_homePosition, _instance.CurrentRange, enemyLayer);
        EnemyMovement best   = null;
        int           bestIdx = -1;

        foreach (var col in hits)
        {
            if (col.TryGetComponent<EnemyMovement>(out var em)
                && em.currentWaypointIndex > bestIdx)
            {
                bestIdx = em.currentWaypointIndex;
                best    = em;
            }
        }
        return best;
    }

    void FaceToward(Vector3 targetPos)
    {
        Vector2 dir = targetPos - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
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

    // ── VFX ───────────────────────────────────────────────────────────────────

    void SpawnAttackVFX(Vector3 pos)
    {
        SpawnBurst(pos);
        SpawnRing(pos);
    }

    /// <summary>Outward particle burst — aquatic splash feel.</summary>
    void SpawnBurst(Vector3 pos)
    {
        var go = new GameObject("WormAttack_Burst");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.30f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(particleColor, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 16;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

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
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(Color.white,   0.6f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

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

    /// <summary>Small expanding ring at the impact point.</summary>
    void SpawnRing(Vector3 pos)
    {
        var go = new GameObject("WormAttack_Ring");
        go.transform.position = pos;
        go.AddComponent<WormImpactRing>().Init(particleColor, vfxSortingLayer, vfxSortingOrder);
    }
}

// ── Impact ring ───────────────────────────────────────────────────────────────

/// <summary>
/// A small expanding ring that fades out over ~0.20 s, placed at the bite point.
/// Self-destructs when done.
/// </summary>
public class WormImpactRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.20f;
    private const float MaxRadius  = 0.20f;
    private const int   Segments   = 20;
    private const float StartWidth = 0.04f;

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
        float alpha  = Mathf.Lerp(0.85f, 0f, t);
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
}
