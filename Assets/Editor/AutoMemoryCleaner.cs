using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class AutoMemoryCleaner
{
    static AutoMemoryCleaner()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
            Debug.Log("Memory auto-cleared before Play mode");
        }
    }
}