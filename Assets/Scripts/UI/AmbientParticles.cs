using UnityEngine;

/// <summary>
/// Scene-level ambient particle system giving the level a living, breathing feel.
/// Place on a GameObject in the scene (e.g. "AmbientParticles").
/// Spawns slow-drifting particles (bubbles/motes) across the camera viewport.
/// </summary>
public class AmbientParticles : MonoBehaviour
{
    [Header("Emission")]
    [SerializeField] private float emissionRate = 3.5f;
    [SerializeField] private float spawnWidth   = 20f;
    [SerializeField] private float spawnHeight  = 14f;

    [Header("Particles")]
    [SerializeField] private Color  moteColorA   = new Color(0.55f, 0.90f, 0.72f, 0.55f);
    [SerializeField] private Color  moteColorB   = new Color(0.72f, 0.94f, 1.00f, 0.65f);
    [SerializeField] private float  minSize      = 0.04f;
    [SerializeField] private float  maxSize      = 0.11f;
    [SerializeField] private float  minLifetime  = 4f;
    [SerializeField] private float  maxLifetime  = 9f;
    [SerializeField] private float  minSpeed     = 0.04f;
    [SerializeField] private float  maxSpeed     = 0.14f;

    private Camera _cam;

    void Start()
    {
        _cam = Camera.main;
        BuildParticleSystem();
    }

    // Keep the emitter centred on the camera so particles always spawn in view.
    void Update()
    {
        if (_cam != null)
            transform.position = new Vector3(_cam.transform.position.x,
                                             _cam.transform.position.y, 0f);
    }

    void BuildParticleSystem()
    {
        var ps   = gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor      = new ParticleSystem.MinMaxGradient(moteColorA, moteColorB);
        main.gravityModifier = -0.008f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 200;

        var emission = ps.emission;
        emission.rateOverTime = emissionRate;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(spawnWidth, spawnHeight, 0.1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(moteColorA, 0.0f),
                new GradientColorKey(moteColorB, 0.5f),
                new GradientColorKey(moteColorA, 1.0f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0.00f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.80f),
                new GradientAlphaKey(0f, 1.00f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.12f;
        noise.frequency   = 0.25f;
        noise.scrollSpeed = 0.08f;
        noise.quality     = ParticleSystemNoiseQuality.Low;

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size    = new ParticleSystem.MinMaxCurve(1f,
                               new AnimationCurve(
                                   new Keyframe(0f,   0f),
                                   new Keyframe(0.2f, 1f),
                                   new Keyframe(0.8f, 1f),
                                   new Keyframe(1f,   0f)));

        var psr = GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = "Default";
        psr.sortingOrder     = 3;   // above background, below units and UI

        ps.Play();
    }
}
