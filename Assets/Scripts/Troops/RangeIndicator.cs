using UnityEngine;

/// <summary>
/// Draws a world-space circle around a troop to visualise its attack range.
/// Attach to a child GameObject on Ally.prefab alongside a LineRenderer.
/// Call SetRadius() once after placement (or after each upgrade), then
/// toggle visibility with SetVisible().
/// </summary>
public class RangeIndicator : MonoBehaviour
{
    [SerializeField] private LineRenderer _lineRenderer;
    private const int Segments = 64;

    void Awake()
    {
        _lineRenderer.positionCount = Segments;
        _lineRenderer.loop = true;
        SetVisible(false);
    }

    public void SetRadius(float radius)
    {
        for (int i = 0; i < Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            _lineRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f));
        }
    }

    public void SetVisible(bool visible) => _lineRenderer.enabled = visible;
}
