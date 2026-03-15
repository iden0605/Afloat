using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a subtle dark vignette at the screen edges for atmosphere.
/// Place on any scene GameObject — it creates its own Canvas and Image at runtime.
///
/// The Canvas uses sort order 5, which puts it above the game world (SpriteRenderers)
/// but below all UIDocuments (sidebar, gold HUD, etc.) so only the game world is vignetted.
/// </summary>
public class ScreenVignette : MonoBehaviour
{
    [Tooltip("Maximum opacity of the vignette at the screen corners (0–1).")]
    [SerializeField] private float  intensity     = 0.55f;
    [Tooltip("Inner radius of the transparent zone (0 = tight, 1 = full screen).")]
    [SerializeField] private float  innerRadius   = 0.45f;
    [Tooltip("Tint colour of the vignette edges (very dark teal fits a pond theme).")]
    [SerializeField] private Color  vignetteColor = new Color(0.00f, 0.04f, 0.07f, 1f);
    [Tooltip("Canvas sort order — keep below UIDocument panels (default 5).")]
    [SerializeField] private int    canvasSortOrder = 5;
    [Tooltip("Texture resolution. 128 is plenty; increase only if edges look pixelated.")]
    [SerializeField] private int    textureSize   = 128;

    void Start()
    {
        BuildVignette();
    }

    void BuildVignette()
    {
        // Canvas
        var canvas             = gameObject.AddComponent<Canvas>();
        canvas.renderMode      = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder    = canvasSortOrder;
        gameObject.AddComponent<CanvasScaler>();

        // Full-screen Image
        var imgGo = new GameObject("VignetteImage");
        imgGo.transform.SetParent(transform, false);

        var img  = imgGo.AddComponent<Image>();
        img.sprite        = CreateVignetteSprite(textureSize, innerRadius, intensity, vignetteColor);
        img.raycastTarget = false;

        var rect = imgGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Sprite CreateVignetteSprite(int size, float inner, float maxAlpha, Color tint)
    {
        var tex        = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float centre = size * 0.5f;
        float outer  = Mathf.Sqrt(2f) * 0.5f; // distance from centre to corner (normalised)

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Normalised coords [-0.5, 0.5]
            float nx = (x - centre) / size;
            float ny = (y - centre) / size;
            float dist = Mathf.Sqrt(nx * nx + ny * ny); // 0 at centre, ~0.707 at corner

            // Remap so alpha is 0 inside innerRadius and rises to maxAlpha at the corners
            float t     = Mathf.InverseLerp(inner * outer, outer, dist);
            float alpha = Mathf.Clamp01(t * t) * maxAlpha;

            tex.SetPixel(x, y, new Color(tint.r, tint.g, tint.b, alpha));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
