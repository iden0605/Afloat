using UnityEngine;

/// <summary>
/// Spawns a small gold sparkle burst + expanding ring at a world position.
/// Called whenever a troop is successfully upgraded.
/// </summary>
public static class UpgradeVFX
{
    public static void Play(Vector3 worldPos)
    {
        SpawnGoldBurst(worldPos);
        SpawnRing(worldPos);
    }

    static void SpawnGoldBurst(Vector3 pos)
    {
        var go = new GameObject("UpgradeBurst");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.20f, 0.50f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 2.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   new Color(1.0f, 0.85f, 0.10f),
                                   new Color(1.0f, 1.0f,  0.55f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 22;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.04f;

        var col  = ps.colorOverLifetime;
        col.enabled = true;
        var gold = new Color(1.0f, 0.85f, 0.10f);
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(gold, 0f), new(Color.white, 0.45f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(1f,
                           new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = "Default";
        psr.sortingOrder     = 10;

        ps.Play();
    }

    static void SpawnRing(Vector3 pos)
    {
        var go = new GameObject("UpgradeRing");
        go.transform.position = pos;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace     = true;
        lr.loop              = true;
        lr.positionCount     = 24;
        lr.numCapVertices    = 0;
        lr.numCornerVertices = 0;
        lr.widthMultiplier   = 0.04f;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = new Color(1.0f, 0.85f, 0.10f);
        lr.material          = mat;
        lr.sortingLayerName  = "Default";
        lr.sortingOrder      = 10;

        go.AddComponent<UpgradeRingFX>().Init(lr, new Color(1.0f, 0.85f, 0.10f));
    }
}

/// <summary>Small expanding gold ring that self-destructs in ~0.35 s.</summary>
public class UpgradeRingFX : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;

    private const float Duration   = 0.35f;
    private const float MaxRadius  = 0.40f;
    private const int   Segments   = 24;
    private const float StartWidth = 0.04f;

    public void Init(LineRenderer ring, Color color)
    {
        _ring  = ring;
        _color = color;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t      = Mathf.Clamp01(_timer / Duration);
        float radius = Mathf.Lerp(0f, MaxRadius, t);
        float alpha  = Mathf.Lerp(0.9f, 0f, t);
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
