using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the Enemy base prefab alongside EnemyInstance.
/// Briefly flashes the SpriteRenderer to a warm-white on every hit.
/// Called from EnemyInstance.ApplyRaw() after damage is confirmed.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHitFlash : MonoBehaviour
{
    [Tooltip("Colour the sprite instantly snaps to on each hit.")]
    [SerializeField] private Color flashColor    = new Color(1.00f, 0.88f, 0.76f);
    [Tooltip("Seconds to fade back from flashColor to the original sprite colour.")]
    [SerializeField] private float flashDuration = 0.10f;

    private SpriteRenderer          _sr;
    private Color                   _originalColor;
    private Coroutine               _routine;
    private SpriteEffectsController _effects;

    void Awake()
    {
        _sr            = GetComponent<SpriteRenderer>();
        _originalColor = _sr.color;
        _effects       = GetComponent<SpriteEffectsController>();
    }

    /// <summary>Start the flash. Interrupts any flash already in progress.</summary>
    public void Flash()
    {
        if (_sr == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(DoFlash());

        // Red outline flash (handled by SpriteEffectsController if present)
        _effects?.FlashHitOutline();
    }

    IEnumerator DoFlash()
    {
        _sr.color = flashColor;
        // Restore to status tint if an effect is active, otherwise to the original sprite colour
        Color restoreTarget = GetComponent<EnemyVisualEffects>()?.ActiveTint ?? _originalColor;
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            _sr.color = Color.Lerp(flashColor, restoreTarget, t / flashDuration);
            yield return null;
        }
        _sr.color = restoreTarget;
        _routine  = null;
    }
}
