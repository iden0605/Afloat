using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a red screen-flash whenever the player loses health (enemy escapes the path).
/// Creates its own full-screen Canvas overlay at runtime — no prefab required.
///
/// ── Scene setup ──
///   Add this component to any scene GameObject (e.g. the same one as ScreenVignette).
///   Sort order 10 puts it above the vignette (5) but below UI panels (UIDocuments).
/// </summary>
public class ScreenDamageFlash : MonoBehaviour
{
    [Tooltip("Peak alpha of the red overlay on the first flash beat (0–1).")]
    [SerializeField] private float peakAlpha      = 0.48f;
    [Tooltip("Sort order for the flash canvas. Keep above ScreenVignette (5) and below UI panels.")]
    [SerializeField] private int   canvasSortOrder = 10;

    private Image     _overlay;
    private Coroutine _flashCoroutine;
    private int       _lastHealth = int.MaxValue;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        BuildOverlay();
        // Capture the actual starting health after all Start()s have run
        _lastHealth = PlayerHealthManager.Instance?.CurrentHealth ?? int.MaxValue;
    }

    void OnEnable()  => PlayerHealthManager.OnHealthChanged += OnHealthChanged;
    void OnDisable() => PlayerHealthManager.OnHealthChanged -= OnHealthChanged;

    // ── Health callback ───────────────────────────────────────────────────────

    void OnHealthChanged()
    {
        int current = PlayerHealthManager.Instance?.CurrentHealth ?? 0;

        // Only flash when health actually decreases (ignore initial set / future healing)
        if (current >= _lastHealth) { _lastHealth = current; return; }
        _lastHealth = current;

        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    IEnumerator FlashRoutine()
    {
        // Ba-dum heartbeat: sharp spike → partial decay → secondary bump → fade out
        yield return Fade(0f,        peakAlpha,        0.06f);   // spike in
        yield return Fade(peakAlpha, peakAlpha * 0.28f, 0.10f);  // quick drop
        yield return Fade(peakAlpha * 0.28f, peakAlpha * 0.55f, 0.07f); // second beat
        yield return Fade(peakAlpha * 0.55f, 0f,       0.38f);   // slow fade out
        _flashCoroutine = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        var   c       = _overlay.color;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            _overlay.color = c;
            yield return null;
        }
        c.a = to;
        _overlay.color = c;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    void BuildOverlay()
    {
        var canvas          = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;
        gameObject.AddComponent<CanvasScaler>();

        var imgGO = new GameObject("DamageFlashOverlay");
        imgGO.transform.SetParent(transform, false);

        _overlay               = imgGO.AddComponent<Image>();
        _overlay.color         = new Color(0.78f, 0.04f, 0.04f, 0f);
        _overlay.raycastTarget = false;

        var rect       = imgGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
