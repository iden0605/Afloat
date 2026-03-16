using UnityEngine;

/// <summary>
/// Drives status-effect visuals (tint + particles) for Burn, Poison, Freeze, and Stun.
/// Auto-added to enemies by EnemyStatusEffects when a status is first applied.
///
/// Burn  : orange tint + floating ember particles.
/// Poison: green tint  + slow downward drip particles.
/// Freeze: icy-blue tint + sparkling crystal particles.
/// Stun  : yellow tint + small stars orbiting above the enemy's head.
///
/// Cooperates with EnemyHitFlash: HitFlash calls ActiveTint so it restores
/// to the status colour rather than plain white after each flash.
/// </summary>
public class EnemyVisualEffects : MonoBehaviour
{
    // ── Component refs ────────────────────────────────────────────────────────

    private SpriteRenderer     _sr;
    private Color              _baseColor;
    private EnemyStatusEffects _status;

    // ── CC timers (visual side — mirrors EnemyStatusEffects) ─────────────────

    private float _freezeTimer;
    private float _stunTimer;

    // ── Particles ─────────────────────────────────────────────────────────────

    private ParticleSystem _burnPS;
    private ParticleSystem _poisonPS;
    private ParticleSystem _freezePS;
    private Transform      _stunOrbit; // rotating anchor above head
    private ParticleSystem _stunPS;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        // Find the primary sprite renderer, skipping health-bar children
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            var n = sr.gameObject.name;
            if (n == "HP_BG" || n == "HP_Fill") continue;
            _sr = sr;
            break;
        }

        _baseColor = _sr != null ? _sr.color : Color.white;
        _status    = GetComponent<EnemyStatusEffects>();
    }

    void OnDisable()
    {
        if (_sr != null) _sr.color = _baseColor;
        StopAll();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Apply or refresh a freeze visual for the given duration.</summary>
    public void ApplyFreeze(float duration)
    {
        _freezeTimer = Mathf.Max(_freezeTimer, duration);
        EnsureFreezePS();
        if (!_freezePS.isPlaying) _freezePS.Play();
    }

    /// <summary>Apply or refresh a stun visual for the given duration.</summary>
    public void ApplyStun(float duration)
    {
        _stunTimer = Mathf.Max(_stunTimer, duration);
        EnsureStunPS();
        if (!_stunPS.isPlaying) _stunPS.Play();
    }

    /// <summary>
    /// Plays a brief red speed-burst trail (Wasp reactive speed / ReactiveSpeedOnHit).
    /// Safe to call every hit — spawns a one-shot particle burst each time.
    /// </summary>
    public void TriggerSpeedBurst()
    {
        SpawnSpeedBurstVFX(transform.position);
    }

    /// <summary>
    /// Plays the desperation dash explosion (Rocket Raccoon) and applies a permanent
    /// red tint to signal the enemy is now in panic mode.
    /// </summary>
    public void TriggerDesperationDash()
    {
        SpawnDashExplosionVFX(transform.position);
        // Permanent red panic tint — override base colour
        if (_sr != null) _baseColor = new Color(1f, 0.25f, 0.15f, _baseColor.a);
    }

    /// <summary>
    /// The current status-tint colour. EnemyHitFlash restores to this instead of
    /// plain white so the status colour persists after a hit flash.
    /// </summary>
    public Color ActiveTint => ComputeTint();

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_freezeTimer > 0f) _freezeTimer -= Time.deltaTime;
        if (_stunTimer   > 0f) _stunTimer   -= Time.deltaTime;

        // Stop expired CC particles
        if (_freezeTimer <= 0f && _freezePS != null && _freezePS.isPlaying)
            _freezePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_stunTimer <= 0f && _stunPS != null && _stunPS.isPlaying)
            _stunPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Sync DoT particles with live stack counts
        bool hasBurn   = _status != null && _status.HasBurnStacks;
        bool hasPoison = _status != null && _status.HasPoisonStacks;

        if (hasBurn)
        {
            EnsureBurnPS();
            if (!_burnPS.isPlaying) _burnPS.Play();
        }
        else if (_burnPS != null && _burnPS.isPlaying)
            _burnPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (hasPoison)
        {
            EnsurePoisonPS();
            if (!_poisonPS.isPlaying) _poisonPS.Play();
        }
        else if (_poisonPS != null && _poisonPS.isPlaying)
            _poisonPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // Rotate the stun orbit anchor
        if (_stunOrbit != null)
            _stunOrbit.Rotate(0f, 0f, 200f * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (_sr == null) return;

        bool any = (_status != null && (_status.HasBurnStacks || _status.HasPoisonStacks))
                   || _freezeTimer > 0f || _stunTimer > 0f;

        Color target = any ? ComputeTint() : _baseColor;
        _sr.color = Color.Lerp(_sr.color, target, 10f * Time.deltaTime);
    }

    // ── Tint ─────────────────────────────────────────────────────────────────

    private Color ComputeTint()
    {
        bool hasBurn   = _status != null && _status.HasBurnStacks;
        bool hasPoison = _status != null && _status.HasPoisonStacks;
        bool hasFreeze = _freezeTimer > 0f;
        bool hasStun   = _stunTimer   > 0f;

        Color tint = _baseColor;
        int   n    = 0;

        if (hasBurn)   { tint += new Color(0.8f, 0.15f, 0.00f); n++; }
        if (hasPoison) { tint += new Color(0.45f, 0.00f, 0.65f); n++; }
        if (hasFreeze) { tint += new Color(0.30f, 0.55f, 0.85f); n++; }
        if (hasStun)   { tint += new Color(0.70f, 0.70f, 0.00f); n++; }

        if (n == 0) return _baseColor;
        return new Color(tint.r / (n + 1), tint.g / (n + 1), tint.b / (n + 1), _baseColor.a);
    }

    // ── Particle builders (lazy) ──────────────────────────────────────────────

    void EnsureBurnPS()
    {
        if (_burnPS != null) return;
        var go = new GameObject("BurnFX");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _burnPS = go.AddComponent<ParticleSystem>();
        var main = _burnPS.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.30f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.40f, 0.90f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.45f, 0.05f),
                                   new Color(1.00f, 0.80f, 0.10f));
        main.gravityModifier = -0.4f; // float upward
        main.maxParticles    = 20;

        var emission = _burnPS.emission;
        emission.rateOverTime = 12f;

        var shape = _burnPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.12f;

        var vel = _burnPS.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeL = _burnPS.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 5;
    }

    void EnsurePoisonPS()
    {
        if (_poisonPS != null) return;
        var go = new GameObject("PoisonFX");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _poisonPS = go.AddComponent<ParticleSystem>();
        var main = _poisonPS.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.60f, 1.00f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.20f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.30f, 0.00f, 0.50f),
                                   new Color(0.60f, 0.05f, 0.80f));
        main.gravityModifier = 0.15f; // drip slowly downward
        main.maxParticles    = 16;

        var emission = _poisonPS.emission;
        emission.rateOverTime = 6f;

        var shape = _poisonPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.10f;

        var sizeL = _poisonPS.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 5;
    }

    void EnsureFreezePS()
    {
        if (_freezePS != null) return;
        var go = new GameObject("FreezeFX");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        _freezePS = go.AddComponent<ParticleSystem>();
        var main = _freezePS.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.10f, 0.35f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.70f, 0.90f, 1.00f),
                                   new Color(0.90f, 0.98f, 1.00f));
        main.gravityModifier = -0.1f;
        main.maxParticles    = 18;

        var emission = _freezePS.emission;
        emission.rateOverTime = 10f;

        var shape = _freezePS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.14f;

        var sizeL = _freezePS.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.8f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 5;
    }

    void EnsureStunPS()
    {
        if (_stunPS != null) return;

        // Rotating orbit anchor — sits above the enemy's head
        _stunOrbit = new GameObject("StunOrbit").transform;
        _stunOrbit.SetParent(transform, false);
        _stunOrbit.localPosition = new Vector3(0f, 0.28f, 0f);

        // Particle emitter offset from the orbit centre so stars appear to circle it
        var go = new GameObject("StunFX");
        go.transform.SetParent(_stunOrbit, false);
        go.transform.localPosition = new Vector3(0.20f, 0f, 0f);

        _stunPS = go.AddComponent<ParticleSystem>();
        var main = _stunPS.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.20f, 0.35f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.10f, 0.25f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.95f, 0.20f),
                                   Color.white);
        main.gravityModifier = -0.05f;
        main.maxParticles    = 12;

        var emission = _stunPS.emission;
        emission.rateOverTime = 8f;

        var shape = _stunPS.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.05f;

        var sizeL = _stunPS.sizeOverLifetime;
        sizeL.enabled = true;
        sizeL.size    = new ParticleSystem.MinMaxCurve(1f,
                            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 8;
    }

    private void StopAll()
    {
        _burnPS?.Stop(true,   ParticleSystemStopBehavior.StopEmittingAndClear);
        _poisonPS?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _freezePS?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _stunPS?.Stop(true,   ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // ── One-shot VFX spawners ─────────────────────────────────────────────────

    // Red/orange speed streak burst — used for ReactiveSpeedOnHit (Wasp panic dash)
    private static void SpawnSpeedBurstVFX(Vector3 pos)
    {
        var go   = new GameObject("SpeedBurst_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f,  5.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.30f, 0.05f),
                                   new Color(1.00f, 0.65f, 0.10f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 16;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        // Horizontal spray — streaks fly sideways to look like speed lines
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.08f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(1f, 0.6f, 0.1f), 0f), new(new Color(1f, 0.2f, 0f), 1f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 10;

        ps.Play();
    }

    // Large red/orange burst + shockwave flash — used for DesperationDash (Rocket Raccoon)
    private static void SpawnDashExplosionVFX(Vector3 pos)
    {
        // Larger, more dramatic burst
        var go   = new GameObject("DesperationDash_VFX");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.0f,  6.0f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.00f, 0.20f, 0.05f),
                                   new Color(1.00f, 0.55f, 0.10f));
        main.gravityModifier = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 36;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.12f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(1f, 0.8f, 0.2f), 0f), new(new Color(0.8f, 0.05f, 0f), 1f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 10;

        ps.Play();
    }
}
