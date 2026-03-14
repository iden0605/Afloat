using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    public float speed = 3f;
    private int currentWaypointIndex = 0;
    private Transform[] waypoints;
    // Start is called before the first frame update
    void Start()
    {
        waypoints = WaypointManager.Instance.waypoints;
        transform.position = waypoints[0].position;
    }

    // Update is called once per frame
    void Update()
    {
        if (currentWaypointIndex >= waypoints.Length) return;

        Transform target = waypoints[currentWaypointIndex];
        float step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target.position, step);

        // Check if close enough to move to next waypoint
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Length)
            {
                ReachEndOfPath();
            }
        }
    }
    void ReachEndOfPath()
    {
        // TODO: subtract player health here
        Destroy(gameObject);
    }
}
