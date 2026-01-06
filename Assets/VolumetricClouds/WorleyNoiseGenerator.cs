using UnityEngine;
using UnityEngine.Rendering;

public class WorleyNoiseGenerator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ComputeShader worleyComputeShader;
    [SerializeField] private WorleyNoiseSettings settings;
    
    [Header("Output")]
    [SerializeField] private Material targetMaterial;
    [SerializeField] private string texturePropertyName = "_NoiseTexture";
    
    [Header("Debug")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool saveAsAsset = false;
    [SerializeField] private string assetPath = "Assets/GeneratedNoise/WorleyNoise3D.asset";
    
    private RenderTexture worleyTexture;
    
    private void Start()
    {
        if (generateOnStart)
        {
            GenerateWorleyNoise();
        }
    }
    
    private void OnDestroy()
    {
        ReleaseTexture();
    }
    
    [ContextMenu("Generate Worley Noise")]
    public void GenerateWorleyNoise()
    {
        if (worleyComputeShader == null)
        {
            Debug.LogError("Worley Compute Shader is not assigned!");
            return;
        }
        
        if (settings == null)
        {
            Debug.LogError("Worley Noise Settings is not assigned!");
            return;
        }
        
        CreateTexture();
        
        int kernelIndex = settings.useMultiOctave 
            ? worleyComputeShader.FindKernel("GenerateMultiOctaveWorley")
            : worleyComputeShader.FindKernel("GenerateWorleyNoise");
        
        worleyComputeShader.SetTexture(kernelIndex, "_ResultTexture", worleyTexture);
        worleyComputeShader.SetInt("_Resolution", settings.resolution);
        worleyComputeShader.SetInt("_NumCells", settings.numCells);
        worleyComputeShader.SetFloat("_Persistence", settings.persistence);
        worleyComputeShader.SetInt("_Octaves", settings.octaves);
        worleyComputeShader.SetInt("_Seed", settings.seed);
        
        int threadGroups = Mathf.CeilToInt(settings.resolution / 8.0f);
        worleyComputeShader.Dispatch(kernelIndex, threadGroups, threadGroups, threadGroups);
        
        if (targetMaterial != null)
        {
            targetMaterial.SetTexture(texturePropertyName, worleyTexture);
        }
        
        Debug.Log($"Generated Worley Noise: {settings.resolution}³, {settings.numCells} cells, {settings.octaves} octaves");
        
        #if UNITY_EDITOR
        if (saveAsAsset)
        {
            SaveAsTexture3D();
        }
        #endif
    }
    
    private void CreateTexture()
    {
        ReleaseTexture();
        
        // Use the proper constructor for 3D RenderTexture
        RenderTextureDescriptor descriptor = new RenderTextureDescriptor
        {
            width = settings.resolution,
            height = settings.resolution,
            volumeDepth = settings.resolution,
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
            enableRandomWrite = true,
            msaaSamples = 1,
            depthBufferBits = 0
        };
        
        worleyTexture = new RenderTexture(descriptor)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "WorleyNoise3D"
        };
        
        if (!worleyTexture.Create())
        {
            Debug.LogError("Failed to create 3D RenderTexture!");
            return;
        }
        
        Debug.Log($"Created 3D RenderTexture: {worleyTexture.width}x{worleyTexture.height}x{worleyTexture.volumeDepth}");
    }
    
    private void ReleaseTexture()
    {
        if (worleyTexture != null)
        {
            if (worleyTexture.IsCreated())
            {
                worleyTexture.Release();
            }
            DestroyImmediate(worleyTexture);
            worleyTexture = null;
        }
    }
    
    public RenderTexture GetTexture()
    {
        return worleyTexture;
    }
    
    #if UNITY_EDITOR
    private void SaveAsTexture3D()
    {
        if (worleyTexture == null || !worleyTexture.IsCreated())
        {
            Debug.LogError("No texture to save!");
            return;
        }
        
        StartCoroutine(ReadbackAndSave());
    }
    
    private System.Collections.IEnumerator ReadbackAndSave()
    {
        var request = AsyncGPUReadback.Request(worleyTexture);
        
        while (!request.done)
        {
            yield return null;
        }
        
        if (request.hasError)
        {
            Debug.LogError("GPU readback failed!");
            yield break;
        }
        
        Texture3D texture3D = new Texture3D(
            settings.resolution, 
            settings.resolution, 
            settings.resolution, 
            TextureFormat.RGBA32, 
            false
        )
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            name = "WorleyNoise3D"
        };
        
        var data = request.GetData<Color32>();
        Color[] colors = new Color[data.Length];
        
        for (int i = 0; i < data.Length; i++)
        {
            colors[i] = data[i];
        }
        
        texture3D.SetPixels(colors);
        texture3D.Apply();
        
        string directory = System.IO.Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        UnityEditor.AssetDatabase.CreateAsset(texture3D, assetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        
        Debug.Log($"Saved Worley Noise to: {assetPath}");
    }
    #endif
}