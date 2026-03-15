using UnityEngine;

/// <summary>
/// Mouse-position parallax for a static-camera scene.
/// Attach to each background layer sprite. Layers further back (lower parallaxStrength)
/// move less, creating a convincing depth illusion without moving the camera.
///
/// Setup:
///   1. Add to each background layer GO.
///   2. Set parallaxStrength: 0 = no movement (foreground), 0.02 = subtle mid, 0.06 = far back.
///   3. The component records the GO's initial world position as the neutral anchor.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("How far (world units) the layer shifts per normalized mouse unit. 0 = locked, 0.06 = far background.")]
    [SerializeField] private float parallaxStrength = 0.04f;

    [Tooltip("How fast the layer catches up to the target position (lower = dreamier lag).")]
    [SerializeField] private float smoothing = 4f;

    private Vector3 _anchor;     // rest position in world space
    private Camera  _cam;

    void Start()
    {
        _anchor = transform.position;
        _cam    = Camera.main;
    }

    /// <summary>Called by BackgroundEnhancer to override serialized values at runtime.</summary>
    public void Configure(float strength, float smoothing)
    {
        parallaxStrength  = strength;
        this.smoothing    = smoothing;
    }

    void Update()
    {
        if (_cam == null) return;

        // Normalise mouse to [-1, 1] relative to screen centre
        Vector2 screen     = new Vector2(Screen.width, Screen.height);
        Vector2 mouseNorm  = ((Vector2)Input.mousePosition - screen * 0.5f) / (screen * 0.5f);

        Vector3 target = _anchor + new Vector3(mouseNorm.x, mouseNorm.y, 0f) * parallaxStrength;
        transform.position = Vector3.Lerp(transform.position, target, smoothing * Time.deltaTime);
    }
}
