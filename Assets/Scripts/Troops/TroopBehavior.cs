using UnityEngine;

/// <summary>
/// Runs the per-troop game loop: detects enemies in range each frame,
/// selects the "First" target (furthest along the path), and rotates
/// the troop sprite to face it. Future TroopAttack can read CurrentTarget
/// and EnemiesInRange directly from this component.
/// </summary>
[RequireComponent(typeof(TroopInstance))]
public class TroopBehavior : MonoBehaviour
{
    [Tooltip("Layer mask for enemy GameObjects")]
    [SerializeField] private LayerMask enemyLayer;

    private TroopInstance _instance;

    /// <summary>The enemy currently being tracked ("First" targeting).</summary>
    public EnemyMovement CurrentTarget  { get; private set; }

    /// <summary>Number of enemies detected inside range this frame.</summary>
    public int EnemiesInRange           { get; private set; }

    void Awake()
    {
        _instance = GetComponent<TroopInstance>();
    }

    void Update()
    {
        if (_instance.Data == null) return;

        // ── Detect all enemies within range ───────────────
        var hits = Physics2D.OverlapCircleAll(
            transform.position, _instance.CurrentRange, enemyLayer);

        EnemiesInRange = hits.Length;

        // ── "First" targeting: pick enemy furthest along path ──
        EnemyMovement best = null;
        int bestIdx = -1;
        foreach (var col in hits)
        {
            if (col.TryGetComponent<EnemyMovement>(out var em)
                && em.currentWaypointIndex > bestIdx)
            {
                bestIdx = em.currentWaypointIndex;
                best    = em;
            }
        }
        CurrentTarget = best;

        // ── Rotate toward current target ──────────────────
        if (CurrentTarget != null)
        {
            Vector2 dir = CurrentTarget.transform.position - transform.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            // -90° offset: sprite "forward" is assumed to face up (positive Y).
            // Adjust per-troop in the Inspector if a sprite faces a different direction.
            transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }
}
