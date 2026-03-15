using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TroopData))]
public class TroopDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Identity ──────────────────────────────────────────
        EditorGUILayout.PropertyField(serializedObject.FindProperty("troopName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portrait"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefab"));

        EditorGUILayout.Space();

        // ── Category ─────────────────────────────────────────
        var categoryProp = serializedObject.FindProperty("category");
        EditorGUILayout.PropertyField(categoryProp);
        bool isPower = categoryProp.enumValueIndex == (int)TroopCategory.Power;

        EditorGUILayout.Space();

        // ── Placement ────────────────────────────────────────
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("placementType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isLandPlatform"));

        EditorGUILayout.Space();

        // ── Economy ──────────────────────────────────────────
        EditorGUILayout.LabelField("Economy", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseCost"));

        // ── Combat Stats + Upgrades — hidden for Powers ───────
        if (!isPower)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Combat Stats", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("attack"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("attackSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("range"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("projectileType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("splashRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("baseEffect"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upgrades  (attackDelta / attackSpeedDelta / rangeDelta add to current stats)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("upgrades"), true);

            // ── Evolutions ──────────────────────────────────────────
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Evolutions  (one entry per evolved form, in order)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("evolutions"), true);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
