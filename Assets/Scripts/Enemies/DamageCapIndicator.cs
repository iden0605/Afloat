using UnityEngine;

/// <summary>
/// Spawns a brief silver/grey "absorbed" shield burst on an enemy when the
/// MaxDamagePerHit cap activates — visually distinct from AttackMissIndicator
/// (which signals a full miss) since this signals a partial hit that was reduced.
///
/// Effect: a ring of grey-silver particles radiating outward, plus a subtle
/// grey disk flash that quickly fades. No nudge — the hit landed, it was just capped.
///
/// Call DamageCapIndicator.Spawn(enemyTransform, attackerPos) from EnemyInstance.
/// </summary>
public static class DamageCapIndicator
{
    public static void Spawn(Transform enemy, Vector3 attackerPos)
    {
        SpawnAbsorbRing(enemy.position);
        SpawnAbsorbParticles(enemy.position, attackerPos);
    }

    private static void SpawnAbsorbRing(Vector3 pos)
    {
        var go = new GameObject("DamageCap_Ring");
        go.transform.position = pos;
        go.AddComponent<AbsorbRing>().Init();
    }

    private static void SpawnAbsorbParticles(Vector3 pos, Vector3 attackerPos)
    {
        Vector2 dir = ((Vector2)(pos - attackerPos)).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;

        var go = new GameObject("DamageCap_Particles");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.0f, 2.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(0.80f, 0.80f, 0.85f, 1.0f),  // silver-grey
                                   new Color(0.60f, 0.60f, 0.70f, 0.8f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 10;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        // Emit mostly in the direction the attack came from (bounce-back feel)
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 35f;
        shape.radius    = 0.04f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(0.9f, 0.9f, 1f), 0f), new(new Color(0.5f, 0.5f, 0.6f), 1f) },
            new GradientAlphaKey[] { new(0.9f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material     = new Material(Shader.Find("Sprites/Default"));
        psr.sortingOrder = 20;

        ps.Play();
    }
}

/// <summary>
/// Expanding silver ring that appears at impact when a damage cap absorbs excess damage.
/// </summary>
public class AbsorbRing : MonoBehaviour
{
    private LineRenderer _lr;
    private float        _timer;

    private const float  Duration  = 0.22f;
    private const int    Segments  = 28;
    private const float  MaxRadius = 0.30f;

    private static readonly Color RingColor = new Color(0.75f, 0.75f, 0.85f, 0.85f);

    public void Init()
    {
        _lr = gameObject.AddComponent<LineRenderer>();
        _lr.useWorldSpace     = false;
        _lr.loop              = true;
        _lr.positionCount     = Segments;
        _lr.widthMultiplier   = 0.045f;
        _lr.numCapVertices    = 0;
        _lr.numCornerVertices = 0;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = RingColor;
        _lr.material         = mat;
        _lr.sortingOrder     = 15;

        DrawRing(0f);
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / Duration);

        float r     = Mathf.Lerp(0f, MaxRadius, t);
        float alpha = Mathf.Lerp(0.85f, 0f, t);
        float width = Mathf.Lerp(1.0f, 0.2f, t);

        _lr.widthMultiplier = 0.045f * width;
        var c = RingColor;
        _lr.startColor = new Color(c.r, c.g, c.b, alpha);
        _lr.endColor   = new Color(c.r, c.g, c.b, alpha);

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
}
