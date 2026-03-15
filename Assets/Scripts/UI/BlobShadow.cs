using UnityEngine;

/// <summary>
/// Adds a soft circular shadow beneath any sprite-based unit (enemy or troop).
/// Add to the base Enemy prefab and troop prefabs.
///
/// Sorting order is auto-detected from the parent's SpriteRenderer (one below it),
/// so the shadow always appears behind the unit regardless of the scene setup.
///
/// Setup: just add this component — it creates its own child SpriteRenderer.
/// </summary>
public class BlobShadow : MonoBehaviour
{
    [Tooltip("Width of the shadow oval in world units.")]
    [SerializeField] private float  shadowWidth   = 0.55f;
    [Tooltip("Height of the shadow oval in world units (smaller = more foreshortened).")]
    [SerializeField] private float  shadowHeight  = 0.22f;
    [Tooltip("Opacity of the shadow (0 = invisible, 1 = fully black).")]
    [SerializeField] private float  shadowAlpha   = 0.50f;
    [Tooltip("Vertical offset from the parent's pivot (negative = below).")]
    [SerializeField] private float  yOffset       = -0.05f;

    private Transform _shadowRoot;
    private static Sprite _shadowSprite;

    void Awake()
    {
        // Create the shadow sprite once and cache it for all instances
        if (_shadowSprite == null)
            _shadowSprite = CreateCircleSprite(64);

        // Match the parent sprite's sorting layer/order so the shadow always
        // sits directly behind the unit, regardless of how the scene is layered.
        var parentSr     = GetComponent<SpriteRenderer>();
        string layerName = parentSr != null ? parentSr.sortingLayerName : "Default";
        int    order     = parentSr != null ? parentSr.sortingOrder - 1  : 0;

        var go = new GameObject("BlobShadow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, yOffset, 0f);
        go.transform.localScale    = new Vector3(shadowWidth, shadowHeight, 1f);

        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = _shadowSprite;
        sr.color            = new Color(0f, 0f, 0f, shadowAlpha);
        sr.sortingLayerName = layerName;
        sr.sortingOrder     = order;

        _shadowRoot = go.transform;
    }

    void LateUpdate()
    {
        // Counter-rotate so the oval always stays flat regardless of parent rotation
        if (_shadowRoot != null)
            _shadowRoot.rotation = Quaternion.identity;
    }

    // Creates a 64×64 circular gradient Texture2D (opaque black centre → transparent edge)
    static Sprite CreateCircleSprite(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float centre = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx    = (x - centre) / centre;
            float dy    = (y - centre) / centre;
            float dist  = Mathf.Sqrt(dx * dx + dy * dy); // 0 at centre, 1 at edge, >1 outside
            float alpha = Mathf.Clamp01(1f - dist);
            alpha = alpha * alpha; // quadratic falloff for softer edge
            tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
