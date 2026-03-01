// Editor/WorleyNoiseGeneratorEditor.cs
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorleyNoiseGenerator))]
public class WorleyNoiseGeneratorEditor : Editor
{
    private Texture2D previewTexture;
    private int previewSlice = 0;
    private int previewChannel = 0;
    private readonly string[] channelNames = { "R (Base Shape)", "G (Detail 1)", "B (Detail 2)", "A (Cellular)" };
    
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        EditorGUILayout.Space(10);
        
        WorleyNoiseGenerator generator = (WorleyNoiseGenerator)target;
        
        if (GUILayout.Button("Generate Worley Noise", GUILayout.Height(30)))
        {
            generator.GenerateWorleyNoise();
            UpdatePreview(generator);
        }
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        
        RenderTexture rt = generator.GetTexture();
        if (rt != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Slice:", GUILayout.Width(40));
            previewSlice = EditorGUILayout.IntSlider(previewSlice, 0, rt.volumeDepth - 1);
            EditorGUILayout.EndHorizontal();
            
            previewChannel = GUILayout.SelectionGrid(previewChannel, channelNames, 2);
            
            if (GUILayout.Button("Update Preview"))
            {
                UpdatePreview(generator);
            }
            
            if (previewTexture != null)
            {
                EditorGUILayout.Space(5);
                Rect previewRect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(previewRect, previewTexture);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Generate noise to see preview", MessageType.Info);
        }
    }
    
    private void UpdatePreview(WorleyNoiseGenerator generator)
    {
        RenderTexture rt = generator.GetTexture();
        if (rt == null) return;
        
        // Create preview material
        Shader previewShader = Shader.Find("Hidden/WorleyNoisePreview");
        if (previewShader == null)
        {
            // Fallback: just show we can't preview
            Debug.LogWarning("Preview shader not found. Create Hidden/WorleyNoisePreview shader for slice preview.");
            return;
        }
        
        // For now, we'll skip the slice preview as it requires additional shader setup
        // The texture is being generated and applied to the material
    }
    
    private void OnDisable()
    {
        if (previewTexture != null)
        {
            DestroyImmediate(previewTexture);
        }
    }
}
#endif