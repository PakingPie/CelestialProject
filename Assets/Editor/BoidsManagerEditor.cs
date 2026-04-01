using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoidsManager))]
public class BoidsManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoidsManager manager = (BoidsManager)target;

        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Morale:", GUILayout.Width(50));
        string moraleInfo = $"{manager.CurrentMorale}  (Score: {manager.CurrentMoraleScore:F2})";
        if (manager.DebugMoraleLocked)
            moraleInfo += "  [LOCKED]";
        EditorGUILayout.LabelField(moraleInfo);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Boids: {manager.BoidCount}");

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("Force Escape", GUILayout.Height(30)))
        {
            manager.DebugForceEscape();
        }

        GUI.backgroundColor = new Color(0.3f, 1f, 0.3f);
        if (GUILayout.Button("Restore Confident", GUILayout.Height(30)))
        {
            manager.DebugRestoreConfident();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
