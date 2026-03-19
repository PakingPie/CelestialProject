using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoidFollowCamera))]
public class BoidFollowCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoidFollowCamera cam = (BoidFollowCamera)target;

        EditorGUILayout.Space();

        string label = cam.BoidManager != null && cam.BoidManager.BoidCount > 0
            ? $"Next Boid ({cam.CurrentBoidIndex + 1}/{cam.BoidManager.BoidCount})"
            : "Next Boid";

        if (GUILayout.Button(label, GUILayout.Height(30)))
        {
            cam.SwitchToNextBoid();
            EditorUtility.SetDirty(cam);
        }
    }
}
