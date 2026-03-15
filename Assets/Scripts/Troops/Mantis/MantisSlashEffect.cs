using UnityEngine;

/// <summary>
/// One-shot slash impact effect for the Praying Mantis attack.
///
/// Visual layers:
///   • 3 slash streaks — LineRenderer lines fanning out in the attack direction,
///     expanding and fading like claw marks slicing through the air.
///   • Particle burst  — small sharp sparks scattering from the impact point.
///   • Impact ring     — tiny LineRenderer ring expanding outward and fading.
///
/// Spawned via MantisSlashEffect.Spawn(). Self-destructs when all visuals finish.
/// </summary>
public class MantisSlashEffect : MonoBehaviour
{
    // ── Colors ────────────────────────────────────────────────────────────────

    // Sharp lime-white — mantis claw feel
    private static readonly Color SlashColor  = new Color(0.78f, 1.00f, 0.48f, 1.00f);
    private static readonly Color RingColor   = new Color(0.60f, 1.00f, 0.30f, 1.00f);

    // ── Tuning ────────────────────────────────────────────────────────────────

    private const float SlashDuration  = 0.22f;   // how long each slash streak lives
    private const float RingDuration   = 0.20f;   // how long the impact ring lives
    private const float SlashMaxLen    = 0.40f;   // world units at full extension
    private const float SlashBaseWidth = 0.07f;
    private const float RingMaxRadius  = 0.28f;
    private const int   RingSegments   = 28;

    // Angles (degrees) of each slash relative to the attack direction
    private static readonly float[] SlashAngles = { -28f, 0f, 28f };

    // ── Runtime ───────────────────────────────────────────────────────────────

    private float       _timer;
    private bool        _done;

    private LineRenderer[] _slashes;
    private LineRenderer   _ring;
    private Vector3        _attackDir;  // unit vector in the attack direction

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiate and start the effect at <paramref name="pos"/> pointing in
    /// <paramref name="attackDirection"/> (world-space, should be normalised).
    /// </summary>
    public static void Spawn(Vector3 pos, Vector2 attackDirection,
                             string sortingLayer = "Default", int sortingOrder = 5)
    {
        var go     = new GameObject("MantisSlashEffect");
        go.transform.position = pos;

        var effect = go.AddComponent<MantisSlashEffect>();
        effect.Init(attackDirection, sortingLayer, sortingOrder);
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    void Init(Vector2 attackDir, string sortingLayer, int sortingOrder)
    {
        _attackDir = new Vector3(attackDir.x, attackDir.y, 0f).normalized;

        BuildSlashes(sortingLayer, sortingOrder);
        BuildRing(sortingLayer, sortingOrder - 1);
        SpawnParticles(transform.position, sortingLayer, sortingOrder + 1);
    }

    void BuildSlashes(string sortingLayer, int sortingOrder)
    {
        _slashes = new LineRenderer[SlashAngles.Length];

        for (int i = 0; i < SlashAngles.Length; i++)
        {
            var go = new GameObject($"Slash_{i}");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.positionCount     = 2;
            lr.numCapVertices    = 2;
            lr.numCornerVertices = 0;

            // Width curve: thick at the root, tapers to a point
            var wc = new AnimationCurve();
            wc.AddKey(0f, 1f);
            wc.AddKey(1f, 0f);
            lr.widthCurve       = wc;
            lr.widthMultiplier  = SlashBaseWidth;

            lr.startColor = SlashColor;
            lr.endColor   = new Color(SlashColor.r, SlashColor.g, SlashColor.b, 0f);

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color            = Color.white;
            lr.material          = mat;
            lr.sortingLayerName  = sortingLayer;
            lr.sortingOrder      = sortingOrder;

            // Both points start at origin; Update() will move the tip outward
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, transform.position);

            _slashes[i] = lr;
        }
    }

    void BuildRing(string sortingLayer, int sortingOrder)
    {
        var go = new GameObject("SlashRing");
        go.transform.SetParent(transform, false);

        _ring = go.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = RingSegments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = 0.04f;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = Color.white;
        _ring.material       = mat;
        _ring.sortingLayerName = sortingLayer;
        _ring.sortingOrder     = sortingOrder;
    }

    static void SpawnParticles(Vector3 pos, string sortingLayer, int sortingOrder)
    {
        var go = new GameObject("MantisSlash_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.0f, 5.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.85f, 1.00f, 0.55f),
                                   new Color(1.00f, 1.00f, 0.85f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 20;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.03f;

        // Fade from sharp white-lime to transparent
        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.00f, 1.00f, 0.80f), 0f),
                new GradientColorKey(new Color(0.65f, 1.00f, 0.35f), 1f)
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
        psr.sortingLayerName = sortingLayer;
        psr.sortingOrder     = sortingOrder;

        ps.Play();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_done) return;

        _timer += Time.deltaTime;

        UpdateSlashes();
        UpdateRing();

        // Self-destruct after the longest effect finishes
        float maxDuration = Mathf.Max(SlashDuration, RingDuration);
        if (_timer >= maxDuration)
        {
            _done = true;
            Destroy(gameObject);
        }
    }

    void UpdateSlashes()
    {
        float t     = Mathf.Clamp01(_timer / SlashDuration);
        float len   = Mathf.Lerp(0f, SlashMaxLen, EaseOutQuart(t));
        float alpha = Mathf.Lerp(1f, 0f, t);
        float width = Mathf.Lerp(SlashBaseWidth, SlashBaseWidth * 0.3f, t);

        for (int i = 0; i < _slashes.Length; i++)
        {
            // Rotate attack direction by each slash angle
            float deg = SlashAngles[i];
            var dir   = RotateVector2(_attackDir, deg * Mathf.Deg2Rad);
            var tip   = transform.position + dir * len;

            _slashes[i].SetPosition(0, transform.position);
            _slashes[i].SetPosition(1, tip);
            _slashes[i].widthMultiplier = width;

            var c = SlashColor;
            _slashes[i].startColor = new Color(c.r, c.g, c.b, alpha);
            _slashes[i].endColor   = new Color(c.r, c.g, c.b, 0f);
        }
    }

    void UpdateRing()
    {
        float t      = Mathf.Clamp01(_timer / RingDuration);
        float radius = Mathf.Lerp(0f, RingMaxRadius, EaseOutCubic(t));
        float alpha  = Mathf.Lerp(0.75f, 0f, t);
        float width  = Mathf.Lerp(0.04f, 0.008f, t);

        _ring.widthMultiplier = width;
        _ring.startColor = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
        _ring.endColor   = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);

        Vector3 centre = transform.position;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = 2f * Mathf.PI * i / RingSegments;
            _ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a) * radius,
                                                       Mathf.Sin(a) * radius, 0f));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Vector3 RotateVector2(Vector3 v, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector3(v.x * cos - v.y * sin, v.x * sin + v.y * cos, 0f);
    }

    static float EaseOutQuart(float t) { float f = 1f - t; return 1f - f * f * f * f; }
    static float EaseOutCubic(float t) { float f = 1f - t; return 1f - f * f * f;     }
}
