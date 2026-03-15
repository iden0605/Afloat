using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Central visual-effects controller for any sprite-based unit (troop or enemy).
/// Add to character prefabs alongside their SpriteRenderer.
///
/// On Awake it replaces the SpriteRenderer's material with a private instance of
/// Custom/SpriteEffects so:
///   - Outline (selection gold / hit red) is drawn around the sprite silhouette
///   - Dissolve-out animation plays on death before the GO is destroyed
///
/// EnemyHitFlash can still coexist — it tints via SpriteRenderer.color (vertex
/// colour), which the custom shader reads exactly like Sprites/Default does.
///
/// ─ How to integrate ─────────────────────────────────────────────────────────
///   EnemyHitFlash    → call FlashHitOutline() from Flash()
///   EnemyInstance    → call Dissolve(()=>Destroy(gameObject)) from Die()
///   TroopSelectionUI → ShowSelectionOutline() on Show(), HideOutline() on Hide()
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffectsController : MonoBehaviour
{
    [Header("Outline")]
    [SerializeField] private Color selectionOutlineColor = new Color(1.00f, 0.95f, 0.30f); // gold
    [SerializeField] private Color hitOutlineColor       = new Color(1.00f, 0.20f, 0.10f); // red
    [SerializeField] private float outlineThickness      = 1.5f;
    [SerializeField] private float hitOutlineDuration    = 0.12f;

    [Header("Dissolve")]
    [SerializeField] private Color dissolveEdgeColor  = new Color(1.00f, 0.45f, 0.05f);
    [SerializeField] private float dissolveEdgeWidth  = 0.05f;
    [SerializeField] private float dissolveNoiseScale = 8.0f;
    [SerializeField] private float dissolveDuration   = 0.55f;

    // ── Shader property IDs ───────────────────────────────────────────────────
    static readonly int ID_OutlineColor     = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_OutlineThickness = Shader.PropertyToID("_OutlineThickness");
    static readonly int ID_OutlineEnabled   = Shader.PropertyToID("_OutlineEnabled");
    static readonly int ID_DissolveProgress = Shader.PropertyToID("_DissolveProgress");
    static readonly int ID_DissolveEdge     = Shader.PropertyToID("_DissolveEdgeColor");
    static readonly int ID_DissolveWidth    = Shader.PropertyToID("_DissolveEdgeWidth");
    static readonly int ID_DissolveScale    = Shader.PropertyToID("_DissolveScale");

    // ── State ─────────────────────────────────────────────────────────────────
    private SpriteRenderer _sr;
    private Material       _mat;
    private bool           _selectionOutlineActive;
    private Coroutine      _outlineFlashRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        InstallMaterial();
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show a persistent gold outline (e.g. troop selected).</summary>
    public void ShowSelectionOutline()
    {
        _selectionOutlineActive = true;
        ApplyOutline(selectionOutlineColor);
    }

    /// <summary>Disable all outline.</summary>
    public void HideOutline()
    {
        _selectionOutlineActive = false;
        if (_outlineFlashRoutine != null)
        {
            StopCoroutine(_outlineFlashRoutine);
            _outlineFlashRoutine = null;
        }
        if (_mat != null) _mat.SetFloat(ID_OutlineEnabled, 0f);
    }

    /// <summary>
    /// Briefly flash a red hit outline. Restores selection outline if it was active.
    /// Called by EnemyHitFlash.Flash().
    /// </summary>
    public void FlashHitOutline()
    {
        if (_mat == null) return;
        if (_outlineFlashRoutine != null) StopCoroutine(_outlineFlashRoutine);
        _outlineFlashRoutine = StartCoroutine(DoOutlineFlash());
    }

    /// <summary>
    /// Play the dissolve-out animation, then invoke <paramref name="onComplete"/>.
    /// Caller should pass ()=>Destroy(gameObject) as the completion callback.
    /// </summary>
    public void Dissolve(Action onComplete = null)
    {
        if (_mat == null) { onComplete?.Invoke(); return; }
        StartCoroutine(DoDissolve(onComplete));
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    void ApplyOutline(Color c)
    {
        if (_mat == null) return;
        _mat.SetColor(ID_OutlineColor,     c);
        _mat.SetFloat(ID_OutlineThickness, outlineThickness);
        _mat.SetFloat(ID_OutlineEnabled,   1f);
    }

    IEnumerator DoOutlineFlash()
    {
        ApplyOutline(hitOutlineColor);
        yield return new WaitForSeconds(hitOutlineDuration);
        _outlineFlashRoutine = null;

        // Restore previous outline state
        if (_selectionOutlineActive) ApplyOutline(selectionOutlineColor);
        else if (_mat != null)       _mat.SetFloat(ID_OutlineEnabled, 0f);
    }

    IEnumerator DoDissolve(Action onComplete)
    {
        if (_mat == null) { onComplete?.Invoke(); yield break; }

        _mat.SetColor(ID_DissolveEdge,     dissolveEdgeColor);
        _mat.SetFloat(ID_DissolveWidth,    dissolveEdgeWidth);
        _mat.SetFloat(ID_DissolveScale,    dissolveNoiseScale);
        _mat.SetFloat(ID_OutlineEnabled,   0f);

        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            _mat.SetFloat(ID_DissolveProgress, Mathf.Clamp01(elapsed / dissolveDuration));
            yield return null;
        }

        onComplete?.Invoke();
    }

    void InstallMaterial()
    {
        var shader = Shader.Find("Custom/SpriteEffects");
        if (shader == null)
        {
            Debug.LogWarning("[SpriteEffectsController] Shader 'Custom/SpriteEffects' not found. " +
                             "Make sure Assets/Shaders/SpriteEffects.shader exists in the project.");
            return;
        }

        _mat             = new Material(shader);
        _mat.name        = $"{gameObject.name}_SpriteEffects";
        _mat.mainTexture = _sr.sprite != null ? _sr.sprite.texture : null;

        // Carry over any existing tint on the renderer
        _mat.SetColor("_Color", _sr.color);

        // All effects off
        _mat.SetFloat(ID_OutlineEnabled,   0f);
        _mat.SetFloat(ID_DissolveProgress, 0f);

        _sr.material = _mat;
    }
}
