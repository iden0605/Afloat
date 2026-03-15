using System.Collections;
using UnityEngine;

/// <summary>
/// Adds a scale-punch (squash &amp; stretch) reaction to any sprite.
/// Call Punch() from attack scripts and damage handlers.
///
/// Common values:
///   Land / slam  → Punch(1.35f, 0.65f, 0.12f)   wide squash on impact
///   Attack swing → Punch(0.75f, 1.30f, 0.08f)   tall stretch on wind-up
///   Hit react    → Punch(1.20f, 0.80f, 0.10f)   quick squash from damage
/// </summary>
public class SquashStretch : MonoBehaviour
{
    [Tooltip("Seconds the punch lerp takes to settle back to base scale.")]
    [SerializeField] private float defaultSettleDuration = 0.18f;

    private Vector3   _baseScale;
    private Coroutine _routine;

    void Awake() => _baseScale = transform.localScale;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a scale punch then smoothly return to base scale.
    /// xMult and yMult are multipliers of the base scale (1 = no change).
    /// Volume is preserved if xMult * yMult ≈ 1 (e.g. 1.35 × 0.74 ≈ 1).
    /// </summary>
    public void Punch(float xMult, float yMult, float settleDuration = -1f)
    {
        if (settleDuration < 0f) settleDuration = defaultSettleDuration;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(DoPunch(xMult, yMult, settleDuration));
    }

    /// <summary>Convenience: named presets for common situations.</summary>
    public void PunchLand()        => Punch(1.35f, 0.65f, 0.14f);
    public void PunchAttackSwing() => Punch(0.75f, 1.30f, 0.09f);
    public void PunchHitReact()    => Punch(1.20f, 0.80f, 0.10f);

    // ── Internal ─────────────────────────────────────────────────────────────

    IEnumerator DoPunch(float xMult, float yMult, float settleDuration)
    {
        // Snap to punch scale immediately
        transform.localScale = new Vector3(
            _baseScale.x * xMult,
            _baseScale.y * yMult,
            _baseScale.z);

        // Smoothly settle back using ease-out
        float elapsed = 0f;
        Vector3 punchScale = transform.localScale;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad
            transform.localScale = Vector3.Lerp(punchScale, _baseScale, ease);
            yield return null;
        }

        transform.localScale = _baseScale;
        _routine = null;
    }
}
