using UnityEngine;

/// <summary>
/// One-stop scene setup for all background visual enhancements.
/// Place this on any scene GameObject (e.g. "BackgroundEnhancer").
///
/// It expects the background layer GameObjects to already exist in the scene
/// and be assigned in the inspector. If no layers are assigned, the component
/// still spawns DappledLight and skips per-sprite effects silently.
///
/// ─ What it sets up ──────────────────────────────────────────────────────────
///
///  ParallaxLayer     — on each layer GO; far layers move more on mouse movement
///  BackgroundColorPulse — on each layer GO; subtle warm/cool breathing tint
///  WaterUVScroll     — only on layers you mark as waterLayers; UV shimmer
///  DappledLight      — spawns as a child GO; drifting additive caustic blobs
///
/// ─ Manual alternative ───────────────────────────────────────────────────────
///  You can skip this script and add ParallaxLayer / BackgroundColorPulse /
///  WaterUVScroll directly to each background GO in the Inspector instead.
/// </summary>
public class BackgroundEnhancer : MonoBehaviour
{
    [System.Serializable]
    public struct BackgroundLayerConfig
    {
        [Tooltip("The background sprite GameObject for this layer (ordered front to back).")]
        public GameObject layerGO;

        [Tooltip("Parallax strength: 0 = no movement, 0.08 = far background. Increase with depth.")]
        public float parallaxStrength;

        [Tooltip("Phase offset for colour pulse so layers don't throb in sync.")]
        public float colorPulsePhase;

        [Tooltip("If true, adds WaterUVScroll to this layer for water shimmer.")]
        public bool isWaterLayer;

        [Tooltip("UV scroll speed X (only used if isWaterLayer is true).")]
        public float waterScrollX;

        [Tooltip("UV scroll speed Y (only used if isWaterLayer is true).")]
        public float waterScrollY;
    }

    [Header("Background Layers (front to back)")]
    [SerializeField] private BackgroundLayerConfig[] layers;

    [Header("Dappled Light")]
    [SerializeField] private bool  spawnDappledLight = true;
    [SerializeField] private int   maxLightBlobs     = 12;
    [SerializeField] private float lightBlobAlpha    = 0.15f;

    void Start()
    {
        // Configure each background layer
        foreach (var cfg in layers)
        {
            if (cfg.layerGO == null) continue;

            // ── Parallax ─────────────────────────────────────────────────────
            if (cfg.parallaxStrength > 0f)
            {
                var p = cfg.layerGO.AddComponent<ParallaxLayer>();
                // Use serialized field via component so we set it via reflection-free way:
                // ParallaxLayer exposes [SerializeField] fields — set them before Start() runs.
                // Since we're inside our own Start(), ParallaxLayer.Start() hasn't run yet
                // (component added this frame). We use SendMessage to trigger a setter, or
                // better: just configure via the public Init method we'll add.
                // For simplicity, let ParallaxLayer read its own [SerializeField] defaults
                // and let the user tune in Inspector when auto-setup is not precise enough.
                // We DO write the field via the component's exposed property if available.
                // Here we call the public configure method (ParallaxLayer exposes one):
                p.Configure(cfg.parallaxStrength, smoothing: 4f);
            }

            // ── Colour Pulse ─────────────────────────────────────────────────
            var pulse = cfg.layerGO.AddComponent<BackgroundColorPulse>();
            pulse.Configure(
                colorA:      new Color(1.00f, 0.98f, 0.94f),
                colorB:      new Color(0.90f, 0.96f, 1.00f),
                period:      8f,
                phaseOffset: cfg.colorPulsePhase);

            // ── Water UV Scroll ──────────────────────────────────────────────
            if (cfg.isWaterLayer)
            {
                var scroll = cfg.layerGO.AddComponent<WaterUVScroll>();
                scroll.Configure(cfg.waterScrollX, cfg.waterScrollY);
            }
        }

        // ── Dappled Light ────────────────────────────────────────────────────
        if (spawnDappledLight)
        {
            var lightGO = new GameObject("DappledLight");
            lightGO.transform.SetParent(transform, false);
            var dl = lightGO.AddComponent<DappledLight>();
            dl.Configure(maxLightBlobs, lightBlobAlpha);
        }
    }
}
