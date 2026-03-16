using UnityEngine;

/// <summary>
/// Subtle dark-purple drool particles emitted from the frog's mouth.
/// Only active when the frog is a Poison Frog (EvolutionLevel >= 1).
///
/// Scales with upgrade level beyond evolution:
///   UpgradeLevel 3  (T4) — very sparse, tiny droplets
///   UpgradeLevel 4  (T5) — slightly denser / larger
///   UpgradeLevel 5  (T6) — most visible, still subtle
///
/// Auto-added by FrogTongueAttack; no prefab setup required.
/// </summary>
public class FrogPoisonDrool : MonoBehaviour
{
    private TroopInstance  _instance;
    private ParticleSystem _droolPS;
    private Transform      _mouthAnchor;   // child that follows frog rotation

    private float _mouthOffset = 0.30f;
    private int   _lastLevel   = -1;       // cache to avoid rebuilding every frame

    // ── Setup ─────────────────────────────────────────────────────────────────

    public void SetMouthOffset(float offset) => _mouthOffset = offset;

    void Awake()
    {
        _instance = GetComponent<TroopInstance>();
    }

    void Start()
    {
        // Child anchor at mouth position — follows frog rotation automatically
        var anchor = new GameObject("DroolAnchor");
        anchor.transform.SetParent(transform, false);
        anchor.transform.localPosition = new Vector3(0f, _mouthOffset, 0f);
        _mouthAnchor = anchor.transform;

        BuildDroolPS();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_instance == null || _droolPS == null) return;

        bool isPoisonFrog = _instance.EvolutionLevel >= 1;
        int  level        = DroolLevel();

        if (!isPoisonFrog || level == 0)
        {
            if (_droolPS.isPlaying) _droolPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _lastLevel = 0;
            return;
        }

        if (level != _lastLevel)
        {
            ApplyLevel(level);
            _lastLevel = level;
        }

        if (!_droolPS.isPlaying) _droolPS.Play();
    }

    // ── Level mapping ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns drool intensity level 1-3 based on upgrade tier,
    /// or 0 if no drool should show.
    /// T4 = UpgradeLevel 3 → level 1
    /// T5 = UpgradeLevel 4 → level 2
    /// T6 = UpgradeLevel 5 → level 3
    /// </summary>
    int DroolLevel()
    {
        if (_instance == null) return 0;
        int ul = _instance.UpgradeLevel;
        if (ul >= 5) return 3;
        if (ul >= 4) return 2;
        if (ul >= 3) return 1;
        return 0;
    }

    // ── Scaling ───────────────────────────────────────────────────────────────

    void ApplyLevel(int level)
    {
        if (_droolPS == null) return;

        var emission = _droolPS.emission;
        emission.rateOverTime = level == 1 ? 1.5f : level == 2 ? 2.8f : 4.5f;

        var main = _droolPS.main;
        float sizeMax = level == 1 ? 0.030f : level == 2 ? 0.042f : 0.055f;
        main.startSize    = new ParticleSystem.MinMaxCurve(sizeMax * 0.5f, sizeMax);
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            level == 1 ? 0.35f : level == 2 ? 0.45f : 0.55f,
            level == 1 ? 0.60f : level == 2 ? 0.75f : 0.90f);
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    void BuildDroolPS()
    {
        var go = new GameObject("PoisonDrool");
        go.transform.SetParent(_mouthAnchor, false);
        go.transform.localPosition = Vector3.zero;

        _droolPS = go.AddComponent<ParticleSystem>();

        var main = _droolPS.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;  // fall in world-down regardless of frog rotation
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.60f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.20f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.015f, 0.030f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.35f, 0.00f, 0.50f, 0.90f),   // deep violet
            new Color(0.55f, 0.05f, 0.70f, 0.75f));  // slightly lighter purple
        main.gravityModifier = 0.55f;   // droplets fall downward
        main.maxParticles    = 30;

        var emission = _droolPS.emission;
        emission.rateOverTime = 0f;     // ApplyLevel() will set this when first activated

        // Emit from a tiny point at the mouth — no shape spread
        var shape = _droolPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.018f;

        // Fade from purple → transparent
        var colLife = _droolPS.colorOverLifetime;
        colLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.55f, 0.05f, 0.70f), 0.00f),
                new GradientColorKey(new Color(0.30f, 0.00f, 0.45f), 0.50f),
                new GradientColorKey(new Color(0.20f, 0.00f, 0.30f), 1.00f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(0.75f, 0.10f),
                new GradientAlphaKey(0.40f, 0.60f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        colLife.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink as they drip
        var sizeLife = _droolPS.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f,   0.40f),
                new Keyframe(0.2f, 1.00f),
                new Keyframe(1f,   0.10f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 6;   // render above the frog sprite

        // Start stopped — Update() activates it once conditions are met
        _droolPS.Stop();
    }
}
