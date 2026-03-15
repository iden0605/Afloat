using UnityEngine;

/// <summary>
/// Draws the attack-range visualisation for a placed troop.
/// Supports two modes:
///   • Circle  (default) — a filled disk + ring border, set via SetRadius().
///   • Rect             — a filled rectangle + border, set via SetRect().
///                        Rotation is controlled externally by setting transform.rotation.
///
/// TroopSelectionUI and TroopDragController call SetRadius / SetRect and SetVisible.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RangeIndicator : MonoBehaviour
{
    [Tooltip("Opacity of the filled shape (0 = invisible, 1 = solid)")]
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.22f;

    [Tooltip("Opacity of the border")]
    [SerializeField, Range(0f, 1f)] private float ringAlpha = 0.65f;

    [Tooltip("Sorting layer name — must match the layer your troop sprites use")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Sorting order within that layer — set higher than your background sprites")]
    [SerializeField] private int sortingOrder = 10;

    private const int CircleSegments = 72;

    private MeshFilter   _meshFilter;
    private MeshRenderer _meshRenderer;
    private LineRenderer _border;
    private bool         _initialized;

    private Transform _originalParent;
    private Quaternion _originalLocalRotation;

    void Awake() => Initialize();

    void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _meshFilter   = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        var fillMat = new Material(Shader.Find("Sprites/Default"));
        fillMat.color = new Color(0.05f, 0.05f, 0.05f, fillAlpha);
        _meshRenderer.material         = fillMat;
        _meshRenderer.sortingLayerName = sortingLayerName;
        _meshRenderer.sortingOrder     = sortingOrder;

        _border = GetComponent<LineRenderer>();
        if (_border == null) _border = gameObject.AddComponent<LineRenderer>();

        _border.useWorldSpace    = false;
        _border.loop             = true;
        _border.widthMultiplier  = 0.05f;
        _border.sortingLayerName = sortingLayerName;
        _border.sortingOrder     = sortingOrder + 1;

        var borderMat = new Material(Shader.Find("Sprites/Default"));
        borderMat.color = new Color(0.08f, 0.08f, 0.08f, ringAlpha);
        _border.material = borderMat;
    }

    // ── Circle mode ───────────────────────────────────────────────────────────

    /// <summary>Sets the indicator to circle mode with the given radius.</summary>
    public void SetRadius(float radius)
    {
        Initialize();
        _meshFilter.mesh = BuildCircleMesh(radius);

        _border.positionCount    = CircleSegments;
        _border.numCapVertices   = 0;
        _border.numCornerVertices = 0;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = 2f * Mathf.PI * i / CircleSegments;
            _border.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }

    // ── Rectangle mode ────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the indicator to rectangle mode.
    /// <paramref name="halfLong"/>  = half the long  dimension (perpendicular to path, local X).
    /// <paramref name="halfShort"/> = half the short dimension (along path,            local Y).
    /// Rotate transform externally to align with the path.
    /// </summary>
    public void SetRect(float halfLong, float halfShort)
    {
        Initialize();
        _meshFilter.mesh = BuildRectMesh(halfLong, halfShort);

        _border.positionCount     = 4;
        _border.numCapVertices    = 2;
        _border.numCornerVertices = 2;
        _border.SetPosition(0, new Vector3(-halfLong, -halfShort, 0f));
        _border.SetPosition(1, new Vector3( halfLong, -halfShort, 0f));
        _border.SetPosition(2, new Vector3( halfLong,  halfShort, 0f));
        _border.SetPosition(3, new Vector3(-halfLong,  halfShort, 0f));
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows or hides the indicator.
    /// When showing, optionally repositions it to <paramref name="atWorldPosition"/> and
    /// orients it to <paramref name="atWorldRotation"/> (identity = no rotation).
    /// </summary>
    public void SetVisible(bool visible, Vector3? atWorldPosition = null, Quaternion? atWorldRotation = null)
    {
        if (visible)
        {
            Initialize();
            _originalParent        = transform.parent;
            _originalLocalRotation = transform.localRotation;

            Vector3    worldPos = atWorldPosition ?? transform.position;
            Quaternion worldRot = atWorldRotation ?? Quaternion.identity;

            transform.SetParent(null, false);
            transform.position = worldPos;
            transform.rotation = worldRot;
        }
        else
        {
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            _originalParent = null;
        }

        gameObject.SetActive(visible);
    }

    // ── Mesh builders ─────────────────────────────────────────────────────────

    static Mesh BuildCircleMesh(float radius)
    {
        int n     = CircleSegments;
        var verts = new Vector3[n + 1];
        var tris  = new int[n * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            float a = 2f * Mathf.PI * i / n;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }
        for (int i = 0; i < n; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % n + 1;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    static Mesh BuildRectMesh(float halfLong, float halfShort)
    {
        var verts = new Vector3[]
        {
            new Vector3(-halfLong, -halfShort, 0f),
            new Vector3( halfLong, -halfShort, 0f),
            new Vector3( halfLong,  halfShort, 0f),
            new Vector3(-halfLong,  halfShort, 0f),
        };
        var tris = new int[] { 0, 2, 1,  0, 3, 2 };

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }
}
