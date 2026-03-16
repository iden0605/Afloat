using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DragonFly attack behaviour.
///
/// The DragonFly does NOT target individual enemies. Instead it:
///   • Flies in a continuous figure-8 loop centred on its placed position.
///   • Drops an egg bomb at its current position every attack interval.
///   • Each egg wobbles and flickers as its fuse burns, then explodes with
///     a bright flash + particle burst + expanding ring, dealing Splash
///     damage to every enemy within the explosion radius.
///
/// Requires TroopInstance (damage dispatcher) and TroopBehavior (required
/// by TroopInstance) but never reads CurrentTarget.
/// </summary>
[RequireComponent(typeof(TroopInstance))]
public class DragonFlyAttack : MonoBehaviour
{
    [Header("Figure-8 Flight")]
    [Tooltip("Half-width of the figure-8 loop in world units")]
    [SerializeField] private float figure8Width  = 1.8f;
    [Tooltip("Half-height of the figure-8 loop in world units")]
    [SerializeField] private float figure8Height = 0.9f;
    [Tooltip("Loop speed (radians per second). Higher = faster laps.")]
    [SerializeField] private float figure8Speed  = 1.2f;

    [Header("Egg Bomb")]
    [Tooltip("Radius in world units within which the explosion deals splash damage")]
    [SerializeField] private float splashRadius = 0.28f;
    [Tooltip("Seconds from drop to detonation")]
    [SerializeField] private float fuseTime     = 2.0f;

    [Header("Visuals")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 4;

    // ── Internal state ────────────────────────────────────────

    private TroopInstance _instance;
    private Vector3       _homePos;    // centre of the figure-8 pattern
    private float         _t;          // figure-8 angle parameter
    private float         _cooldown;

    // ── Lifecycle ─────────────────────────────────────────────

    void Awake()
    {
        _instance = GetComponent<TroopInstance>();

        // Stop TroopBehavior from rotating this troop toward enemies.
        // Target detection (EnemiesInRange / CurrentTarget) still runs for effect calculations.
        var behavior = GetComponent<TroopBehavior>();
        if (behavior != null) behavior.suppressRotation = true;
    }

    void Start()
    {
        _homePos = transform.position;
        // Stagger first drop so it doesn't fire the instant the troop is placed
        _cooldown = 0.5f;
    }

    void Update()
    {
        if (_instance.Data == null) return;

        _t += Time.deltaTime * figure8Speed;

        // Lemniscate-style figure-8:
        //   x = A · sin(t)
        //   y = B · sin(t) · cos(t)  [= B/2 · sin(2t)]
        transform.position = _homePos + new Vector3(
            figure8Width  * Mathf.Sin(_t),
            figure8Height * Mathf.Sin(_t) * Mathf.Cos(_t),
            0f);

        // Analytical velocity — d/dt of the lemniscate, exact at every point including
        // the curve extremes where finite-difference deltas would be tiny and noisy.
        //   dx/dt = A · cos(t)
        //   dy/dt = B · cos(2t)
        float vx = figure8Width  * Mathf.Cos(_t);
        float vy = figure8Height * Mathf.Cos(2f * _t);
        float angle = Mathf.Atan2(vy, vx) * Mathf.Rad2Deg;
        // -90° offset so a sprite pointing up (local +Y) aligns with travel direction
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f)
        {
            DropBombs();
            _cooldown = _instance.GetEffectiveAttackInterval();
        }
    }

    // ── Bomb drop ─────────────────────────────────────────────

    // U2 upgrade: drops 2 eggs per interval, slightly offset so they don't overlap.
    void DropBombs()
    {
        int count = _instance.UpgradeLevel >= 2 ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            float xOff = count > 1 ? (i == 0 ? -0.10f : 0.10f) : 0f;
            var go = new GameObject("DragonFlyEggBomb");
            go.transform.position = transform.position + new Vector3(xOff, 0f, 0f);

            var bomb = go.AddComponent<DragonFlyEggBomb>();
            bomb.Init(_instance, fuseTime, splashRadius, sortingLayerName, sortingOrder);
        }
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// Egg bomb
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Self-contained egg bomb spawned by DragonFlyAttack.
///
/// Visual layers (all procedural):
///   • Cream-white egg body  — oval mesh
///   • Highlight smudge      — small bright ellipse offset to upper-left
///   • Shadow tint           — darker ellipse offset below-right
///
/// Behaviour while fuse is active:
///   • Drifts slowly downward (falling feel)
///   • Wobbles (slow spin)
///   • Flickers increasingly fast as the fuse nears zero
///
/// On detonation:
///   • Bright yellow-white flash disk (quickly fades)
///   • Orange/red particle burst
///   • Expanding orange ring
///   • Physics2D overlap circle → DealDamage(Splash) on all enemies in radius
/// </summary>
public class DragonFlyEggBomb : MonoBehaviour
{
    // ── Set by Init() ─────────────────────────────────────────

    private TroopInstance _instance;
    private float         _fuseTime;
    private float         _splashRadius;
    private string        _sortingLayer;
    private int           _sortingOrder;

    // ── Runtime ───────────────────────────────────────────────

    private float      _timer;
    private bool       _exploded;
    private float      _spinAngle;
    private GameObject _eggRoot;

    // ── Colors ────────────────────────────────────────────────

    private static readonly Color EggBody      = new Color(0.97f, 0.95f, 0.87f, 1.00f); // cream white
    private static readonly Color EggHighlight = new Color(1.00f, 1.00f, 1.00f, 0.70f); // bright highlight
    private static readonly Color EggShadow    = new Color(0.70f, 0.65f, 0.50f, 0.55f); // warm shadow

    // ── Public API ────────────────────────────────────────────

    public void Init(TroopInstance instance, float fuseTime, float splashRadius,
                     string sortingLayer, int sortingOrder)
    {
        _instance     = instance;
        _fuseTime     = fuseTime;
        _splashRadius = splashRadius;
        _sortingLayer = sortingLayer;
        _sortingOrder = sortingOrder;

        BuildVisuals();
    }

    // ── Visuals ───────────────────────────────────────────────

    void BuildVisuals()
    {
        // Parent object that we spin — children share this rotation
        _eggRoot = new GameObject("EggRoot");
        _eggRoot.transform.SetParent(transform, false);

        // Main egg body
        BuildEllipse(_eggRoot, "EggBody",
                     rx: 0.11f, ry: 0.14f,
                     offset: Vector3.zero,
                     color: EggBody,
                     order: _sortingOrder);

        // Highlight — small ellipse, upper-left
        BuildEllipse(_eggRoot, "EggHighlight",
                     rx: 0.035f, ry: 0.045f,
                     offset: new Vector3(-0.035f, 0.045f, 0f),
                     color: EggHighlight,
                     order: _sortingOrder + 1);

        // Shadow — medium ellipse, lower-right
        BuildEllipse(_eggRoot, "EggShadow",
                     rx: 0.07f, ry: 0.09f,
                     offset: new Vector3(0.025f, -0.030f, 0f),
                     color: EggShadow,
                     order: _sortingOrder - 1);
    }

    void BuildEllipse(GameObject parent, string name, float rx, float ry,
                      Vector3 offset, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = offset;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.mesh = MakeEllipseMesh(rx, ry, 32);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mr.material         = mat;
        mr.sortingLayerName = _sortingLayer;
        mr.sortingOrder     = order;
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        if (_exploded) return;

        _timer += Time.deltaTime;

        // Gentle fall
        transform.position += Vector3.down * (0.25f * Time.deltaTime);

        // Wobble spin
        _spinAngle += Time.deltaTime * 160f; // ~160°/s
        _eggRoot.transform.localRotation = Quaternion.Euler(0f, 0f, _spinAngle);

        // Fuse flicker — starts at 60% elapsed, accelerates to rapid strobe at 100%
        float fuseRatio = _timer / _fuseTime;
        if (fuseRatio > 0.6f)
        {
            float speed   = Mathf.Lerp(5f, 22f, (fuseRatio - 0.6f) / 0.4f);
            bool  visible = Mathf.Sin(_timer * speed * Mathf.PI) > 0f;
            _eggRoot.SetActive(visible);
        }

        if (_timer >= _fuseTime)
            Explode();
    }

    // ── Explosion ─────────────────────────────────────────────

    void Explode()
    {
        _exploded = true;

        Vector3 pos = transform.position;

        // Damage — all enemies in splash radius
        var cols = Physics2D.OverlapCircleAll(pos, _splashRadius);
        var hit  = new HashSet<EnemyMovement>();
        foreach (var col in cols)
        {
            if (col.TryGetComponent<EnemyMovement>(out var em) && hit.Add(em))
                _instance.DealDamage(em, AttackType.Splash, pos);
        }

        // VFX
        SpawnFlash(pos);
        SpawnParticles(pos);
        SpawnRing(pos);

        Destroy(gameObject);
    }

    // ── VFX spawners ──────────────────────────────────────────

    void SpawnFlash(Vector3 pos)
    {
        var go = new GameObject("EggExplosion_Flash");
        go.transform.position = pos;
        go.AddComponent<EggExplosionFlash>().Init(_splashRadius * 0.75f, _sortingLayer, _sortingOrder + 3);
    }

    void SpawnParticles(Vector3 pos)
    {
        var go   = new GameObject("EggExplosion_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.20f, 0.45f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f,  2.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.85f, 0.10f), // bright yellow
                                   new Color(1.00f, 0.45f, 0.05f)); // orange
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 48;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.07f;

        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 0.95f, 0.40f), 0.00f),  // yellow-white
                new GradientColorKey(new Color(1.00f, 0.45f, 0.05f), 0.40f),  // orange
                new GradientColorKey(new Color(0.45f, 0.08f, 0.00f), 1.00f),  // dark red
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.00f),
                new GradientAlphaKey(0.8f, 0.50f),
                new GradientAlphaKey(0.0f, 1.00f),
            });
        colorLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = _sortingLayer;
        psr.sortingOrder     = _sortingOrder + 2;

        ps.Play();
    }

    void SpawnRing(Vector3 pos)
    {
        var go = new GameObject("EggExplosion_Ring");
        go.transform.position = pos;
        go.AddComponent<EggExplosionRing>().Init(_splashRadius, _sortingLayer, _sortingOrder + 1);
    }

    // ── Mesh helper ───────────────────────────────────────────

    static Mesh MakeEllipseMesh(float rx, float ry, int segments)
    {
        var verts = new Vector3[segments + 1];
        var tris  = new int[segments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = 2f * Mathf.PI * i / segments;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f);
        }
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }
}


// ─────────────────────────────────────────────────────────────────────────────
// Explosion ring VFX
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Expanding + fading ring spawned at egg detonation.
/// Orange → dark red, fades to transparent as it reaches max radius.
/// </summary>
public class EggExplosionRing : MonoBehaviour
{
    private LineRenderer _lr;
    private float        _maxRadius;
    private float        _timer;

    private const float Duration = 0.42f;
    private const int   Segments = 40;

    private static readonly Color RingStart = new Color(1.00f, 0.70f, 0.10f, 0.95f);
    private static readonly Color RingEnd   = new Color(0.85f, 0.25f, 0.02f, 0.00f);

    public void Init(float maxRadius, string sortingLayer, int sortingOrder)
    {
        _maxRadius = maxRadius;

        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace     = false;
        _lr.loop              = true;
        _lr.positionCount     = Segments;
        _lr.widthMultiplier   = 0.06f;
        _lr.numCapVertices    = 0;
        _lr.numCornerVertices = 0;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = RingStart;
        _lr.material         = mat;
        _lr.sortingLayerName = sortingLayer;
        _lr.sortingOrder     = sortingOrder;

        DrawRing(0f);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        float r     = Mathf.Lerp(0f, _maxRadius, EaseOutCubic(t));
        float alpha = Mathf.Lerp(0.95f, 0f, t);
        float width = Mathf.Lerp(1.0f, 0.15f, t);

        _lr.widthMultiplier = 0.11f * width;
        var c = Color.Lerp(RingStart, RingEnd, t);
        c.a = alpha;
        _lr.startColor = c;
        _lr.endColor   = c;

        DrawRing(r);

        if (t >= 1f) Destroy(gameObject);
    }

    void DrawRing(float r)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
        }
    }

    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
}


// ─────────────────────────────────────────────────────────────────────────────
// Explosion flash VFX
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Brief bright disk that pops at explosion centre, then quickly fades.
/// Gives the "bang" moment before the ring and particles reach the viewer's eye.
/// </summary>
public class EggExplosionFlash : MonoBehaviour
{
    private MeshRenderer _mr;
    private GameObject   _disk;
    private float        _timer;

    private const float Duration = 0.18f;

    private static readonly Color FlashColor = new Color(1.00f, 0.97f, 0.55f, 1.00f);

    public void Init(float radius, string sortingLayer, int sortingOrder)
    {
        _disk = new GameObject("FlashDisk");
        _disk.transform.SetParent(transform, false);

        var mf = _disk.AddComponent<MeshFilter>();
        _mr    = _disk.AddComponent<MeshRenderer>();
        mf.mesh = MakeDiskMesh(radius, 32);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = FlashColor;
        _mr.material         = mat;
        _mr.sortingLayerName = sortingLayer;
        _mr.sortingOrder     = sortingOrder;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        // Expand slightly and fade
        _disk.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.6f, t);
        var c = FlashColor;
        _mr.material.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1.0f, 0.0f, t));

        if (t >= 1f) Destroy(gameObject);
    }

    static Mesh MakeDiskMesh(float r, int segments)
    {
        var verts = new Vector3[segments + 1];
        var tris  = new int[segments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float a = 2f * Mathf.PI * i / segments;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
        for (int i = 0; i < segments; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % segments + 1;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }
}
