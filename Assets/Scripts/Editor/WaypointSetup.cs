using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class WaypointSetup
{
    static readonly Vector2[] waypointPositions = new Vector2[]
    {
        // Entry approach
        new Vector2( 5.00f, -2.50f),  // WP0
        new Vector2( 4.20f, -3.00f),  // WP1
        new Vector2( 3.20f, -3.50f),  // WP2
        new Vector2( 2.50f, -3.70f),  // WP3

        // Right swirl — Circle 1 (clockwise, 30° steps, radius 1.3, center 1.5,-4.9)
        new Vector2( 2.15f, -3.77f),  // WP4
        new Vector2( 2.63f, -4.25f),  // WP5
        new Vector2( 2.80f, -4.90f),  // WP6
        new Vector2( 2.63f, -5.55f),  // WP7
        new Vector2( 2.15f, -6.03f),  // WP8
        new Vector2( 1.50f, -6.20f),  // WP9
        new Vector2( 0.85f, -6.03f),  // WP10
        new Vector2( 0.37f, -5.55f),  // WP11
        new Vector2( 0.20f, -4.90f),  // WP12
        new Vector2( 0.37f, -4.25f),  // WP13
        new Vector2( 0.85f, -3.77f),  // WP14
        new Vector2( 1.50f, -3.60f),  // WP15

        // Right swirl — Circle 2 (repeat)
        new Vector2( 2.15f, -3.77f),  // WP16
        new Vector2( 2.63f, -4.25f),  // WP17
        new Vector2( 2.80f, -4.90f),  // WP18
        new Vector2( 2.63f, -5.55f),  // WP19
        new Vector2( 2.15f, -6.03f),  // WP20
        new Vector2( 1.50f, -6.20f),  // WP21
        new Vector2( 0.85f, -6.03f),  // WP22
        new Vector2( 0.37f, -5.55f),  // WP23
        new Vector2( 0.20f, -4.90f),  // WP24
        new Vector2( 0.37f, -4.25f),  // WP25
        new Vector2( 0.85f, -3.77f),  // WP26
        new Vector2( 1.50f, -3.60f),  // WP27

        // Transition to left swirl
        new Vector2( 0.50f, -3.80f),  // WP28
        new Vector2(-0.80f, -4.00f),  // WP29
        new Vector2(-1.80f, -4.10f),  // WP30

        // Left swirl — Circle 1 (clockwise, 30° steps, radius 1.2, center -3.1,-5.2)
        new Vector2(-2.50f, -4.16f),  // WP31
        new Vector2(-2.06f, -4.60f),  // WP32
        new Vector2(-1.90f, -5.20f),  // WP33
        new Vector2(-2.06f, -5.80f),  // WP34
        new Vector2(-2.50f, -6.24f),  // WP35
        new Vector2(-3.10f, -6.40f),  // WP36
        new Vector2(-3.70f, -6.24f),  // WP37
        new Vector2(-4.14f, -5.80f),  // WP38
        new Vector2(-4.30f, -5.20f),  // WP39
        new Vector2(-4.14f, -4.60f),  // WP40
        new Vector2(-3.70f, -4.16f),  // WP41
        new Vector2(-3.10f, -4.00f),  // WP42

        // Exit path
        new Vector2(-4.20f, -4.50f),  // WP43
        new Vector2(-5.20f, -4.90f),  // WP44
        new Vector2(-5.90f, -5.10f),  // WP45
    };

    [MenuItem("Tools/Setup Waypoints")]
    static void SetupWaypoints()
    {
        WaypointManager manager = Object.FindObjectOfType<WaypointManager>();
        if (manager == null)
        {
            Debug.LogError("WaypointSetup: No WaypointManager found in scene!");
            return;
        }

        // Move WaypointManager to origin so children use world-space positions directly
        manager.transform.position = Vector3.zero;

        // Delete all existing child waypoints
        for (int i = manager.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(manager.transform.GetChild(i).gameObject);

        // Create new waypoints
        Transform[] transforms = new Transform[waypointPositions.Length];
        for (int i = 0; i < waypointPositions.Length; i++)
        {
            GameObject wp = new GameObject("Waypoint" + i);
            Undo.RegisterCreatedObjectUndo(wp, "Create Waypoint");
            wp.transform.SetParent(manager.transform);
            wp.transform.position = new Vector3(waypointPositions[i].x, waypointPositions[i].y, 0f);
            transforms[i] = wp.transform;
        }

        // Assign to WaypointManager array
        Undo.RecordObject(manager, "Assign Waypoints");
        manager.waypoints = transforms;
        EditorUtility.SetDirty(manager);

        // Save the scene
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"WaypointSetup: Created {transforms.Length} waypoints successfully. Save the scene (Ctrl+S) to persist.");
    }
}
