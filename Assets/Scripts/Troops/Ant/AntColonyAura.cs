using UnityEngine;

/// <summary>
/// Procedural colony-aura VFX for all Ant family troops.
///
/// The aura is a ring of rising embers/sparks that grows with the number of
/// same-type allies within the ant's current range:
///   1-3 allies  = small (level 1)
///   4-7 allies  = medium (level 2)
///   8+ allies   = large  (level 3)
///
/// Colour varies by evolution tier so each form reads distinctly:
///   Ant (evo 0)        → warm green-amber  (organic colony glow)
///   Fire Ant (evo 1)   → orange-red        (fiery colony)
///   Bullet Ant (evo 2) → cyan-white        (kinetic swarm energy)
///
/// Auto-added by AntBiteAttack; no prefab setup required.
/// </summary>
public class AntColonyAura : MonoBehaviour
{
    private TroopInstance _instance;
    private ParticleSystem _auraPS;

    // Cached so we don't re-create the PS when the value fluctuates
    private bool _hasColonyEffect;
    private int  _cachedEvolutionLevel = -1;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _instance = GetComponent<TroopInstance>();
    }

    void Start()
    {
        if (_instance != null)
        {
            _hasColonyEffect   = HasColony();
            _cachedEvolutionLevel = _instance.EvolutionLevel;
        }
    }

    void Update()
    {
        if (_instance == null) return;

        // Rebuild PS if the troop evolved and the colour needs to change
        if (_instance.EvolutionLevel != _cachedEvolutionLevel)
        {
            DestroyPS();
            _cachedEvolutionLevel = _instance.EvolutionLevel;
        }

        _hasColonyEffect = HasColony();
        UpdateAura();
    }

    // ── Aura update ───────────────────────────────────────────────────────────

    void UpdateAura()
    {
        if (!_hasColonyEffect)
        {
            StopPS(_auraPS);
            return;
        }

        int allies = CountNearbyAllies();
        int level  = allies >= 8 ? 3 : allies >= 4 ? 2 : allies >= 1 ? 1 : 0;

        if (level == 0)
        {
            StopPS(_auraPS);
            return;
        }

        EnsureAuraPS();
        ApplyLevel(_auraPS, level);
        if (!_auraPS.isPlaying) _auraPS.Play();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    bool HasColony() =>
        _instance.HasEffect(TroopEffectType.AllyProximityBuff) ||
        _instance.HasEffect(TroopEffectType.AllySpeedBuff);

    int CountNearbyAllies()
    {
        if (_instance == null || TroopManager.Instance == null) return 0;
        float rangeSq = _instance.CurrentRange * _instance.CurrentRange;
        int   count   = 0;
        int   evoLevel = _instance.EvolutionLevel;
        foreach (var troop in TroopManager.Instance.PlacedTroops)
        {
            if (troop == _instance) continue;
            if (troop.Data != _instance.Data) continue;
            if (troop.EvolutionLevel != evoLevel) continue;  // only count same evo form
            if ((troop.transform.position - transform.position).sqrMagnitude <= rangeSq)
                count++;
        }
        return count;
    }

    // ── Level scaling ─────────────────────────────────────────────────────────

    static void ApplyLevel(ParticleSystem ps, int level)
    {
        if (ps == null) return;

        var emission = ps.emission;
        emission.rateOverTime = level == 1 ? 3f : level == 2 ? 7f : 13f;

        var main = ps.main;
        float sizeMax = level == 1 ? 0.04f : level == 2 ? 0.07f : 0.10f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMax * 0.55f, sizeMax);

        var shape = ps.shape;
        shape.radius = 0.11f * (level == 1 ? 0.7f : level == 2 ? 1f : 1.4f);
    }

    static void StopPS(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void DestroyPS()
    {
        if (_auraPS != null)
        {
            Destroy(_auraPS.gameObject);
            _auraPS = null;
        }
    }

    // ── PS builder ────────────────────────────────────────────────────────────

    void EnsureAuraPS()
    {
        if (_auraPS != null) return;

        // Pick colour by evolution tier
        Color col1, col2, fadeMid;
        int evo = _instance != null ? _instance.EvolutionLevel : 0;

        if (evo >= 2)
        {
            // Bullet Ant — cyan-white kinetic energy
            col1    = new Color(0.25f, 0.85f, 1.00f);
            col2    = new Color(0.75f, 0.95f, 1.00f);
            fadeMid = new Color(0.35f, 0.80f, 1.00f);
        }
        else if (evo == 1)
        {
            // Fire Ant — orange-red ember glow
            col1    = new Color(1.00f, 0.35f, 0.05f);
            col2    = new Color(1.00f, 0.72f, 0.10f);
            fadeMid = new Color(0.90f, 0.28f, 0.04f);
        }
        else
        {
            // Ant — warm green-amber colony glow
            col1    = new Color(0.35f, 0.90f, 0.25f);
            col2    = new Color(0.80f, 1.00f, 0.35f);
            fadeMid = new Color(0.45f, 0.85f, 0.20f);
        }

        _auraPS = BuildAuraPS("ColonyAura", col1, col2, fadeMid);
    }

    ParticleSystem BuildAuraPS(string goName, Color col1, Color col2, Color fadeMid)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.15f, 0.70f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(col1, col2);
        main.gravityModifier = -0.35f;  // rise upward
        main.maxParticles    = 70;

        var emission = ps.emission;
        emission.rateOverTime = 7f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.14f;

        // Fade from bright → mid → transparent
        var colLife = ps.colorOverLifetime;
        colLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(col2,    0.00f),
                new GradientColorKey(fadeMid, 0.50f),
                new GradientColorKey(col1,    1.00f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(0.60f, 0.15f),
                new GradientAlphaKey(0.40f, 0.60f),
                new GradientAlphaKey(0.00f, 1.00f)
            });
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        // Grow then shrink
        var sizeL = ps.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(
                                new Keyframe(0f,    0.20f),
                                new Keyframe(0.25f, 1.00f),
                                new Keyframe(1f,    0.00f)));

        // Upward drift — all axes must share the same MinMaxCurve mode
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(0f,    0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.12f, 0.40f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f,    0f);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 2;

        return ps;
    }
}
