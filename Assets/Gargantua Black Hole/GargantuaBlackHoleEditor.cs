using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GargantuaBlackHole))]
public class GargantuaBlackHoleEditor : Editor
{
    GargantuaBlackHole blackHole;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        blackHole = (GargantuaBlackHole)target;
        if (GUILayout.Button("Render"))
        {
            blackHole.Render();
        }
    }

    public void OnEnable()
    {
        blackHole = (GargantuaBlackHole)target;
    }
}