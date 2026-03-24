using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoidFollowCamera))]
public class BoidFollowCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoidFollowCamera cam = (BoidFollowCamera)target;
        int boidCount = cam.BoidManager != null ? cam.BoidManager.BoidCount : 0;

        EditorGUILayout.Space();

        // Direct index input
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Go to Boid #", GUILayout.Width(80));
        int inputIndex = EditorGUILayout.IntField(cam.CurrentBoidIndex, GUILayout.Width(60));
        EditorGUILayout.LabelField(boidCount > 0 ? $"/ {boidCount}" : "", GUILayout.Width(40));
        if (inputIndex != cam.CurrentBoidIndex && boidCount > 0)
        {
            cam.SwitchToBoid(Mathf.Clamp(inputIndex, 0, boidCount - 1));
            EditorUtility.SetDirty(cam);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Previous / Next buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("\u25C0 Previous Boid", GUILayout.Height(30)))
        {
            cam.SwitchToPreviousBoid();
            EditorUtility.SetDirty(cam);
        }

        if (GUILayout.Button("Next Boid \u25B6", GUILayout.Height(30)))
        {
            cam.SwitchToNextBoid();
            EditorUtility.SetDirty(cam);
        }

        EditorGUILayout.EndHorizontal();
    }
}
