using UnityEngine;

/// <summary>
/// Applies a gentle sine-wave colour tint to the attached SpriteRenderer,
/// breathing between two colours so the background feels alive.
///
/// Works with any background sprite. Keep the colour swing subtle —
/// the default values shift the pond background between a warm afternoon
/// tint and a cool underwater tint over ~8 seconds.
///
/// Add to each background layer GO you want to pulse (or just the main
/// background sprite).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundColorPulse : MonoBehaviour
{
    [Tooltip("The base/neutral sprite colour (usually Color.white to keep original art).")]
    [SerializeField] private Color colorA = new Color(1.00f, 0.98f, 0.94f); // warm ivory

    [Tooltip("The alternate colour the sprite drifts toward at the other end of the sine wave.")]
    [SerializeField] private Color colorB = new Color(0.90f, 0.96f, 1.00f); // cool blue-white

    [Tooltip("Seconds for one full warm→cool→warm cycle.")]
    [SerializeField] private float period = 8f;

    [Tooltip("Phase offset so layered backgrounds don't pulse in sync (use different values per layer).")]
    [SerializeField] private float phaseOffset = 0f;

    private SpriteRenderer _sr;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>Called by BackgroundEnhancer to override serialized values at runtime.</summary>
    public void Configure(Color colorA, Color colorB, float period, float phaseOffset)
    {
        this.colorA      = colorA;
        this.colorB      = colorB;
        this.period      = period;
        this.phaseOffset = phaseOffset;
    }

    void Update()
    {
        // t oscillates 0→1→0 over `period` seconds
        float t = (Mathf.Sin((Time.time / period + phaseOffset) * Mathf.PI * 2f) + 1f) * 0.5f;
        _sr.color = Color.Lerp(colorA, colorB, t);
    }
}
