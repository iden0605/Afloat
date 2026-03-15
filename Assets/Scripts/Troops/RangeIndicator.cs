using UnityEngine;

/// <summary>
/// Draws a filled semi-transparent disk + a thin ring border to show a troop's attack range.
/// Attach to a child GameObject on every troop prefab.
/// TroopSelectionUI calls SetRadius() + SetVisible() when the player selects / deselects a troop.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RangeIndicator : MonoBehaviour
{
    [Tooltip("Opacity of the filled disk (0 = invisible, 1 = solid)")]
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.22f;

    [Tooltip("Opacity of the ring border")]
    [SerializeField, Range(0f, 1f)] private float ringAlpha = 0.65f;

    [Tooltip("Sorting layer name — must match the layer your troop sprites use")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("Sorting order within that layer — set higher than your background sprites")]
    [SerializeField] private int sortingOrder = 10;

    private const int Segments = 72;

    private MeshFilter   _meshFilter;
    private MeshRenderer _meshRenderer;
    private LineRenderer _ring;
    private bool         _initialized;

    private Transform _originalParent;

    void Awake() => Initialize();

    void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _meshFilter   = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();

        // ── Filled disk material ──────────────────────────────
        var fillMat = new Material(Shader.Find("Sprites/Default"));
        fillMat.color = new Color(0.05f, 0.05f, 0.05f, fillAlpha);
        _meshRenderer.material          = fillMat;
        _meshRenderer.sortingLayerName  = sortingLayerName;
        _meshRenderer.sortingOrder      = sortingOrder;

        // ── Ring border via LineRenderer ──────────────────────
        _ring = GetComponent<LineRenderer>();
        if (_ring == null) _ring = gameObject.AddComponent<LineRenderer>();

        _ring.useWorldSpace   = false;
        _ring.loop            = true;
        _ring.widthMultiplier = 0.05f;
        _ring.positionCount   = Segments;
        _ring.sortingLayerName = sortingLayerName;
        _ring.sortingOrder     = sortingOrder + 1;

        var ringMat = new Material(Shader.Find("Sprites/Default"));
        ringMat.color  = new Color(0.08f, 0.08f, 0.08f, ringAlpha);
        _ring.material = ringMat;
    }

    public void SetRadius(float radius)
    {
        Initialize();
        _meshFilter.mesh = BuildDiskMesh(radius);
        BuildRing(radius);
    }

    public void SetVisible(bool visible, Vector3? atWorldPosition = null)
    {
        if (visible)
        {
            Initialize();
            // Always re-capture the parent in case we were re-parented since last hide.
            _originalParent = transform.parent;
            Vector3 worldPos = atWorldPosition ?? transform.position;
            transform.SetParent(null, false);
            transform.position = worldPos;
        }
        else
        {
            if (_originalParent != null)
            {
                transform.SetParent(_originalParent, false);
                transform.localPosition = Vector3.zero;
            }
            _originalParent = null;
        }

        gameObject.SetActive(visible);
    }

    // -------------------------------------------------------
    // Mesh helpers
    // -------------------------------------------------------

    Mesh BuildDiskMesh(float radius)
    {
        var verts = new Vector3[Segments + 1];
        var tris  = new int[Segments * 3];

        verts[0] = Vector3.zero;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
        }

        for (int i = 0; i < Segments; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % Segments + 1;
        }

        var mesh = new Mesh();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    void BuildRing(float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }
}
