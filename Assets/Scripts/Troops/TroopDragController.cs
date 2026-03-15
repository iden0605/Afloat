using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the drag gesture for both placing new troops (from sidebar)
/// and moving existing placed troops.
/// Must be on the same GameObject as the UIDocument.
///
/// PathOnly troops (e.g. Anchovies):
///   • Placement is ONLY valid when the cursor is over the enemy path.
///   • The range indicator rotates each frame so its long side stays
///     perpendicular to the nearest path segment.
///   • The placed troop inherits that orientation on drop.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TroopDragController : MonoBehaviour
{
    public static TroopDragController Instance { get; private set; }

    public bool IsDragging => _mode != DragMode.None;

    [Tooltip("Radius (world units) used to check for overlapping troops — lower values allow troops to be placed closer together")]
    [SerializeField] private float overlapRadius = 0.01f;

    [Tooltip("Radius (world units) used to check which zone the cursor is in — keep small so troops can be placed at zone edges")]
    [SerializeField] private float zoneCheckRadius = 0.1f;

    [Header("Placement Zones — assign the matching layers")]
    [Tooltip("Layer used by enemy path colliders")]
    [SerializeField] private LayerMask enemyPathMask;
    [Tooltip("Layer used by the water zone collider")]
    [SerializeField] private LayerMask waterMask;

    [Tooltip("Optional: assign a scene RangeIndicator — auto-created at runtime if left empty")]
    [SerializeField] private RangeIndicator dragRangeIndicator;

    private enum DragMode { None, NewTroop, MoveTroop }

    private UIDocument    _uiDoc;
    private DragMode      _mode;
    private TroopData     _newTroopData;
    private TroopInstance _movingInstance;
    private VisualElement _ghost;
    private int           _activationFrame;

    // Rotation to apply to the placed troop (updated each frame for PathOnly troops)
    private Quaternion _pendingPlacementRotation = Quaternion.identity;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _uiDoc = GetComponent<UIDocument>();

        if (dragRangeIndicator == null)
        {
            var go = new GameObject("Drag Range Indicator");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            dragRangeIndicator = go.AddComponent<RangeIndicator>();
        }
    }

    void OnDisable() => CancelDrag();

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    public void BeginNewDrag(TroopData data)
    {
        if (GoldManager.Instance != null && !GoldManager.Instance.CanAfford(data.baseCost))
            return;

        _newTroopData              = data;
        _mode                      = DragMode.NewTroop;
        _activationFrame           = Time.frameCount;
        _pendingPlacementRotation  = Quaternion.identity;
        SpawnGhost(data.portrait);
        ShowDragRange(data.range);
    }

    public void BeginMoveDrag(TroopInstance instance)
    {
        _movingInstance            = instance;
        _mode                      = DragMode.MoveTroop;
        _activationFrame           = Time.frameCount;
        _pendingPlacementRotation  = Quaternion.identity;
        TroopManager.Instance.Unregister(instance);
        instance.gameObject.SetActive(false);
        SpawnGhost(instance.Data.portrait);
        ShowDragRange(instance.CurrentRange);
    }

    // -------------------------------------------------------
    // Update
    // -------------------------------------------------------

    void Update()
    {
        if (_mode == DragMode.None || _ghost == null) return;

        var root = _uiDoc.rootVisualElement;
        float px = (Input.mousePosition.x / Screen.width)                    * root.resolvedStyle.width;
        float py = ((Screen.height - Input.mousePosition.y) / Screen.height) * root.resolvedStyle.height;
        _ghost.style.left = px - 36f;
        _ghost.style.top  = py - 36f;

        var  worldPos = ScreenToWorld(Input.mousePosition);
        bool valid    = IsPlacementValid(worldPos);
        _ghost.EnableInClassList("drag-ghost--invalid", !valid);

        // Keep the range preview centred on the cursor
        if (dragRangeIndicator != null)
        {
            dragRangeIndicator.transform.position = worldPos;

            // PathOnly: rotate the indicator so its long side is ⊥ to the path,
            // and cache that rotation for application on drop.
            if (GetCurrentPlacementType() == PlacementType.PathOnly
                && WaypointManager.Instance != null)
            {
                Vector2 pathDir = WaypointManager.Instance.GetPathDirectionAt(worldPos);
                // -90° so the troop's local-Y points along the path direction
                float angle = Mathf.Atan2(pathDir.y, pathDir.x) * Mathf.Rad2Deg - 90f;
                _pendingPlacementRotation         = Quaternion.Euler(0f, 0f, angle);
                dragRangeIndicator.transform.rotation = _pendingPlacementRotation;
            }
            else
            {
                _pendingPlacementRotation         = Quaternion.identity;
                dragRangeIndicator.transform.rotation = Quaternion.identity;
            }
        }

        // NewTroop: release to place
        if (_mode == DragMode.NewTroop && Input.GetMouseButtonUp(0))
        {
            if (valid)
            {
                var data = _newTroopData;
                var rot  = _pendingPlacementRotation;
                CancelDrag();
                var inst = TroopManager.Instance.PlaceTroop(data, worldPos);
                if (inst != null) inst.transform.rotation = rot;
            }
            else
            {
                CancelDrag();
            }
        }
        // MoveTroop: click to place (skip activation frame)
        else if (_mode == DragMode.MoveTroop
                 && Input.GetMouseButtonDown(0)
                 && Time.frameCount > _activationFrame)
        {
            if (valid)
            {
                var instance = _movingInstance;
                var rot      = _pendingPlacementRotation;
                CancelDrag();
                instance.gameObject.SetActive(true);
                instance.transform.position = worldPos;
                instance.transform.rotation = rot;
                TroopManager.Instance.Register(instance);
            }
            // Invalid position: ghost stays up so the player can try again
        }
    }

    // -------------------------------------------------------
    // Placement validation
    // -------------------------------------------------------

    bool IsPlacementValid(Vector3 worldPos)
    {
        var  pos2D        = new Vector2(worldPos.x, worldPos.y);
        bool placingPower = GetCurrentTroopData()?.category == TroopCategory.Power;

        // ── PathOnly troops (e.g. Anchovies) ──────────────────
        // Must be ON the enemy path; terrain and platform checks are skipped.
        if (GetCurrentPlacementType() == PlacementType.PathOnly)
        {
            if (!Physics2D.OverlapCircle(pos2D, zoneCheckRadius, enemyPathMask))
                return false; // center not on path

            // The short sides are the two ends of the rectangle in the perpendicular-to-path
            // direction (±halfLong along local X). They must lie OUTSIDE the path to confirm
            // the rectangle spans the full path width with the troop centred on it.
            var data = GetCurrentTroopData();
            if (data != null && data.useRectangularRange)
            {
                float   halfLong   = data.range;
                Vector2 localRight = _pendingPlacementRotation * Vector3.right;
                if (Physics2D.OverlapCircle(pos2D + localRight *  halfLong, zoneCheckRadius, enemyPathMask))
                    return false;
                if (Physics2D.OverlapCircle(pos2D + localRight * -halfLong, zoneCheckRadius, enemyPathMask))
                    return false;
            }

            // Still prevent stacking two troops on the exact same spot
            if (overlapRadius > 0 && IsTroopOverlapping(worldPos))
                return false;

            return true;
        }

        // ── All other troops: never on the enemy path ──────────
        if (Physics2D.OverlapCircle(pos2D, zoneCheckRadius, enemyPathMask)) return false;

        bool onLilyPad = HasLandPlatformAt(pos2D);
        bool onWater   = !onLilyPad && Physics2D.OverlapCircle(pos2D, zoneCheckRadius, waterMask);

        bool terrainValid = GetCurrentPlacementType() switch
        {
            PlacementType.LandOnly     => !onWater,
            PlacementType.WaterOnly    =>  onWater,
            PlacementType.LandAndWater => true,
            _                          => true,
        };
        if (!terrainValid) return false;

        if (overlapRadius > 0)
        {
            if (placingPower  && IsPowerOverlapping(worldPos))  return false;
            if (!placingPower && IsTroopOverlapping(worldPos))  return false;
        }

        return true;
    }

    PlacementType GetCurrentPlacementType() =>
        GetCurrentTroopData()?.placementType ?? PlacementType.LandOnly;

    bool HasLandPlatformAt(Vector2 pos)
    {
        foreach (var power in TroopManager.Instance.PlacedPowers)
        {
            if (!power.Data.isLandPlatform) continue;
            var col = power.GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(pos))
                return true;
        }
        return false;
    }

    bool IsTroopOverlapping(Vector3 worldPos)
    {
        var pos2D = new Vector2(worldPos.x, worldPos.y);
        foreach (var troop in TroopManager.Instance.PlacedTroops)
        {
            var troopPos = new Vector2(troop.transform.position.x, troop.transform.position.y);
            if (Vector2.Distance(pos2D, troopPos) < overlapRadius)
                return true;
        }
        return false;
    }

    bool IsPowerOverlapping(Vector3 worldPos)
    {
        var pos2D = new Vector2(worldPos.x, worldPos.y);
        foreach (var power in TroopManager.Instance.PlacedPowers)
        {
            var powerPos = new Vector2(power.transform.position.x, power.transform.position.y);
            if (Vector2.Distance(pos2D, powerPos) < overlapRadius)
                return true;
        }
        return false;
    }

    TroopData GetCurrentTroopData()
    {
        if (_mode == DragMode.NewTroop)  return _newTroopData;
        if (_mode == DragMode.MoveTroop) return _movingInstance?.Data;
        return null;
    }

    // -------------------------------------------------------
    // Internal
    // -------------------------------------------------------

    void SpawnGhost(Sprite portrait)
    {
        _ghost = new VisualElement();
        _ghost.AddToClassList("drag-ghost");
        if (portrait != null)
            _ghost.style.backgroundImage = new StyleBackground(portrait);

        _ghost.style.left = -200;
        _ghost.style.top  = -200;
        _uiDoc.rootVisualElement.Add(_ghost);
    }

    void CancelDrag()
    {
        _mode         = DragMode.None;
        _newTroopData = null;

        if (_movingInstance != null)
        {
            _movingInstance.gameObject.SetActive(true);
            TroopManager.Instance.Register(_movingInstance);
            _movingInstance = null;
        }

        if (_ghost != null)
        {
            _ghost.RemoveFromHierarchy();
            _ghost = null;
        }

        if (dragRangeIndicator != null) dragRangeIndicator.SetVisible(false);
    }

    void ShowDragRange(float range)
    {
        if (dragRangeIndicator == null) return;

        var data = GetCurrentTroopData();
        if (data != null && data.useRectangularRange)
            dragRangeIndicator.SetRect(range, data.rangeRectWidth / 2f);
        else
            dragRangeIndicator.SetRadius(range);

        dragRangeIndicator.SetVisible(true);
    }

    static Vector3 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(Camera.main.transform.position.z);
        var   world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        world.z = 0f;
        return world;
    }
}
