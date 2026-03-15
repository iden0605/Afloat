using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Manages the drag gesture for both placing new troops (from sidebar)
/// and moving existing placed troops.
/// Must be on the same GameObject as the UIDocument.
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
        // Block drag entirely if the player cannot afford this troop
        if (GoldManager.Instance != null && !GoldManager.Instance.CanAfford(data.baseCost))
            return;

        _newTroopData    = data;
        _mode            = DragMode.NewTroop;
        _activationFrame = Time.frameCount;
        SpawnGhost(data.portrait);
        ShowDragRange(data.range);
    }

    public void BeginMoveDrag(TroopInstance instance)
    {
        _movingInstance  = instance;
        _mode            = DragMode.MoveTroop;
        _activationFrame = Time.frameCount;
        TroopManager.Instance.Unregister(instance); // exclude from distance checks while moving
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

        // Map mouse position to panel coordinates (handles Scale With Screen Size)
        var root = _uiDoc.rootVisualElement;
        float px = (Input.mousePosition.x / Screen.width)                    * root.resolvedStyle.width;
        float py = ((Screen.height - Input.mousePosition.y) / Screen.height) * root.resolvedStyle.height;
        _ghost.style.left = px - 36f;
        _ghost.style.top  = py - 36f;

        // Check validity and update ghost colour
        var worldPos = ScreenToWorld(Input.mousePosition);
        bool valid   = IsPlacementValid(worldPos);
        _ghost.EnableInClassList("drag-ghost--invalid", !valid);

        // Keep the range preview centred on the cursor
        if (dragRangeIndicator != null)
            dragRangeIndicator.transform.position = worldPos;

        // NewTroop: release to place
        if (_mode == DragMode.NewTroop && Input.GetMouseButtonUp(0))
        {
            if (valid)
            {
                var data = _newTroopData;
                CancelDrag();
                TroopManager.Instance.PlaceTroop(data, worldPos);
            }
            else
            {
                CancelDrag(); // just cancel — don't place
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
                CancelDrag();
                instance.gameObject.SetActive(true);
                instance.transform.position = worldPos;
                TroopManager.Instance.Register(instance); // re-register at new position
            }
            // Invalid position: ghost stays up so the player can try again
        }
    }

    // -------------------------------------------------------
    // Placement validation
    // -------------------------------------------------------

    bool IsPlacementValid(Vector3 worldPos)
    {
        var pos2D = new Vector2(worldPos.x, worldPos.y);
        bool placingPower = GetCurrentTroopData()?.category == TroopCategory.Power;

        // Never allow placement on the enemy path
        if (Physics2D.OverlapCircle(pos2D, zoneCheckRadius, enemyPathMask)) return false;

        // Check if a land platform (e.g. Lily Pad) is present — reads PlacedPowers only,
        // so it is never blocked by the troop overlap check.
        bool onLilyPad = HasLandPlatformAt(pos2D);

        // If standing on a land platform, treat as land regardless of the water zone beneath
        bool onWater = !onLilyPad && Physics2D.OverlapCircle(pos2D, zoneCheckRadius, waterMask);

        bool terrainValid = GetCurrentPlacementType() switch
        {
            PlacementType.LandOnly     => !onWater,   // land or lily pad = valid
            PlacementType.WaterOnly    =>  onWater,   // open water only — not on a lily pad
            PlacementType.LandAndWater => true,
            _                          => true,
        };
        if (!terrainValid) return false;

        // Powers overlap-check against other powers only (no stacking lily pads).
        // Troops overlap-check against other troops only (powers are invisible to this check).
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
        // Reads PlacedPowers — lily pads are never in PlacedTroops, so this is clean.
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
            TroopManager.Instance.Register(_movingInstance); // restore if move was cancelled
            _movingInstance = null;
        }

        if (_ghost != null)
        {
            _ghost.RemoveFromHierarchy();
            _ghost = null;
        }

        if (dragRangeIndicator != null) dragRangeIndicator.SetVisible(false);
    }

    void ShowDragRange(float radius)
    {
        if (dragRangeIndicator == null) return;
        dragRangeIndicator.SetRadius(radius);
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
