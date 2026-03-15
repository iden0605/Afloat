using UnityEngine;

/// <summary>
/// Spawns softly drifting caustic / dappled-light blobs across the background.
/// Each blob is a procedurally generated radial gradient sprite rendered with
/// additive blending so it brightens underlying pixels rather than covering them.
///
/// Place on any scene GO (e.g. "DappledLight"). Blobs spawn inside a configurable
/// world-space rectangle centred on this transform. Adjust spawnRect to cover
/// the playfield.
/// </summary>
public class DappledLight : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("Half-extents (world units) of the rectangle in which blobs are spawned.")]
    [SerializeField] private Vector2 spawnRect   = new Vector2(12f, 8f);
    [Tooltip("Maximum number of blobs alive at once.")]
    [SerializeField] private int     maxBlobs    = 14;

    [Header("Blob Appearance")]
    [Tooltip("Minimum world-unit diameter of a blob.")]
    [SerializeField] private float   minSize     = 0.6f;
    [Tooltip("Maximum world-unit diameter of a blob.")]
    [SerializeField] private float   maxSize     = 1.8f;
    [Tooltip("Peak alpha at the centre of a blob (0–1). Keep low for subtlety.")]
    [SerializeField] private float   maxAlpha    = 0.18f;
    [Tooltip("Warm sunlight tint — blends with the ambient colour below.")]
    [SerializeField] private Color   colorA      = new Color(1.00f, 0.96f, 0.72f);
    [Tooltip("Cool caustic tint for variety.")]
    [SerializeField] private Color   colorB      = new Color(0.72f, 0.94f, 1.00f);

    [Header("Motion")]
    [Tooltip("Drift speed range (world units / second).")]
    [SerializeField] private float   minSpeed    = 0.04f;
    [SerializeField] private float   maxSpeed    = 0.14f;
    [Tooltip("How long (seconds) a blob lives before it fades out and respawns.")]
    [SerializeField] private float   minLifetime = 5f;
    [SerializeField] private float   maxLifetime = 12f;

    [Header("Sorting")]
    [SerializeField] private string  sortingLayer = "Default";
    [Tooltip("Render just above the background layers, below units and game UI.")]
    [SerializeField] private int     sortingOrder = 2;

    // ── Runtime configure ────────────────────────────────────────────────────

    /// <summary>Called by BackgroundEnhancer to override serialized values before Start().</summary>
    public void Configure(int blobCount, float alpha)
    {
        maxBlobs  = blobCount;
        maxAlpha  = alpha;
    }

    // ── Cached ───────────────────────────────────────────────────────────────

    private static Sprite _blobSprite;   // shared across all instances
    private static Shader _additiveShader;

    private BlobData[] _blobs;

    // ── Struct ───────────────────────────────────────────────────────────────

    private struct BlobData
    {
        public GameObject    Go;
        public SpriteRenderer Sr;
        public Vector3       Velocity;
        public float         Lifetime;
        public float         Age;
        public Color         BaseColor;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        EnsureSharedAssets();
        _blobs = new BlobData[maxBlobs];
        for (int i = 0; i < maxBlobs; i++)
            _blobs[i] = SpawnBlob(randomAge: true);
    }

    void Update()
    {
        for (int i = 0; i < _blobs.Length; i++)
        {
            ref var b = ref _blobs[i];
            b.Age += Time.deltaTime;

            if (b.Age >= b.Lifetime)
            {
                Destroy(b.Go);
                _blobs[i] = SpawnBlob(randomAge: false);
                continue;
            }

            // Drift
            b.Go.transform.position += b.Velocity * Time.deltaTime;

            // Fade in and out over lifetime (smooth ramp, plateau in middle)
            float t     = b.Age / b.Lifetime;
            float alpha = FadeCurve(t) * maxAlpha;
            var   col   = b.BaseColor;
            col.a       = alpha;
            b.Sr.color  = col;
        }
    }

    void OnDestroy()
    {
        if (_blobs == null) return;
        foreach (var b in _blobs)
            if (b.Go != null) Destroy(b.Go);
    }

    // ── Spawn ────────────────────────────────────────────────────────────────

    BlobData SpawnBlob(bool randomAge)
    {
        float lifetime = Random.Range(minLifetime, maxLifetime);
        float age      = randomAge ? Random.Range(0f, lifetime) : 0f;

        // Random start position inside spawn rect
        Vector3 pos = transform.position + new Vector3(
            Random.Range(-spawnRect.x, spawnRect.x),
            Random.Range(-spawnRect.y, spawnRect.y),
            0f);

        float  size  = Random.Range(minSize, maxSize);
        Color  col   = Color.Lerp(colorA, colorB, Random.value);
        col.a        = 0f; // starts invisible

        float  speed = Random.Range(minSpeed, maxSpeed);
        float  angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        var    vel   = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);

        var go = new GameObject("DappledBlob");
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * size;

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = _blobSprite;
        sr.color            = col;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder     = sortingOrder;

        // Additive blending — brightens underlying pixels
        if (_additiveShader != null)
            sr.material = new Material(_additiveShader);

        return new BlobData
        {
            Go        = go,
            Sr        = sr,
            Velocity  = vel,
            Lifetime  = lifetime,
            Age       = age,
            BaseColor = col,
        };
    }

    // ── Fade curve: ramp up first 15%, plateau, ramp out last 20% ───────────

    static float FadeCurve(float t)
    {
        if (t < 0.15f) return Mathf.SmoothStep(0f, 1f, t / 0.15f);
        if (t > 0.80f) return Mathf.SmoothStep(0f, 1f, 1f - (t - 0.80f) / 0.20f);
        return 1f;
    }

    // ── Shared asset creation ────────────────────────────────────────────────

    static void EnsureSharedAssets()
    {
        if (_blobSprite == null)
            _blobSprite = CreateRadialGradientSprite(64);

        if (_additiveShader == null)
        {
            // Try several names for cross-pipeline compatibility
            _additiveShader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                Shader.Find("Legacy Shaders/Particles/Additive") ??
                Shader.Find("Particles/Additive") ??
                Shader.Find("Sprites/Default");
        }
    }

    /// <summary>
    /// 64×64 radial gradient: opaque white at centre, fully transparent at edge.
    /// Used as the base blob shape; tint applied via SpriteRenderer.color.
    /// </summary>
    static Sprite CreateRadialGradientSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float centre = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx    = (x - centre) / centre;
            float dy    = (y - centre) / centre;
            float dist  = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(1f - dist);
            alpha = alpha * alpha * alpha; // cubic falloff — very soft edge
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
