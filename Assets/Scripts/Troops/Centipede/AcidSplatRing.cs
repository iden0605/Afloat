using UnityEngine;

/// <summary>
/// Short-lived expanding ring that plays at an acid impact point.
/// Spawned by AcidProjectile.SpawnImpact() on a standalone GameObject.
/// Expands outward quickly, fades, then destroys itself.
/// </summary>
public class AcidSplatRing : MonoBehaviour
{
    private string _sortingLayer;
    private int    _sortingOrder;

    private LineRenderer _ring;
    private float        _timer;

    private const float Duration  = 0.28f;
    private const float MaxRadius = 0.38f;
    private const int   Segments  = 32;

    private static readonly Color RingColor = new Color(0.40f, 1.00f, 0.18f);

    // ── Public API ────────────────────────────────────────────

    /// <summary>Call immediately after AddComponent to configure and build the ring.</summary>
    public void Init(string sortingLayer, int sortingOrder)
    {
        _sortingLayer = sortingLayer;
        _sortingOrder = sortingOrder;
        BuildRing();
    }

    // ── Construction ──────────────────────────────────────────

    void BuildRing()
    {
        _ring = gameObject.AddComponent<LineRenderer>();
        _ring.useWorldSpace     = true;
        _ring.loop              = true;
        _ring.positionCount     = Segments;
        _ring.numCapVertices    = 0;
        _ring.numCornerVertices = 0;
        _ring.widthMultiplier   = 0.06f;

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color            = RingColor;
        _ring.material       = mat;
        _ring.sortingLayerName = _sortingLayer;
        _ring.sortingOrder     = _sortingOrder;
    }

    // ── Update ────────────────────────────────────────────────

    void Update()
    {
        if (_ring == null) return; // Init not yet called

        _timer += Time.deltaTime;
        float t      = Mathf.Clamp01(_timer / Duration);
        float radius = Mathf.Lerp(0f, MaxRadius, t);
        float alpha  = Mathf.Lerp(0.85f, 0f, t);
        float width  = Mathf.Lerp(0.06f, 0.012f, t);

        _ring.widthMultiplier = width;
        _ring.startColor = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);
        _ring.endColor   = new Color(RingColor.r, RingColor.g, RingColor.b, alpha);

        Vector3 centre = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float a = 2f * Mathf.PI * i / Segments;
            _ring.SetPosition(i,
                centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
