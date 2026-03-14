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
    }

    void OnDisable() => CancelDrag();

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    public void BeginNewDrag(TroopData data)
    {
        _newTroopData    = data;
        _mode            = DragMode.NewTroop;
        _activationFrame = Time.frameCount;
        SpawnGhost(data.portrait);
    }

    public void BeginMoveDrag(TroopInstance instance)
    {
        _movingInstance  = instance;
        _mode            = DragMode.MoveTroop;
        _activationFrame = Time.frameCount;
        TroopManager.Instance.Unregister(instance); // exclude from distance checks while moving
        instance.gameObject.SetActive(false);
        SpawnGhost(instance.Data.portrait);
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
        if (overlapRadius > 0 && IsTroopOverlapping(worldPos)) return false;

        var pos2D = new Vector2(worldPos.x, worldPos.y);

        // Never allow placement on the enemy path
        if (Physics2D.OverlapCircle(pos2D, zoneCheckRadius, enemyPathMask)) return false;

        // Check terrain against the troop's placement type
        bool onWater = Physics2D.OverlapCircle(pos2D, zoneCheckRadius, waterMask);

        return GetCurrentPlacementType() switch
        {
            PlacementType.LandOnly    => !onWater,
            PlacementType.WaterOnly   =>  onWater,
            PlacementType.LandAndWater => true,
            _                         => true,
        };
    }

    PlacementType GetCurrentPlacementType()
    {
        if (_mode == DragMode.NewTroop  && _newTroopData    != null) return _newTroopData.placementType;
        if (_mode == DragMode.MoveTroop && _movingInstance  != null) return _movingInstance.Data.placementType;
        return PlacementType.LandOnly;
    }

    bool IsTroopOverlapping(Vector3 worldPos)
    {
        // Compare center-to-center distances so overlapRadius directly controls
        // minimum spacing regardless of collider size.
        var pos2D = new Vector2(worldPos.x, worldPos.y);
        foreach (var troop in TroopManager.Instance.PlacedTroops)
        {
            // PlacedTroops excludes the troop currently being moved (it's deregistered on pickup)
            var troopPos = new Vector2(troop.transform.position.x, troop.transform.position.y);
            if (Vector2.Distance(pos2D, troopPos) < overlapRadius)
                return true;
        }
        return false;
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
    }

    static Vector3 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(Camera.main.transform.position.z);
        var   world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        world.z = 0f;
        return world;
    }
}
