using UnityEngine;

/// <summary>
/// Drives procedural fire-aura VFX on the Praying Mantis and Giant Mantis.
///
/// War Frenzy (ConditionalAttackBuff) — crimson fire that grows with enemies in range:
///   2 enemies = small,  3 enemies = medium,  4+ enemies = large.
///
/// Rampage (RampingDoubleBuff) — red + blue bicolor fire that grows with stacks:
///   1 stack = small,  2 stacks = medium,  3 stacks = large.
///
/// Auto-added by MantisJumpAttack; no prefab setup required.
/// </summary>
public class MantisTroopAura : MonoBehaviour
{
    private TroopBehavior _behavior;
    private TroopInstance _instance;

    // War Frenzy — red fire ring (ConditionalAttackBuff)
    private ParticleSystem _warFrenzyPS;

    // Rampage — red and blue fire rings (RampingDoubleBuff)
    private ParticleSystem _rampageRedPS;
    private ParticleSystem _rampageBluePS;

    // Cache which effects are active so we don't query every frame
    private bool _hasWarFrenzy;
    private bool _hasRampage;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
    }

    void Start()
    {
        // Check which effects are active — safe to do in Start() after Initialize()
        _hasWarFrenzy = _instance != null && _instance.HasEffect(TroopEffectType.ConditionalAttackBuff);
        _hasRampage   = _instance != null && _instance.HasEffect(TroopEffectType.RampingDoubleBuff);
    }

    void Update()
    {
        // Re-check on evolution (EvolutionLevel change unlocks new effects)
        if (_instance != null)
        {
            _hasWarFrenzy = _instance.HasEffect(TroopEffectType.ConditionalAttackBuff);
            _hasRampage   = _instance.HasEffect(TroopEffectType.RampingDoubleBuff);
        }

        UpdateWarFrenzy();
        UpdateRampage();
    }

    // ── War Frenzy ────────────────────────────────────────────────────────────

    void UpdateWarFrenzy()
    {
        if (!_hasWarFrenzy)
        {
            StopPS(_warFrenzyPS);
            return;
        }

        int enemies = _behavior != null ? _behavior.EnemiesInRange : 0;
        int level   = enemies >= 4 ? 3 : enemies >= 3 ? 2 : enemies >= 2 ? 1 : 0;

        if (level == 0) { StopPS(_warFrenzyPS); return; }

        EnsureWarFrenzyPS();
        ApplyLevel(_warFrenzyPS, level, baseRadius: 0.18f);
        if (!_warFrenzyPS.isPlaying) _warFrenzyPS.Play();
    }

    // ── Rampage ───────────────────────────────────────────────────────────────

    void UpdateRampage()
    {
        if (!_hasRampage)
        {
            StopPS(_rampageRedPS);
            StopPS(_rampageBluePS);
            return;
        }

        int stacks = _instance != null ? _instance.RampingStackCount : 0;
        int level  = stacks >= 3 ? 3 : stacks >= 2 ? 2 : stacks >= 1 ? 1 : 0;

        if (level == 0) { StopPS(_rampageRedPS); StopPS(_rampageBluePS); return; }

        EnsureRampagePS();
        ApplyLevel(_rampageRedPS,  level, baseRadius: 0.22f);
        ApplyLevel(_rampageBluePS, level, baseRadius: 0.14f);
        if (!_rampageRedPS.isPlaying)  _rampageRedPS.Play();
        if (!_rampageBluePS.isPlaying) _rampageBluePS.Play();
    }

    // ── Level scaling ─────────────────────────────────────────────────────────

    static void ApplyLevel(ParticleSystem ps, int level, float baseRadius)
    {
        if (ps == null) return;

        var emission = ps.emission;
        emission.rateOverTime = level == 1 ? 4f : level == 2 ? 9f : 17f;

        var main = ps.main;
        float sizeMax = level == 1 ? 0.05f : level == 2 ? 0.09f : 0.13f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMax * 0.55f, sizeMax);

        var shape = ps.shape;
        shape.radius = baseRadius * (level == 1 ? 0.7f : level == 2 ? 1f : 1.35f);
    }

    static void StopPS(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // ── Particle system builders ──────────────────────────────────────────────

    void EnsureWarFrenzyPS()
    {
        if (_warFrenzyPS != null) return;
        _warFrenzyPS = BuildFirePS(
            "WarFrenzyAura",
            col1: new Color(1.00f, 0.08f, 0.03f),  // deep crimson
            col2: new Color(1.00f, 0.38f, 0.00f),  // burnt orange-red (still distinctly red)
            fadeMid: new Color(0.90f, 0.10f, 0.05f),
            radius: 0.18f,
            sortOrder: 3);
    }

    void EnsureRampagePS()
    {
        if (_rampageRedPS != null) return;

        _rampageRedPS = BuildFirePS(
            "RampageAura_Red",
            col1: new Color(1.00f, 0.08f, 0.03f),
            col2: new Color(1.00f, 0.42f, 0.00f),
            fadeMid: new Color(0.85f, 0.05f, 0.05f),
            radius: 0.22f,
            sortOrder: 4);

        _rampageBluePS = BuildFirePS(
            "RampageAura_Blue",
            col1: new Color(0.15f, 0.35f, 1.00f),  // vivid blue
            col2: new Color(0.55f, 0.78f, 1.00f),  // pale ice-blue
            fadeMid: new Color(0.20f, 0.50f, 1.00f),
            radius: 0.14f,
            sortOrder: 5);
    }

    ParticleSystem BuildFirePS(string goName, Color col1, Color col2, Color fadeMid,
                               float radius, int sortOrder)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.30f, 0.60f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.40f, 1.20f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.10f);
        main.startColor      = new ParticleSystem.MinMaxGradient(col1, col2);
        main.gravityModifier = -0.55f; // rise upward
        main.maxParticles    = 80;

        var emission = ps.emission;
        emission.rateOverTime = 9f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = radius;

        // Color fades from bright → mid-tone → transparent
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(col2,    0.00f),
                new GradientColorKey(fadeMid, 0.50f),
                new GradientColorKey(col1,    1.00f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(0.75f, 0.12f),
                new GradientAlphaKey(0.55f, 0.55f),
                new GradientAlphaKey(0.00f, 1.00f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Grow then shrink — flame tongue shape
        var sizeL = ps.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(
                                new Keyframe(0f, 0.25f),
                                new Keyframe(0.25f, 1f),
                                new Keyframe(1f, 0f)));

        // Additional upward drift so flames feel like they're rising
        // All three axes must use the same MinMaxCurve mode (TwoConstants here)
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.20f, 0.65f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = sortOrder;

        return ps;
    }
}
