using System.Collections.Generic;
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

    // Tracked so TroopDragController can do center-to-center distance checks
    public IReadOnlyList<TroopInstance> PlacedTroops => _placedTroops;
    private readonly List<TroopInstance> _placedTroops = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (TroopDragController.Instance.IsDragging) return;

        var worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var hits = Physics2D.RaycastAll(new Vector2(worldPos.x, worldPos.y), Vector2.zero);

        foreach (var hit in hits)
        {
            if (hit.collider.TryGetComponent<TroopInstance>(out var troop))
            {
                TroopSelectionUI.Instance.Show(troop);
                break;
            }
        }
    }

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

        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        instance.Initialize(data);
        Register(instance);
        return instance;
    }

    public void Register(TroopInstance troop)
    {
        if (!_placedTroops.Contains(troop))
            _placedTroops.Add(troop);
    }

    public void Unregister(TroopInstance troop)
    {
        _placedTroops.Remove(troop);
    }
}
