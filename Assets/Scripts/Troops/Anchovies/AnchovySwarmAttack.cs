using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anchovy swarm attack — a school of anchovies that sweeps back and forth
/// across the enemy path, dealing piercing damage to every enemy they touch.
///
/// ── Coordinate convention ──
/// The troop is oriented so its LOCAL Y axis points along the enemy path direction.
/// The attack rectangle is therefore:
///   • Long side  (local X): perpendicular to path  — the sweep direction
///   • Short side (local Y): along the path         — the capture depth
///
/// ── Phases ──
///   1. Charging — the 5 fish orbit in a tight schooling formation while waiting
///                 for the next sweep. Each fish is evenly spaced around the orbit
///                 and faces its tangent direction (scale-flip). A slow breathe-pulse
///                 adds life. No animator clips required.
///   2. Sweeping — the school translates along local X. Fish face the sweep direction
///                 via scale-flip and bob with a staggered sine-wave swim motion.
///                 Every enemy touched is hit once per sweep (HashSet guard).
///
/// ── Setup ──
///   • Add this component to the Anchovies Variant prefab.
///   • Set enemyLayer to the Enemy layer.
///   • TroopData: placementType = PathOnly, useRectangularRange = true,
///     range = half the sweep distance, rangeRectWidth = depth along the path.
///   • Optionally add AnchovyFish to each child for a tilt-while-swimming enhancement.
/// </summary>
[RequireComponent(typeof(TroopBehavior), typeof(TroopInstance))]
public class AnchovySwarmAttack : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Layer mask for enemy GameObjects — must match the Enemy layer.")]
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("Collision radius per fish (world units). Enemies within this radius of any fish are hit.")]
    [SerializeField] private float fishCollisionRadius = 0.14f;

    [Header("Sweep")]
    [Tooltip("Speed at which the school crosses the rectangle (world units / second).")]
    [SerializeField] private float sweepSpeed = 2.2f;

    [Header("Charge — Schooling Orbit")]
    [Tooltip("How many full orbits per second each fish completes while waiting.")]
    [SerializeField] private float orbitSpeed  = 0.18f;

    [Tooltip("Radius of each fish's orbit around its rest position (world units).")]
    [SerializeField] private float orbitRadius = 0.055f;

    [Tooltip("Frequency of the slow breathe-pulse applied on top of the orbit (Hz).")]
    [SerializeField] private float breatheFreq = 0.7f;

    [Tooltip("Max scale deviation of the breathe-pulse (0.08 = ±8% size).")]
    [SerializeField] private float breathePulse = 0.08f;

    [Header("Sweep — Swim Animation")]
    [Tooltip("Frequency of the up/down swim bob while sweeping (Hz).")]
    [SerializeField] private float swimBobFreq  = 7f;

    [Tooltip("Max Y-bob amplitude while sweeping (world units, local space).")]
    [SerializeField] private float swimBobAmt   = 0.018f;

    [Tooltip("Per-fish phase offset for the swim bob (radians).")]
    [SerializeField] private float swimStagger  = 0.9f;

    [Header("Fish Sprites")]
    [Tooltip("The angle (degrees) at which the fish sprite naturally points when localRotation = identity.\n" +
             "  0   = sprite faces RIGHT  (+local X)\n" +
             "  90  = sprite faces UP     (+local Y, common for top-down fish)\n" +
             "  180 = sprite faces LEFT   (-local X)\n" +
             " -90  = sprite faces DOWN   (-local Y)\n" +
             "Adjust until the fish face the direction they swim.")]
    [SerializeField] private float fishNaturalAngleDeg = 90f;

    [Header("Collision VFX")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder      = 5;

    // ── Internal state ────────────────────────────────────────────────────────

    private enum Phase { Charging, Sweeping }

    private TroopBehavior _behavior;
    private TroopInstance _instance;

    // Visual fish children (all children except the RangeIndicator)
    private Transform[]   _fish;
    private AnchovyFish[] _anchovyFish;      // optional per-fish tilt component
    private Vector3[]     _baseFishLocalPos;
    private float[]       _baseFishAbsScaleX;
    private float[]       _baseFishScaleY;

    private Phase _phase       = Phase.Charging;
    private float _cooldown    = 0f;
    private float _chargeTimer = 0f;

    // Current X offset of the school along the troop's local X axis
    private float _sweepOffset = 0f;
    // +1 = sweeping toward +X (right), -1 = sweeping toward -X (left)
    private int   _sweepDir    = 1;

    private readonly HashSet<EnemyMovement> _hitThisSwipe = new();

    // ── Properties ────────────────────────────────────────────────────────────

    float HalfLong  => _instance.CurrentRange;
    float HalfShort => (_instance.Data?.rangeRectWidth ?? 0.6f) / 2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _behavior = GetComponent<TroopBehavior>();
        _instance = GetComponent<TroopInstance>();
        _behavior.suppressRotation = true;
    }

    void Start()
    {
        SnapToPathPerpendicular();
        GatherFish();

        _sweepOffset = -HalfLong;
        _sweepDir    = 1;
        _phase       = Phase.Charging;
        _cooldown    = 0.35f;
    }

    void OnDisable()
    {
        if (_fish == null) return;
        for (int i = 0; i < _fish.Length; i++)
        {
            if (_fish[i] == null) continue;
            _fish[i].localPosition = _baseFishLocalPos[i];
            _fish[i].localScale    = new Vector3(_baseFishAbsScaleX[i], _baseFishScaleY[i], _fish[i].localScale.z);
            _fish[i].localRotation = Quaternion.identity;
            _anchovyFish[i]?.ResetTilt();
        }
        _phase    = Phase.Charging;
        _cooldown = 0f;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        _cooldown -= Time.deltaTime;

        switch (_phase)
        {
            case Phase.Charging: TickCharge(); break;
            case Phase.Sweeping: TickSweep();  break;
        }
    }

    // ── Phase: Charging ───────────────────────────────────────────────────────

    void TickCharge()
    {
        _chargeTimer += Time.deltaTime;
        AnimateChargeFish();

        if (_cooldown <= 0f)
            BeginSweep();
    }

    /// <summary>
    /// Each fish orbits its rest position in a tight circle, staggered so the 5 fish
    /// are evenly spread (72° apart). Every fish faces its tangent direction via
    /// scale-flip. A slow breathe-pulse adds a sense of life to the waiting state.
    /// </summary>
    void AnimateChargeFish()
    {
        if (_fish == null) return;

        float orbitAngleBase = _chargeTimer * orbitSpeed * Mathf.PI * 2f;
        float breathe        = 1f + Mathf.Sin(_chargeTimer * breatheFreq * Mathf.PI * 2f) * breathePulse;
        float evenSpread     = Mathf.PI * 2f / _fish.Length;

        for (int i = 0; i < _fish.Length; i++)
        {
            if (_fish[i] == null) continue;

            float phase = orbitAngleBase + i * evenSpread;

            // Orbital displacement in local XY space
            float ox = Mathf.Cos(phase) * orbitRadius;
            float oy = Mathf.Sin(phase) * orbitRadius;

            Vector3 bp = _baseFishLocalPos[i];
            _fish[i].localPosition = new Vector3(bp.x + _sweepOffset + ox, bp.y + oy, bp.z);

            // Tangent of a CCW circle at angle 'phase' is (-sin, cos).
            var tangent = new Vector2(-Mathf.Sin(phase), Mathf.Cos(phase));
            _anchovyFish[i]?.SetTilt(tangent.x);
            SetFishFacing(i, tangent, breathe);
        }
    }

    // ── Phase: Sweeping ───────────────────────────────────────────────────────

    void BeginSweep()
    {
        _phase       = Phase.Sweeping;
        _sweepOffset = _sweepDir > 0 ? -HalfLong : HalfLong;
        _hitThisSwipe.Clear();
    }

    void TickSweep()
    {
        _sweepOffset += sweepSpeed * _sweepDir * Time.deltaTime;

        float target     = _sweepDir > 0 ? HalfLong : -HalfLong;
        bool  reachedEnd = _sweepDir > 0
            ? _sweepOffset >= target
            : _sweepOffset <= target;

        if (reachedEnd)
        {
            _sweepOffset = target;
            UpdateFishForSweep();
            EndSweep();
            return;
        }

        UpdateFishForSweep();
        CheckDamage();
    }

    void UpdateFishForSweep()
    {
        if (_fish == null) return;

        for (int i = 0; i < _fish.Length; i++)
        {
            if (_fish[i] == null) continue;

            float bob = Mathf.Sin(Time.time * swimBobFreq * Mathf.PI * 2f + i * swimStagger) * swimBobAmt;

            Vector3 bp = _baseFishLocalPos[i];
            _fish[i].localPosition = new Vector3(bp.x + _sweepOffset, bp.y + bob, bp.z);

            var sweepDir2D = new Vector2(_sweepDir, 0f);
            _anchovyFish[i]?.SetTilt(_sweepDir);
            SetFishFacing(i, sweepDir2D, 1f);
        }
    }

    void EndSweep()
    {
        _sweepDir    = -_sweepDir;
        _phase       = Phase.Charging;
        _chargeTimer = 0f;
        _cooldown    = _instance.GetEffectiveAttackInterval();
    }

    // ── Facing ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a scale-flip to fish[i] so it faces the given horizontal direction.
    /// dirX > 0 → moving right; dirX < 0 → moving left.
    /// breathe is a uniform scale multiplier (1 = normal).
    /// </summary>
    void SetFishFacing(int i, Vector2 localDir, float breathe)
    {
        // AnchovyFish.Update() was overriding our rotation every frame (it set localRotation
        // to its tilt angle, wiping the facing). Now AnchovyFish has no Update() — we tick
        // it manually here and fold the tilt into a single localRotation write.
        _anchovyFish[i]?.TickTilt();
        float tilt = _anchovyFish[i]?.CurrentTilt ?? 0f;

        if (localDir.sqrMagnitude > 0.0001f)
        {
            float movAngle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
            _fish[i].localRotation = Quaternion.Euler(0f, 0f, movAngle - fishNaturalAngleDeg + tilt);
        }

        // Breathe-pulse via scale — always derive from cached base to avoid compounding.
        _fish[i].localScale = new Vector3(
            _baseFishAbsScaleX[i] * breathe,
            _baseFishScaleY[i]    * breathe,
            _fish[i].localScale.z);
    }

    // ── Damage ────────────────────────────────────────────────────────────────

    void CheckDamage()
    {
        for (int i = 0; i < _fish.Length; i++)
        {
            if (_fish[i] == null) continue;

            var hits = Physics2D.OverlapCircleAll(
                _fish[i].position, fishCollisionRadius, enemyLayer);

            foreach (var col in hits)
            {
                if (!col.TryGetComponent<EnemyMovement>(out var em)) continue;
                if (!_hitThisSwipe.Add(em)) continue;

                _instance.DealDamage(
                    em,
                    _instance.Data?.attackType ?? AttackType.Generic,
                    transform.position);

                SpawnHitBubbles(em.transform.position);
            }
        }
    }

    // ── VFX ───────────────────────────────────────────────────────────────────

    void SpawnHitBubbles(Vector3 pos)
    {
        var go = new GameObject("AnchovyHit_Bubbles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.6f, 2.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.025f, 0.08f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.45f, 0.82f, 1.00f),
                                   new Color(0.90f, 0.97f, 1.00f));
        main.gravityModifier = -0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 16;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.05f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.45f, 0.82f, 1.00f), 0f),
                new GradientColorKey(new Color(0.90f, 0.97f, 1.00f), 0.5f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f,   1f),
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f, 0.6f),
                                   new Keyframe(0.4f, 1f),
                                   new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = sortingLayerName;
        psr.sortingOrder     = sortingOrder + 1;

        ps.Play();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SnapToPathPerpendicular()
    {
        if (WaypointManager.Instance == null) return;
        Vector2 pathDir = WaypointManager.Instance.GetPathDirectionAt(transform.position);
        float   angle   = Mathf.Atan2(pathDir.y, pathDir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void GatherFish()
    {
        var list = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.GetComponent<RangeIndicator>() != null) continue;
            list.Add(child);
        }

        _fish              = list.ToArray();
        _baseFishLocalPos  = System.Array.ConvertAll(_fish, f => f.localPosition);
        _baseFishAbsScaleX = System.Array.ConvertAll(_fish, f => Mathf.Abs(f.localScale.x));
        _baseFishScaleY    = System.Array.ConvertAll(_fish, f => f.localScale.y);
        _anchovyFish       = System.Array.ConvertAll(_fish, f => f.GetComponent<AnchovyFish>());

    }
}
