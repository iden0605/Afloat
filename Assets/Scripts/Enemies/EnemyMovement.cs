using UnityEngine;

/// <summary>
/// Moves this enemy along the waypoints defined in WaypointManager and rotates
/// the sprite to always face the direction of travel.
///
/// Speed is set by EnemyInstance.Initialize() from the EnemyData asset,
/// but can still be overridden in the Inspector for quick testing.
///
/// Attack scripts hold a reference to EnemyMovement; call TakeDamage() on it
/// and it will delegate to EnemyInstance.
/// </summary>
public class EnemyMovement : MonoBehaviour
{
    /// <summary>
    /// Set at runtime by EnemyInstance.Initialize() from EnemyData.speed.
    /// Not shown in the Inspector — configure speed in the EnemyData asset instead.
    /// </summary>
    [HideInInspector] public float speed = 2f;

    /// <summary>
    /// Multiplied against speed each frame.
    /// Set to 0 for stun, 0–1 for freeze/slow. Managed by EnemyStatusEffects.
    /// </summary>
    [HideInInspector] public float speedMultiplier = 1f;

    // ── Daze (reverse movement) ───────────────────────────────────────────────

    private float _dazeEndTime = 0f;

    /// <summary>True while the enemy is dazed and walking backwards along the path.</summary>
    public bool IsDazed => Time.time < _dazeEndTime;

    /// <summary>Forces the enemy to walk backwards along the path for <paramref name="duration"/> seconds.</summary>
    public void Daze(float duration)
    {
        _dazeEndTime = Mathf.Max(_dazeEndTime, Time.time + duration);
    }

    [Tooltip("Rotation offset (degrees) that corrects for the sprite's default facing direction.\n" +
             "0   = sprite already faces right (+X).\n" +
             "-90 = sprite faces up (+Y) — same convention as troops.\n" +
             "Adjust per prefab variant in the Inspector if a sprite faces a different way.")]
    [SerializeField] private float rotationOffset = -90f;

    /// <summary>Index of the waypoint this enemy is currently heading toward. Used by TroopBehavior for "First" targeting.</summary>
    public int currentWaypointIndex = 0;

    private Transform[] _waypoints;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _waypoints = WaypointManager.Instance.waypoints;
        transform.position = _waypoints[0].position;

        // Face the first waypoint immediately so the sprite doesn't pop on the first frame
        FaceToward(_waypoints[0].position);
    }

    void Update()
    {
        if (IsDazed)
        {
            TickDazed();
            return;
        }

        if (currentWaypointIndex >= _waypoints.Length) return;

        Transform target = _waypoints[currentWaypointIndex];

        FaceToward(target.position);
        transform.position = Vector3.MoveTowards(
            transform.position, target.position, speed * speedMultiplier * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= _waypoints.Length)
                ReachEndOfPath();
        }
    }

    /// <summary>Moves the enemy backward toward the previous waypoint while dazed.</summary>
    private void TickDazed()
    {
        // Already at the very first waypoint — just sit there until daze expires
        if (currentWaypointIndex <= 0) return;

        Transform prev = _waypoints[currentWaypointIndex - 1];
        FaceToward(prev.position);
        transform.position = Vector3.MoveTowards(
            transform.position, prev.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, prev.position) < 0.1f)
            currentWaypointIndex--;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Convenience method so attack scripts can deal damage without needing a direct
    /// reference to EnemyInstance. Delegates to EnemyInstance.TakeDamage().
    /// </summary>
    /// <returns>True if damage landed; false if blocked/dodged.</returns>
    public bool TakeDamage(float amount, AttackType attackType = AttackType.Generic, Vector3 attackerPos = default)
    {
        if (TryGetComponent<EnemyInstance>(out var inst))
            return inst.TakeDamage(amount, attackType, attackerPos);
        return false;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void FaceToward(Vector3 targetPos)
    {
        Vector2 dir = targetPos - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return; // already there — keep current rotation

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
    }

    void ReachEndOfPath()
    {
        // Notify WaveManager so it can track alive count and subtract player lives
        if (TryGetComponent<EnemyInstance>(out var inst))
            WaveManager.Instance?.OnEnemyEscaped(inst);
        else
            WaveManager.Instance?.OnEnemyEscaped(null);

        Destroy(gameObject);
    }
}
