using UnityEngine;

/// <summary>
/// Handles placing troops in the world.
/// Assign the Allys container Transform in the Inspector.
/// </summary>
public class TroopManager : MonoBehaviour
{
    public static TroopManager Instance { get; private set; }

    [Tooltip("The Allys parent GameObject in the scene hierarchy")]
    [SerializeField] private Transform troopsParent;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // The UIDocument overlay blocks OnMouseDown on world GameObjects, so we
        // detect troop clicks manually using the raw input system.
        if (!Input.GetMouseButtonDown(0)) return;
        if (TroopDragController.Instance.IsDragging) return;
        if (TroopSelectionUI.Instance.JustHidden) return;

        var worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(new Vector2(worldPos.x, worldPos.y), Vector2.zero);

        if (hit.collider != null && hit.collider.TryGetComponent<TroopInstance>(out var troop))
            TroopSelectionUI.Instance.Show(troop);
    }

    /// <summary>Instantiates the troop prefab at worldPos and initialises its TroopInstance.</summary>
    public TroopInstance PlaceTroop(TroopData data, Vector3 worldPos)
    {
        if (data == null || data.prefab == null)
        {
            Debug.LogWarning($"[TroopManager] TroopData '{data?.troopName}' has no prefab assigned.");
            return null;
        }

        var go = Instantiate(data.prefab, worldPos, Quaternion.identity, troopsParent);

        var instance = go.GetComponent<TroopInstance>();
        if (instance == null) instance = go.AddComponent<TroopInstance>();

        // Ensure there is a Collider2D for click detection
        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        instance.Initialize(data);
        return instance;
    }
}
