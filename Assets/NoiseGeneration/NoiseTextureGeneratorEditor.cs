using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(NoiseTextureGenerator))]
public class NoiseTextureGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        NoiseTextureGenerator generator = (NoiseTextureGenerator)target;

        if (GUILayout.Button("Save 2D Texture as Asset"))
        {
            if (generator.textureSize > 0)
            {
                generator.Generate2DNoiseTexture();
                generator.SaveTextureAsAsset(generator.NoiseTexture2D, "Noise2D_");
            }
            else
            {
                Debug.LogError("Texture size must be greater than 0.");
            }
        }

        if (GUILayout.Button("Save 3D Texture as Asset"))
        {
            if (generator.textureSize > 0)
            {
                generator.Generate3DNoiseTexture();
                generator.SaveTextureAsAsset(generator.NoiseTexture3D, "Noise3D_");
            }
            else
            {
                Debug.LogError("Texture size must be greater than 0.");
            }
        }
    }
}