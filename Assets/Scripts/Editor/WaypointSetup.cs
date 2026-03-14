using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class WaypointSetup
{
    static readonly Vector2[] waypointPositions = new Vector2[]
    {
        // Entry approach (flowing in from top-right)
        new Vector2( 5.00f, -2.50f),  // WP0
        new Vector2( 4.00f, -3.50f),  // WP1
        new Vector2( 3.20f, -4.00f),  // WP2

        // Right swirl — Circle 1 (Counter-Clockwise, center ~2.0, -5.0, radius ~1.0)
        new Vector2( 2.80f, -4.40f),  // WP3  (Top-Right)
        new Vector2( 2.00f, -4.00f),  // WP4  (Top)
        new Vector2( 1.20f, -4.40f),  // WP5  (Top-Left)
        new Vector2( 1.00f, -5.00f),  // WP6  (Left)
        new Vector2( 1.20f, -5.60f),  // WP7  (Bottom-Left)
        new Vector2( 2.00f, -6.00f),  // WP8  (Bottom)
        new Vector2( 2.80f, -5.60f),  // WP9  (Bottom-Right)
        new Vector2( 3.00f, -5.00f),  // WP10 (Right)

        // Right swirl — Circle 2 (repeat for TD loop)
        new Vector2( 2.80f, -4.40f),  // WP11 (Top-Right)
        new Vector2( 2.00f, -4.00f),  // WP12 (Top)
        new Vector2( 1.20f, -4.40f),  // WP13 (Top-Left)
        new Vector2( 1.00f, -5.00f),  // WP14 (Left)

        // Transition to left swirl (flowing left and slightly up)
        new Vector2( 0.00f, -4.50f),  // WP15
        new Vector2(-1.00f, -4.20f),  // WP16
        new Vector2(-2.00f, -4.00f),  // WP17

        // Left swirl — Circle 1 (Counter-Clockwise, center ~-3.0, -4.5, radius ~1.0)
        new Vector2(-2.20f, -3.90f),  // WP18 (Top-Right)
        new Vector2(-3.00f, -3.50f),  // WP19 (Top)
        new Vector2(-3.80f, -3.90f),  // WP20 (Top-Left)
        new Vector2(-4.00f, -4.50f),  // WP21 (Left)
        new Vector2(-3.80f, -5.10f),  // WP22 (Bottom-Left)
        new Vector2(-3.00f, -5.50f),  // WP23 (Bottom)
        new Vector2(-2.20f, -5.10f),  // WP24 (Bottom-Right)
        new Vector2(-2.00f, -4.50f),  // WP25 (Right)

        // Left swirl — Circle 2
        new Vector2(-2.20f, -3.90f),  // WP26 (Top-Right)
        new Vector2(-3.00f, -3.50f),  // WP27 (Top)
        new Vector2(-3.80f, -3.90f),  // WP28 (Top-Left)
        new Vector2(-4.00f, -4.50f),  // WP29 (Left)

        // Exit path (flowing out to bottom-left)
        new Vector2(-4.50f, -5.50f),  // WP30
        new Vector2(-5.00f, -6.50f),  // WP31
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

    [MenuItem("Tools/Fix Duplicate Waypoints")]
    static void FixDuplicateWaypoints()
    {
        WaypointManager manager = Object.FindObjectOfType<WaypointManager>();
        if (manager == null)
        {
            Debug.LogError("WaypointSetup: No WaypointManager found in scene!");
            return;
        }

        int childCount = manager.transform.childCount;
        Transform[] transforms = new Transform[childCount];

        // Rename every child by its hierarchy position, clearing any " (1)" suffixes
        for (int i = 0; i < childCount; i++)
        {
            Transform child = manager.transform.GetChild(i);
            Undo.RecordObject(child.gameObject, "Renumber Waypoint");
            child.name = "Waypoint" + i;
            transforms[i] = child;
        }

        // Rebuild array in hierarchy order
        Undo.RecordObject(manager, "Fix Duplicate Waypoints");
        manager.waypoints = transforms;
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log($"WaypointSetup: Renumbered {transforms.Length} waypoints. Save scene (Ctrl+S) to persist.");
    }
}