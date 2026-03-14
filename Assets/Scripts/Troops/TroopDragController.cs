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

    private enum DragMode { None, NewTroop, MoveTroop }

    private UIDocument    _uiDoc;
    private DragMode      _mode;
    private TroopData     _newTroopData;
    private TroopInstance _movingInstance;
    private VisualElement _ghost;
    private int           _activationFrame; // prevents same-frame placement on move-click

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

    /// <summary>Start dragging a brand-new troop from the sidebar (hold to drag).</summary>
    public void BeginNewDrag(TroopData data)
    {
        _newTroopData    = data;
        _mode            = DragMode.NewTroop;
        _activationFrame = Time.frameCount;
        SpawnGhost(data.portrait);
    }

    /// <summary>Start moving a placed troop (click-to-place, no hold required).</summary>
    public void BeginMoveDrag(TroopInstance instance)
    {
        _movingInstance  = instance;
        _mode            = DragMode.MoveTroop;
        _activationFrame = Time.frameCount;
        instance.gameObject.SetActive(false);
        SpawnGhost(instance.Data.portrait);
    }

    // -------------------------------------------------------
    // Update — ghost follows cursor every frame (no UI event lag)
    // -------------------------------------------------------

    void Update()
    {
        if (_mode == DragMode.None || _ghost == null) return;

        // Panel space: top-left origin. Input.mousePosition: bottom-left origin.
        _ghost.style.left = Input.mousePosition.x - 36f;
        _ghost.style.top  = Screen.height - Input.mousePosition.y - 36f;

        // NewTroop: release mouse button to place
        if (_mode == DragMode.NewTroop && Input.GetMouseButtonUp(0))
        {
            var data = _newTroopData;
            CancelDrag();
            TroopManager.Instance.PlaceTroop(data, ScreenToWorld(Input.mousePosition));
        }
        // MoveTroop: single click to place — skip the frame the mode was activated
        else if (_mode == DragMode.MoveTroop
                 && Input.GetMouseButtonDown(0)
                 && Time.frameCount > _activationFrame)
        {
            var instance = _movingInstance;
            CancelDrag();
            instance.gameObject.SetActive(true);
            instance.transform.position = ScreenToWorld(Input.mousePosition);
        }
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
            _movingInstance = null;
        }

        if (_ghost != null)
        {
            _ghost.RemoveFromHierarchy();
            _ghost = null;
        }
    }

    // Input.mousePosition uses bottom-left origin, same as ScreenToWorldPoint — no Y flip needed.
    static Vector3 ScreenToWorld(Vector2 screenPos)
    {
        float depth = Mathf.Abs(Camera.main.transform.position.z);
        var   world = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, depth));
        world.z = 0f;
        return world;
    }
}
