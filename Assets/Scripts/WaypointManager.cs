using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaypointManager : MonoBehaviour
{
    public static WaypointManager Instance;
    public Transform[] waypoints;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    void Awake()
    {
        Instance = this;
    }
    /// <summary>
    /// Returns the normalised path direction at the point on the path closest
    /// to <paramref name="worldPos"/>. Walks every segment and picks the nearest
    /// projected point, so it works at any world position — not just waypoint nodes.
    /// Falls back to Vector2.right if fewer than two waypoints exist.
    /// </summary>
    public Vector2 GetPathDirectionAt(Vector2 worldPos)
    {
        if (waypoints == null || waypoints.Length < 2) return Vector2.right;

        float   minDist = float.MaxValue;
        Vector2 bestDir = Vector2.right;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;

            Vector2 a   = waypoints[i].position;
            Vector2 b   = waypoints[i + 1].position;
            Vector2 seg = b - a;
            float   sqLen = seg.sqrMagnitude;
            if (sqLen < 0.0001f) continue;

            // Project worldPos onto the segment [a, b]
            float   t       = Mathf.Clamp01(Vector2.Dot(worldPos - a, seg) / sqLen);
            Vector2 closest = a + seg * t;
            float   dist    = (worldPos - closest).sqrMagnitude;

            if (dist < minDist)
            {
                minDist = dist;
                bestDir = seg.normalized;
            }
        }

        return bestDir;
    }

    void OnDrawGizmos()
    {
        if (waypoints == null) return;

        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(waypoints[i].position, 0.3f);

            // Draw a line connecting each waypoint to the next
            if (i + 1 < waypoints.Length && waypoints[i + 1] != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
