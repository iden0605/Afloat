using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pokémon-style full-screen evolve cutscene.
///
/// Flow:
///   1. Black overlay fades in.
///   2. From-sprite zooms in with an "EaseOutBack" pop (small → normal).
///   3. Rotation accelerates from slow to very fast.
///   4. At peak speed the sprite swaps to the evolved form.
///   5. Rotation decelerates back to slow.
///   6. Sprite zooms out (normal → zero).
///   7. onComplete() fires — TroopInstance.Evolve() runs here.
///   8. Black overlay fades out, revealing the game world.
///   9. Exaggerated particle burst + expanding rings at the troop's world position.
///
/// Uses Time.unscaledDeltaTime / WaitForSecondsRealtime throughout so it
/// works while Time.timeScale == 0.
///
/// Usage (called by TroopSelectionUI):
///   EvolveCutscene.Play(fromSprite, toSprite, worldPos, () => target.Evolve());
/// </summary>
public class EvolveCutscene : MonoBehaviour
{
    public static EvolveCutscene Instance { get; private set; }

    // Tracks which evolution keys have already played their cutscene.
    // Key format: "<TroopData.name>_<evolutionName>" — persists for the lifetime of the session.
    private static readonly System.Collections.Generic.HashSet<string> _seenEvolutions = new();

    // ── Timing ────────────────────────────────────────────────────────────────

    private const float FadeInDuration  = 0.25f;
    private const float ZoomInDuration  = 0.70f;
    private const float AccelDuration   = 2.0f;
    private const float DecelDuration   = 2.0f;
    private const float ZoomOutDuration = 0.45f;
    private const float FadeOutDuration = 0.30f;
    private const float BurstHold       = 0.85f;  // extra time to enjoy the particles

    private const float MinRotSpeed  = 50f;
    private const float MaxRotSpeed  = 1260f;  // ~3.5 full rotations per second at peak

    // ── UI refs ───────────────────────────────────────────────────────────────

    private Canvas        _canvas;
    private Image         _blackPanel;
    private Image         _spriteImage;
    private RectTransform _spriteRect;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
    }

    void BuildCanvas()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 1000;   // always on top of every UIDocument
        gameObject.AddComponent<CanvasScaler>();

        // Full-screen black panel — starts fully transparent
        var bgGo = new GameObject("CutsceneBG");
        bgGo.transform.SetParent(transform, false);
        _blackPanel       = bgGo.AddComponent<Image>();
        _blackPanel.color = new Color(0f, 0f, 0f, 0f);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;

        // Sprite display — centred, starts invisible
        var spGo = new GameObject("CutsceneSprite");
        spGo.transform.SetParent(transform, false);
        _spriteImage              = spGo.AddComponent<Image>();
        _spriteImage.preserveAspect = true;
        _spriteImage.color        = Color.white;
        _spriteRect               = spGo.GetComponent<RectTransform>();
        _spriteRect.anchorMin        = new Vector2(0.5f, 0.5f);
        _spriteRect.anchorMax        = new Vector2(0.5f, 0.5f);
        _spriteRect.pivot            = new Vector2(0.5f, 0.5f);
        _spriteRect.sizeDelta        = new Vector2(300f, 300f);
        _spriteRect.anchoredPosition = Vector2.zero;
        _spriteRect.localScale       = Vector3.zero;   // invisible until zoom-in
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Play the evolve cutscene, but only if <paramref name="evolutionKey"/> has not been seen before.
    /// On repeat evolutions of the same type the cutscene is skipped and
    /// <paramref name="onComplete"/> is invoked immediately.
    /// <paramref name="onComplete"/> is called after the zoom-out, before the particle burst,
    /// so the newly evolved troop is visible when the overlay fades away.
    /// </summary>
    public static void Play(Sprite from, Sprite to, Vector3 worldPos,
                            string evolutionKey, Action onComplete)
    {
        if (_seenEvolutions.Contains(evolutionKey))
        {
            // Already seen this evolution's cutscene — just evolve directly
            onComplete?.Invoke();
            return;
        }

        _seenEvolutions.Add(evolutionKey);

        if (Instance == null)
        {
            var go = new GameObject("EvolveCutscene");
            go.AddComponent<EvolveCutscene>();
        }
        Instance.StartCoroutine(Instance.RunCutscene(from, to, worldPos, onComplete));
    }

    // ── Cutscene coroutine ────────────────────────────────────────────────────

    IEnumerator RunCutscene(Sprite from, Sprite to, Vector3 worldPos, Action onComplete)
    {
        Time.timeScale = 0f;

        _spriteImage.sprite      = from;
        _spriteRect.localScale       = Vector3.zero;
        _spriteRect.localEulerAngles = Vector3.zero;

        // 1 ── Fade in black ─────────────────────────────────────────────────
        yield return FadePanel(0f, 1f, FadeInDuration);

        // 2 ── Zoom in (EaseOutBack = slight overshoot for a "charging up" feel)
        float t = 0f;
        while (t < ZoomInDuration)
        {
            t += Time.unscaledDeltaTime;
            float s = EaseOutBack(Mathf.Clamp01(t / ZoomInDuration));
            _spriteRect.localScale = Vector3.one * s;
            yield return null;
        }
        _spriteRect.localScale = Vector3.one;

        // 3 ── Accelerate rotation ────────────────────────────────────────────
        float angle = 0f;
        t = 0f;
        while (t < AccelDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / AccelDuration);
            float speed    = Mathf.Lerp(MinRotSpeed, MaxRotSpeed, progress * progress);
            angle += speed * Time.unscaledDeltaTime;
            _spriteRect.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        // 4 ── Swap to evolved sprite at peak speed ───────────────────────────
        _spriteImage.sprite = to;

        // 5 ── Decelerate rotation ────────────────────────────────────────────
        t = 0f;
        while (t < DecelDuration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / DecelDuration);
            float speed    = Mathf.Lerp(MaxRotSpeed, MinRotSpeed, progress * progress);
            angle += speed * Time.unscaledDeltaTime;
            _spriteRect.localEulerAngles = new Vector3(0f, 0f, angle);
            yield return null;
        }

        // 6 ── Zoom out (ease-in quad — snappy disappearance) ─────────────────
        t = 0f;
        while (t < ZoomOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / ZoomOutDuration);
            _spriteRect.localScale = Vector3.one * (1f - p * p);
            yield return null;
        }
        _spriteRect.localScale = Vector3.zero;

        // 7 ── Restore time and trigger evolution ─────────────────────────────
        Time.timeScale = 1f;
        onComplete?.Invoke();

        // 8 ── Fade out black overlay (game world reappears) ──────────────────
        yield return FadePanel(1f, 0f, FadeOutDuration);

        // 9 ── Exaggerated particle burst at the troop's world position ────────
        SpawnEvolveBurst(worldPos);
        yield return new WaitForSeconds(BurstHold);

        // Reset for next use
        var c = _blackPanel.color; c.a = 0f; _blackPanel.color = c;
        _spriteRect.localScale = Vector3.zero;
    }

    // ── Panel fade helper ─────────────────────────────────────────────────────

    IEnumerator FadePanel(float fromAlpha, float toAlpha, float duration)
    {
        float t = 0f;
        var   c = _blackPanel.color;
        while (t < duration)
        {
            t   += Time.unscaledDeltaTime;
            c.a  = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(t / duration));
            _blackPanel.color = c;
            yield return null;
        }
        c.a = toAlpha;
        _blackPanel.color = c;
    }

    // ── Particle burst ────────────────────────────────────────────────────────

    void SpawnEvolveBurst(Vector3 pos)
    {
        // Three layers for depth: gold core, white sparkles, cyan accents
        SpawnBurstLayer(pos, new Color(1.0f, 0.82f, 0.10f), 36, 2.5f,  8f,  0.14f, 0.34f, 0.5f, 1.3f);
        SpawnBurstLayer(pos, Color.white,                    24, 4.5f, 12f,  0.06f, 0.20f, 0.3f, 1.0f);
        SpawnBurstLayer(pos, new Color(0.35f, 0.88f, 1.0f), 20, 1.5f,  5f,  0.16f, 0.38f, 0.6f, 1.5f);

        // Two expanding rings — staggered for a cinematic "shockwave" feel
        StartCoroutine(DelayedRing(pos, new Color(1.0f, 0.82f, 0.10f), 0.00f, 0.75f, 1.60f));
        StartCoroutine(DelayedRing(pos, Color.white,                   0.20f, 0.60f, 1.10f));
    }

    void SpawnBurstLayer(Vector3 pos, Color color, int count,
                         float minSpeed, float maxSpeed,
                         float minSize,  float maxSize,
                         float minLife,  float maxLife)
    {
        var go = new GameObject("EvolveBurst");
        go.transform.position = pos;

        var ps   = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(minLife, maxLife);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor      = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.gravityModifier = 0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = count + 10;
        main.stopAction      = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.06f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(color, 0f), new(Color.white, 0.4f) },
            new GradientAlphaKey[] { new(1f, 0f), new(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size    = new ParticleSystem.MinMaxCurve(1f,
                           new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material         = new Material(Shader.Find("Sprites/Default"));
        psr.sortingLayerName = "Default";
        psr.sortingOrder     = 50;

        ps.Play();
    }

    IEnumerator DelayedRing(Vector3 pos, Color color, float delay, float duration, float maxRadius)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        var go = new GameObject("EvolveRing");
        go.transform.position = pos;
        go.AddComponent<EvolveImpactRing>().Init(color, duration, maxRadius);
    }

    // ── Easing ────────────────────────────────────────────────────────────────

    /// <summary>Overshoots slightly before settling — gives a "charging" feel to the zoom-in.</summary>
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
    }
}

// ── Expanding ring helper ─────────────────────────────────────────────────────

/// <summary>
/// Expanding line-renderer ring that fades and shrinks over time. Self-destructs.
/// Used for the shockwave rings in the evolve burst.
/// </summary>
public class EvolveImpactRing : MonoBehaviour
{
    private LineRenderer _ring;
    private Color        _color;
    private float        _timer;
    private float        _duration;
    private float        _maxRadius;

    private const int   Segments   = 32;
    private const float StartWidth = 0.10f;

    public void Init(Color color, float duration, float maxRadius)
    {
        _color     = color;
        _duration  = duration;
        _maxRadius = maxRadius;

        _ring = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = Segments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = StartWidth;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = color;
        _ring.material       = mat;
        _ring.sortingLayerName = "Default";
        _ring.sortingOrder   = 50;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        float t      = Mathf.Clamp01(_timer / _duration);
        float radius = Mathf.Lerp(0f, _maxRadius, t);
        float alpha  = Mathf.Lerp(0.9f, 0f, t);
        float width  = Mathf.Lerp(StartWidth, 0.005f, t);

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
