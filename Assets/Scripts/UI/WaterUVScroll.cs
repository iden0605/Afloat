using UnityEngine;

/// <summary>
/// Scrolls the mainTextureOffset of the attached SpriteRenderer's material over time,
/// simulating water ripple / caustic shimmer on background water layers.
///
/// IMPORTANT: The sprite's texture must have Wrap Mode = Repeat in the Import Settings,
/// otherwise the UV scroll has no visible effect (it wraps off the edge into solid colour).
///
/// Setup:
///   Add to a water-background sprite GO. The component will automatically call
///   renderer.material (creating a unique instance) so other objects using the same
///   sprite atlas are not affected.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WaterUVScroll : MonoBehaviour
{
    [Header("Scroll Speed")]
    [Tooltip("Horizontal UV scroll speed (positive = right).")]
    [SerializeField] private float scrollX = 0.015f;
    [Tooltip("Vertical UV scroll speed.")]
    [SerializeField] private float scrollY = 0.005f;

    [Header("Wave Distortion (optional)")]
    [Tooltip("Additional sine-wave oscillation amplitude added on top of the base scroll.")]
    [SerializeField] private float waveAmplitude  = 0.004f;
    [Tooltip("How many complete wave cycles per second.")]
    [SerializeField] private float waveFrequency  = 0.6f;

    private Material _mat;
    private float    _time;

    /// <summary>Called by BackgroundEnhancer to override serialized values at runtime.</summary>
    public void Configure(float x, float y)
    {
        scrollX = x;
        scrollY = y;
    }

    void Start()
    {
        // .material creates a unique material instance — won't affect the shared atlas material
        _mat = GetComponent<SpriteRenderer>().material;
    }

    void Update()
    {
        _time += Time.deltaTime;

        float wave = Mathf.Sin(_time * waveFrequency * Mathf.PI * 2f) * waveAmplitude;

        _mat.mainTextureOffset = new Vector2(
            _time * scrollX + wave,
            _time * scrollY);
    }

    void OnDestroy()
    {
        // Avoid material leak — Unity does not auto-destroy instanced materials
        if (_mat != null) Destroy(_mat);
    }
}
