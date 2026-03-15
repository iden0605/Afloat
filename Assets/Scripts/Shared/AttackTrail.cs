using UnityEngine;

/// <summary>
/// Manages a TrailRenderer that activates during an attack phase.
/// Add to the troop GO (or a specific child bone/hand) and configure
/// via Inspector. Attack scripts call StartTrail() / StopTrail().
///
/// The trail is set up entirely in code so no prefab changes are needed.
///
/// Suggested settings per troop:
///   Mantis  — cyan/green, time 0.15, startWidth 0.06
///   Beetle  — amber/brown, time 0.20, startWidth 0.10
///   Ant     — orange, time 0.10, startWidth 0.04
/// </summary>
public class AttackTrail : MonoBehaviour
{
    [Header("Trail Appearance")]
    [SerializeField] private Color startColor  = new Color(1.0f, 0.9f, 0.3f, 0.9f);
    [SerializeField] private Color endColor    = new Color(1.0f, 0.5f, 0.1f, 0.0f);
    [SerializeField] private float trailTime   = 0.15f;   // seconds trail persists
    [SerializeField] private float startWidth  = 0.07f;
    [SerializeField] private float endWidth    = 0.01f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int    sortingOrder = 8;

    private TrailRenderer _trail;

    void Awake()
    {
        _trail = gameObject.AddComponent<TrailRenderer>();
        _trail.time         = trailTime;
        _trail.startWidth   = startWidth;
        _trail.endWidth     = endWidth;
        _trail.minVertexDistance = 0.02f;
        _trail.autodestruct = false;
        _trail.emitting     = false;

        // Gradient: opaque at head → transparent at tail
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor,   1f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(0f,            1f),
            });
        _trail.colorGradient = gradient;

        // Material — additive blend looks great for energy trails
        var shader = Shader.Find("Legacy Shaders/Particles/Additive")
                  ?? Shader.Find("Particles/Additive")
                  ?? Shader.Find("Sprites/Default");
        if (shader != null)
            _trail.material = new Material(shader);

        _trail.sortingLayerName = sortingLayer;
        _trail.sortingOrder     = sortingOrder;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Begin emitting trail (call at start of attack lunge/swing).</summary>
    public void StartTrail()
    {
        if (_trail == null) return;
        _trail.Clear();    // flush stale geometry from previous attack
        _trail.emitting = true;
    }

    /// <summary>Stop emitting new trail vertices (tail will still fade out naturally).</summary>
    public void StopTrail()
    {
        if (_trail != null) _trail.emitting = false;
    }

    /// <summary>Stop emitting AND immediately clear all trail geometry.</summary>
    public void ClearTrail()
    {
        if (_trail == null) return;
        _trail.emitting = false;
        _trail.Clear();
    }
}
