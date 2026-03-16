using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural tongue attack for the Frog troop.
/// Draws the tongue with a LineRenderer — no sprites required.
/// Reads CurrentTarget from TroopBehavior and fires on the interval
/// defined by TroopInstance.CurrentAttackSpeed.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class FrogTongueAttack : MonoBehaviour
{
    [Header("Tongue Visuals")]
    [SerializeField] private Color  tongueColor     = new Color(0.88f, 0.27f, 0.36f, 1f);
    [SerializeField] private float  tongueBaseWidth = 0.13f;
    [SerializeField] private float  tongueTipWidth  = 0.045f;
    [SerializeField] private float  mouthOffset     = 0.30f;   // local +Y offset from troop centre
    [SerializeField] private int    tongueSegments  = 16;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 5;

    [Header("Attack Timing")]
    [SerializeField] private float extendDuration  = 0.13f;
    [SerializeField] private float retractDuration = 0.09f;

    // ── Internal state ───────────────────────────────────────

    private enum Phase { Idle, Extending, Retracting }

    private TroopBehavior    _behavior;
    private TroopInstance    _instance;
    private LineRenderer     _tongue;
    private Transform        _tipTransform;

    private Phase         _phase       = Phase.Idle;
    private float         _phaseTimer  = 0f;
    private float         _cooldown    = 0f;
    private EnemyMovement _lockedTarget;
    private Vector3       _lockedTipPos;

    // Tracks which enemies were hit on this strike so each is only registered once
    private readonly HashSet<EnemyMovement> _hitThisStrike = new();

    // ── Awake ────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        BuildTongue();
        BuildTipCollider();

        if (GetComponent<FrogPoisonDrool>() == null)
            gameObject.AddComponent<FrogPoisonDrool>().SetMouthOffset(mouthOffset);
    }

    void BuildTongue()
    {
        var go = new GameObject("Tongue");
        go.transform.SetParent(transform, false);

        _tongue = go.AddComponent<LineRenderer>();
        _tongue.useWorldSpace     = true;
        _tongue.positionCount     = tongueSegments + 1;
        _tongue.loop              = false;
        _tongue.numCapVertices    = 6;
        _tongue.numCornerVertices = 4;
        _tongue.widthCurve        = MakeWidthCurve();

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = tongueColor;
        _tongue.material         = mat;
        _tongue.sortingLayerName = sortingLayerName;
        _tongue.sortingOrder     = sortingOrder;
        _tongue.enabled          = false;
    }

    AnimationCurve MakeWidthCurve()
    {
        // Thick at the mouth (index 0), slight mid-bulge, tapers to a rounded tip
        var c = new AnimationCurve();
        c.AddKey(new Keyframe(0.00f, tongueBaseWidth,        0f,  -0.05f));
        c.AddKey(new Keyframe(0.30f, tongueBaseWidth * 0.85f, 0f,   0f));
        c.AddKey(new Keyframe(0.75f, tongueBaseWidth * 0.60f, 0f,   0f));
        c.AddKey(new Keyframe(1.00f, tongueTipWidth,         0f,   0f));
        return c;
    }

    void BuildTipCollider()
    {
        var go = new GameObject("TongueTip");
        go.transform.SetParent(transform, false);
        _tipTransform = go.transform;

        // Kinematic Rigidbody2D is required for OnTrigger callbacks to fire
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius    = 0.12f;
        col.isTrigger = true;

        var trigger = go.AddComponent<TongueTipTrigger>();
        trigger.Init(this);

        go.SetActive(false);
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        if (_phase == Phase.Idle)
        {
            if (_cooldown <= 0f && _behavior.CurrentTarget != null)
                BeginExtend();
            return;
        }

        _phaseTimer += Time.deltaTime;

        if (_phase == Phase.Extending)
        {
            // Track the live enemy while extending so the tongue leads into them
            if (_lockedTarget != null)
                _lockedTipPos = _lockedTarget.transform.position;

            float t = Mathf.Clamp01(_phaseTimer / extendDuration);
            DrawTongue(EaseOutQuart(t));

            if (t >= 1f) BeginRetract();
        }
        else // Retracting — tip position is fixed at where the enemy was hit
        {
            float t = Mathf.Clamp01(_phaseTimer / retractDuration);
            DrawTongue(EaseInQuart(1f - t));

            if (t >= 1f) EndAttack();
        }
    }

    void BeginExtend()
    {
        _lockedTarget  = _behavior.CurrentTarget;
        _lockedTipPos  = _lockedTarget.transform.position;
        _phase         = Phase.Extending;
        _phaseTimer    = 0f;
        _hitThisStrike.Clear();
        _tongue.enabled = true;
        _tipTransform.gameObject.SetActive(true);
    }

    void BeginRetract()
    {
        _phase      = Phase.Retracting;
        _phaseTimer = 0f;
        // Deactivate tip so no more hits register during retract
        _tipTransform.gameObject.SetActive(false);
    }

    void EndAttack()
    {
        _phase          = Phase.Idle;
        _tongue.enabled = false;
        _cooldown       = _instance.GetEffectiveAttackInterval();
    }

    // ── Drawing ───────────────────────────────────────────────

    void DrawTongue(float t)
    {
        // origin is the frog's mouth: mouthOffset units along local +Y (which TroopBehavior
        // keeps pointed toward the current target by rotating the whole transform)
        Vector3 origin = transform.TransformPoint(Vector3.up * mouthOffset);
        Vector3 tip    = Vector3.Lerp(origin, _lockedTipPos, t);

        _tipTransform.position = tip;

        Vector3 axis = tip - origin;
        float   len  = axis.magnitude;
        // Perpendicular in 2D for the mid-wiggle effect
        Vector3 perp = len > 0.001f
            ? new Vector3(-axis.y, axis.x, 0f).normalized
            : Vector3.right;

        for (int i = 0; i <= tongueSegments; i++)
        {
            float   s   = (float)i / tongueSegments;
            Vector3 pos = Vector3.Lerp(origin, tip, s);

            // Subtle sine-arc that fades out as tongue fully extends —
            // gives a fleshy, organic look during launch
            float wiggle = Mathf.Sin(s * Mathf.PI) * 0.055f * (1f - t);
            pos += perp * wiggle;

            _tongue.SetPosition(i, pos);
        }
    }

    // ── Hit registration (called by TongueTipTrigger) ─────────

    /// <summary>
    /// Called when the tongue tip's trigger collider overlaps an enemy.
    /// Each enemy is registered at most once per strike.
    /// Damage application is left for when the health system is implemented.
    /// </summary>
    public void OnTipHit(EnemyMovement enemy)
    {
        if (_phase != Phase.Extending) return;
        if (!_hitThisStrike.Add(enemy)) return; // already hit this strike

        _instance.DealDamage(enemy, _instance.Data?.attackType ?? AttackType.Ranged, transform.position);
        SpawnTongueSplat(_tipTransform.position);

        if (_instance.UpgradeLevel >= 3)
            SpawnShockwave(_tipTransform.position);
    }

    void SpawnShockwave(Vector3 pos)
    {
        // Two concentric ripple rings — inner fires immediately, outer with a slight delay
        var go1 = new GameObject("FrogShockwave_1");
        go1.transform.position = pos;
        go1.AddComponent<FrogShockwaveRing>().Init(_instance, delay: 0.00f, sortingLayerName, sortingOrder - 1);

        var go2 = new GameObject("FrogShockwave_2");
        go2.transform.position = pos;
        go2.AddComponent<FrogShockwaveRing>().Init(_instance, delay: 0.08f, sortingLayerName, sortingOrder - 1);
    }

    // ── Tongue tip VFX ────────────────────────────────────────

    void SpawnTongueSplat(Vector3 pos)
    {
        SpawnSplatParticles(pos);
        SpawnSplatRing(pos);
    }

    void SpawnSplatParticles(Vector3 pos)
    {
        var go   = new GameObject("TongueSplat_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.0f, 3.2f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   tongueColor,
                                   new Color(1.00f, 0.62f, 0.70f));
        main.gravityModifier = 0.4f;  // droplets fall slightly after impact
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 14;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.03f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(tongueColor, 0f), new(Color.white, 0.4f) },
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

    void SpawnSplatRing(Vector3 pos)
    {
        var go = new GameObject("TongueSplat_Ring");
        go.transform.position = pos;
        go.AddComponent<TongueSplatRing>().Init(tongueColor, sortingLayerName, sortingOrder);
    }

    // ── Easing functions ─────────────────────────────────────

    // Fast snap-out: slow deceleration as tongue reaches enemy
    static float EaseOutQuart(float t)
    {
        float f = 1f - t;
        return 1f - f * f * f * f;
    }

    // Accelerates back in — tongue snaps home quickly at the end
    static float EaseInQuart(float t) => t * t * t * t;
}

// ── Tongue splat ring ─────────────────────────────────────────────────────────

/// <summary>Tiny expanding ring at the tongue impact point. Self-destructs in ~0.18s.</summary>
public class TongueSplatRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.18f;
    private const float MaxRadius  = 0.18f;
    private const int   Segments   = 18;
    private const float StartWidth = 0.035f;

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
        float width  = Mathf.Lerp(StartWidth, 0.003f, t);

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

// ── Frog shockwave ring (U3+) ──────────────────────────────────────────────────

/// <summary>
/// A single expanding water-ripple ring spawned on each tongue hit at upgrade level 3+.
/// Two rings are spawned with a slight delay between them for a shockwave feel.
/// Each ring deals 50% of the frog's current attack as splash damage and applies
/// a slow to every enemy the wave front passes through (each enemy hit once per ring).
/// </summary>
public class FrogShockwaveRing : MonoBehaviour
{
    private TroopInstance _instance;
    private LineRenderer  _ring;
    private float         _delay;
    private float         _timer;
    private bool          _started;

    private const float Duration    = 0.45f;
    private const float MaxRadius   = 0.70f;
    private const int   Segments    = 32;
    private const float StartWidth  = 0.055f;
    private const float SlowDur     = 1.8f;
    private const float SlowFactor  = 0.45f;   // enemy moves at 45% speed

    private static readonly Color RingColor = new Color(0.50f, 0.88f, 1.00f, 0.80f);

    // Enemies already processed — each hit exactly once per ring
    private readonly System.Collections.Generic.HashSet<EnemyMovement> _hit = new();

    public void Init(TroopInstance instance, float delay, string sortingLayer, int sortingOrder)
    {
        _instance = instance;
        _delay    = delay;

        _ring = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = Segments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = StartWidth;
        _ring.enabled           = false;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color              = RingColor;
        _ring.material         = mat;
        _ring.sortingLayerName = sortingLayer;
        _ring.sortingOrder     = sortingOrder;
    }

    void Update()
    {
        _timer += Time.deltaTime;

        if (!_started)
        {
            if (_timer < _delay) return;
            _started      = true;
            _timer        = 0f;
            _ring.enabled = true;
        }

        float t      = Mathf.Clamp01(_timer / Duration);
        float radius = Mathf.Lerp(0f, MaxRadius, EaseOutCubic(t));
        float alpha  = Mathf.Lerp(0.70f, 0f, t);
        float width  = Mathf.Lerp(StartWidth, 0.004f, t);

        _ring.widthMultiplier = width;
        var c = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
        _ring.startColor = c;
        _ring.endColor   = c;

        Vector3 centre = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        if (_instance != null) CheckHits(centre, radius);

        if (t >= 1f) Destroy(gameObject);
    }

    // Enemies are detected once each as the expanding ring first reaches their distance.
    void CheckHits(Vector3 centre, float radius)
    {
        var cols = Physics2D.OverlapCircleAll(centre, radius);
        foreach (var col in cols)
        {
            if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
            if (!_hit.Add(em)) continue;  // already hit this enemy

            float dmg = _instance.CurrentAttack * 0.5f;
            em.TakeDamage(dmg, AttackType.Splash, centre);

            EnemyStatusEffects.ApplyFreeze(em.gameObject, SlowDur, SlowFactor);
            EnemySlowIndicator.Apply(em.gameObject, SlowDur);
        }
    }

    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f; }
}
