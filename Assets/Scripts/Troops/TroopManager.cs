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

    // Combat troops — used for center-to-center overlap checks
    public IReadOnlyList<TroopInstance> PlacedTroops  => _placedTroops;
    private readonly List<TroopInstance> _placedTroops = new();

    // Powers (e.g. Lily Pad) — separate registry so troops don't collide-check against them
    public IReadOnlyList<TroopInstance> PlacedPowers  => _placedPowers;
    private readonly List<TroopInstance> _placedPowers = new();

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
                EnemySelectionUI.Instance?.Hide();
                TroopSelectionUI.Instance.Show(troop);
                return;
            }

            if (hit.collider.TryGetComponent<TroopHomeProxy>(out var proxy) && proxy.Troop != null)
            {
                EnemySelectionUI.Instance?.Hide();
                TroopSelectionUI.Instance.Show(proxy.Troop);
                return;
            }

            if (hit.collider.TryGetComponent<EnemyInstance>(out var enemy))
            {
                TroopSelectionUI.Instance?.Hide();
                EnemySelectionUI.Instance?.Show(enemy);
                return;
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

        // Safety-net gold check (primary check is in TroopDragController.BeginNewDrag)
        if (GoldManager.Instance != null && !GoldManager.Instance.SpendGold(data.baseCost))
        {
            Debug.LogWarning($"[TroopManager] Cannot afford {data.troopName} ({data.baseCost}g).");
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
        if (troop.Data.category == TroopCategory.Power)
        {
            if (!_placedPowers.Contains(troop)) _placedPowers.Add(troop);
        }
        else
        {
            if (!_placedTroops.Contains(troop)) _placedTroops.Add(troop);
        }
    }

    public void Unregister(TroopInstance troop)
    {
        _placedTroops.Remove(troop);
        _placedPowers.Remove(troop);
    }
}
