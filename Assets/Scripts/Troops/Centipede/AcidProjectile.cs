using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Self-contained acid ball projectile spawned by CentipedeAcidAttack.
///
/// Visual layers (all procedural, no sprites):
///   • Outer glow   — large semi-transparent green disk
///   • Inner core   — solid bright green disk
///   • Ring border  — thin bright LineRenderer ring
///
/// The ball pulses in scale as it flies (quivering acid feel).
/// On trigger hit it invokes the hit callback and spawns acid splat FX.
///
/// Pierce: if pierceCount > 1 the projectile passes through enemies and hits
/// up to pierceCount targets before destroying. After each pierce the shot
/// continues in the same direction it was travelling at the moment of impact.
///
/// Call Launch() immediately after AddComponent<AcidProjectile>().
/// </summary>
public class AcidProjectile : MonoBehaviour
{
    // ── Set by Launch() ───────────────────────────────────────

    private EnemyMovement          _target;
    private float                  _damage;
    private float                  _speed;
    private float                  _radius;
    private string                 _sortingLayer;
    private int                    _sortingOrder;
    private Action<EnemyMovement>  _onHit;

    // ── Runtime ───────────────────────────────────────────────

    private Vector3               _targetPos;
    private Vector3               _travelDir;    // direction after last pierce
    private bool                  _launched;
    private int                   _pierceRemaining; // hits left before destroy
    private readonly HashSet<EnemyMovement> _alreadyHit = new();
    private float                 _lifetime;
    private float                 _pulseTimer;

    // ── Colors ────────────────────────────────────────────────

    private static readonly Color CoreColor  = new Color(0.20f, 1.00f, 0.08f, 1.00f);
    private static readonly Color GlowColor  = new Color(0.35f, 1.00f, 0.18f, 0.28f);
    private static readonly Color RingColor  = new Color(0.60f, 1.00f, 0.35f, 0.90f);

    // ── Public API ────────────────────────────────────────────

    /// <summary>
    /// Builds visuals + collider, then starts the projectile moving.
    /// Must be called once, right after AddComponent.
    /// </summary>
    /// <param name="pierceCount">Max enemies to hit before destroying (1 = no pierce).</param>
    public void Launch(
        EnemyMovement         target,
        float                 damage,
        float                 speed,
        float                 radius,
        string                sortingLayer,
        int                   sortingOrder,
        Action<EnemyMovement> onHit,
        int                   pierceCount = 1)
    {
        _target           = target;
        _damage           = damage;
        _speed            = speed;
        _radius           = radius;
        _sortingLayer     = sortingLayer;
        _sortingOrder     = sortingOrder;
        _onHit            = onHit;
        _pierceRemaining  = Mathf.Max(1, pierceCount);
        _lifetime         = 12f;

        if (_target != null)
            _targetPos = _target.transform.position;

        _travelDir = _target != null
            ? (_targetPos - transform.position).normalized
            : Vector3.up;

        BuildVisuals();
        BuildCollider();
        _launched = true;
    }

    // ── Visuals ───────────────────────────────────────────────

    void BuildVisuals()
    {
        // Outer glow — larger, low-alpha disk behind the core
        BuildDisk("AcidGlow", _radius * 1.65f, GlowColor, _sortingOrder - 1);

        // Inner core — solid bright disk in front
        BuildDisk("AcidCore", _radius, CoreColor, _sortingOrder);

        // Thin ring around the ball
        var ringGO = new GameObject("AcidRing");
        ringGO.transform.SetParent(transform, false);

        var lr = ringGO.AddComponent<LineRenderer>();
        lr.useWorldSpace     = false; // local space: moves with the projectile
        lr.loop              = true;
        lr.positionCount     = 24;
        lr.widthMultiplier   = 0.022f;
        lr.numCapVertices    = 0;
        lr.numCornerVertices = 0;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = RingColor;
        lr.material          = mat;
        lr.sortingLayerName  = _sortingLayer;
        lr.sortingOrder      = _sortingOrder + 1;

        float ringR = _radius * 1.18f;
        for (int i = 0; i < 24; i++)
        {
            float a = 2f * Mathf.PI * i / 24;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * ringR, Mathf.Sin(a) * ringR, 0f));
        }
    }

    void BuildDisk(string name, float r, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.mesh = MakeDiskMesh(r, 32);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color           = color;
        mr.material         = mat;
        mr.sortingLayerName = _sortingLayer;
        mr.sortingOrder     = order;
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

    // ── Collider ──────────────────────────────────────────────

    void BuildCollider()
    {
        // Kinematic Rigidbody2D required for trigger callbacks to fire
        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        var col = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = _radius * 0.85f; // slightly smaller than visual for fair hit feel
        col.isTrigger = true;
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        if (!_launched) return;

        // Soft-track: follow the live enemy so the shot feels responsive
        if (_target != null)
        {
            _travelDir = (_target.transform.position - transform.position).normalized;
            _targetPos = _target.transform.position;
        }

        // Move
        transform.position = Vector3.MoveTowards(
            transform.position, _targetPos, _speed * Time.deltaTime);

        // Quiver: scale pulsing gives a "living" acid blob feel
        _pulseTimer += Time.deltaTime * 9f;
        float pulse = 1f + Mathf.Sin(_pulseTimer) * 0.10f;
        transform.localScale = Vector3.one * pulse;

        // Failsafe: destroy if it never hits anything
        _lifetime -= Time.deltaTime;
        if (_lifetime <= 0f) { Destroy(gameObject); return; }

        // If there is no target (or it died) and we've reached the destination, clean up
        if (_target == null &&
            Vector3.Distance(transform.position, _targetPos) < 0.08f)
        {
            Destroy(gameObject);
        }
    }

    // ── Hit detection ─────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_pierceRemaining <= 0) return;
        if (!other.TryGetComponent<EnemyMovement>(out var enemy)) return;
        if (!_alreadyHit.Add(enemy)) return; // don't double-hit the same enemy

        _onHit?.Invoke(enemy);
        SpawnImpact(transform.position);

        _pierceRemaining--;
        if (_pierceRemaining <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Pierce: clear tracked target so the projectile continues in its current
        // direction. Project the destination well ahead so it flies past this enemy.
        _target    = null;
        _targetPos = transform.position + _travelDir * 20f;
    }

    // ── Impact FX ─────────────────────────────────────────────

    void SpawnImpact(Vector3 pos)
    {
        SpawnParticles(pos);
        SpawnSplatRing(pos);
    }

    void SpawnParticles(Vector3 pos)
    {
        var go = new GameObject("AcidSplat_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop              = false;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.28f, 0.55f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(1.8f, 4.5f);
        main.startSize         = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startColor        = new ParticleSystem.MinMaxGradient(
                                     new Color(0.20f, 1.00f, 0.08f),
                                     new Color(0.75f, 1.00f, 0.22f));
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.maxParticles      = 30;
        main.stopAction        = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        // Fade from bright green to yellow-green as particles die
        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.20f, 1.00f, 0.08f), 0f),
                new GradientColorKey(new Color(0.80f, 1.00f, 0.25f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Shrink as they travel
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(
                           1f, new AnimationCurve(
                               new Keyframe(0f, 1f),
                               new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = _sortingLayer;
        psr.sortingOrder     = _sortingOrder + 2;

        ps.Play();
    }

    void SpawnSplatRing(Vector3 pos)
    {
        var go = new GameObject("AcidSplat_Ring");
        go.transform.position = pos;

        var ring = go.AddComponent<AcidSplatRing>();
        ring.Init(_sortingLayer, _sortingOrder);
    }
}
