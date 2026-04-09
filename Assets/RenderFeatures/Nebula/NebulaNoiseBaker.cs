// NebulaNoiseBaker.cs
// Attach to any GameObject. In the Inspector, assign the compute shader,
// choose a resolution (128 is a good default), then click "Bake Noise Texture".
// The resulting Texture3D asset is assigned to NebulaVolume.noiseTexture.

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class NebulaNoiseBaker : MonoBehaviour
{
    [Header("Compute Shader")]
    [Tooltip("Assign NebulaNoiseBake.compute here.")]
    public ComputeShader noiseCompute;

    [Header("Bake Settings")]
    [Tooltip("Per-axis resolution of the 3D texture. 128 is a good balance of quality and size.")]
    [Range(32, 256)]
    public int resolution = 128;

    [Tooltip("Half-extent of the bake domain. The texture stores fbm(q) for q in [-domainHalf, +domainHalf]. "
           + "2.5 covers the default density input range (0.25 * p with p in [-10,10]).")]
    public float domainHalf = 2.5f;

    [Header("Output (auto-assigned after bake)")]
    public Texture3D bakedNoise;

    /// <summary>
    /// Dispatches the compute shader, reads back results, and creates a Texture3D.
    /// </summary>
    public Texture3D Bake()
    {
        if (noiseCompute == null)
        {
            Debug.LogError("NebulaNoiseBaker: Compute shader is not assigned.");
            return null;
        }

        int kernel = noiseCompute.FindKernel("BakeNebulaNoise");
        int count  = resolution * resolution * resolution;

        // Allocate GPU buffer
        ComputeBuffer buffer = new ComputeBuffer(count, sizeof(float));
        noiseCompute.SetBuffer(kernel, "_ResultBuffer", buffer);
        noiseCompute.SetInt("_Resolution", resolution);
        noiseCompute.SetFloat("_DomainHalf", domainHalf);

        int groups = Mathf.CeilToInt(resolution / 8f);
        noiseCompute.Dispatch(kernel, groups, groups, groups);

        // Read back to CPU
        float[] data = new float[count];
        buffer.GetData(data);
        buffer.Release();

        // Build Texture3D (single-channel RFloat)
        Texture3D tex = new Texture3D(resolution, resolution, resolution,
                                      TextureFormat.RFloat, false);
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixelData(data, 0);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        bakedNoise = tex;
        Debug.Log($"NebulaNoiseBaker: Baked {resolution}³ noise texture ({count} voxels).");
        return tex;
    }
}

// ─────────────────────────── Editor ───────────────────────────

#if UNITY_EDITOR
[CustomEditor(typeof(NebulaNoiseBaker))]
public class NebulaNoiseBakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        NebulaNoiseBaker baker = (NebulaNoiseBaker)target;

        if (GUILayout.Button("Bake Noise Texture", GUILayout.Height(32)))
        {
            Texture3D tex = baker.Bake();
            if (tex == null) return;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save 3D Noise Texture",
                "NebulaNoiseVolume",
                "asset",
                "Choose where to save the baked noise texture.");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(tex, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                baker.bakedNoise = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
                EditorUtility.SetDirty(baker);
                Debug.Log($"NebulaNoiseBaker: Saved to {path}");
            }
        }

        if (baker.bakedNoise != null)
        {
            EditorGUILayout.HelpBox(
                $"Current texture: {baker.bakedNoise.width}³  {baker.bakedNoise.format}",
                MessageType.Info);
        }
    }
}
#endif