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
