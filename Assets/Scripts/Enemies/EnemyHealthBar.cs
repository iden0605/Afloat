using UnityEngine;

/// <summary>
/// World-space health bar rendered above the enemy using two SpriteRenderer quads
/// (background + fill). The fill shrinks from the right as health decreases and
/// shifts colour from green → red at low health.
///
/// Attach to the base Enemy prefab alongside EnemyInstance.
/// The bar is hidden until Initialize() is called by EnemyInstance.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Total width of the bar in world units.")]
    [SerializeField] private float barWidth  = 0.55f;
    [Tooltip("Height of the bar in world units.")]
    [SerializeField] private float barHeight = 0.07f;
    [Tooltip("Vertical offset above the enemy pivot in world units.")]
    [SerializeField] private float yOffset   = 0.42f;

    [Header("Colors")]
    [SerializeField] private Color fullColor = new Color(0.18f, 0.80f, 0.28f, 1f); // green
    [SerializeField] private Color lowColor  = new Color(0.90f, 0.22f, 0.22f, 1f); // red
    [SerializeField] private Color bgColor   = new Color(0.12f, 0.12f, 0.12f, 0.85f);

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    bgSortingOrder   = 10;
    [SerializeField] private int    fillSortingOrder = 11;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private GameObject     _barRoot;     // container — toggled to show/hide the whole bar
    private Transform      _fillPivot;   // anchored at the bar's left edge
    private SpriteRenderer _fillSr;

    private static Sprite _whiteSprite;  // shared 1×1 white pixel sprite

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildBar();
        _barRoot.SetActive(false); // hidden until Initialize()
    }

    void LateUpdate()
    {
        // Counter-rotate the bar so it stays axis-aligned regardless of the enemy's rotation
        _barRoot.transform.rotation = Quaternion.identity;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    void BuildBar()
    {
        Sprite px = GetWhiteSprite();

        // Root container — hiding this hides the entire bar
        _barRoot = new GameObject("HealthBar");
        _barRoot.transform.SetParent(transform, false);

        // ── Background ──────────────────────────────────────
        var bg = CreateQuad("HP_BG", _barRoot.transform, px, bgColor, bgSortingOrder);
        bg.localPosition = new Vector3(0f, yOffset, 0f);
        bg.localScale    = new Vector3(barWidth, barHeight, 1f);

        // ── Fill ────────────────────────────────────────────
        // _fillPivot anchors the fill at the bar's left edge.
        // Scaling _fillPivot on X from 0→1 shrinks the fill leftward.
        _fillPivot = new GameObject("HP_FillPivot").transform;
        _fillPivot.SetParent(_barRoot.transform, false);
        _fillPivot.localPosition = new Vector3(-barWidth * 0.5f, yOffset, -0.005f);

        // The visual is offset +X by half the bar width so it starts at the pivot edge
        var fillVisual = CreateQuad("HP_Fill", _fillPivot, px, fullColor, fillSortingOrder);
        fillVisual.localPosition = new Vector3(barWidth * 0.5f, 0f, 0f);
        fillVisual.localScale    = new Vector3(barWidth, barHeight, 1f);
        _fillSr = fillVisual.GetComponent<SpriteRenderer>();
    }

    Transform CreateQuad(string goName, Transform parent, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var sr              = go.AddComponent<SpriteRenderer>();
        sr.sprite           = sprite;
        sr.color            = color;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder     = order;
        return go.transform;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>Reveals the bar and sets it to full. Called by EnemyInstance.Initialize().</summary>
    public void Initialize(float maxHealth)
    {
        SetFill(1f);
        _barRoot.SetActive(true);
    }

    /// <summary>
    /// Updates the bar. <paramref name="fraction"/> is in [0,1] where 1 = full health.
    /// Called by EnemyInstance each time damage is taken.
    /// </summary>
    public void SetFill(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);

        // Scale the pivot container: fraction=1 shows full bar, fraction=0 collapses it
        _fillPivot.localScale = new Vector3(fraction, 1f, 1f);

        // Lerp colour: green at full health → red when almost dead
        _fillSr.color = Color.Lerp(lowColor, fullColor, fraction);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return _whiteSprite;
    }
}
