using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class SimpleBlit : MonoBehaviour
{
    public GameObject GO;
    public Shader PlanetNoiseShader;
    public Shader PlanetDayNightShader;
    public RenderTexture MainRT;
    public int Resolution = 512;
    public Vector3 SunPosition = new Vector3(0, 1, 0);
    private Material _noiseMaterial;
    private Material _dayNightMaterial;
    private int _frameCount = 0;

    public void Init()
    {
        CleanUp();

        _noiseMaterial = new Material(PlanetNoiseShader);
        _dayNightMaterial = new Material(PlanetDayNightShader);
        
        MainRT = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBFloat);
        MainRT.wrapMode = TextureWrapMode.Repeat;
        MainRT.filterMode = FilterMode.Bilinear;
        MainRT.Create();

        _noiseMaterial.SetVector("_Resolution", new Vector4(Resolution, Resolution, 0, 0));
        _dayNightMaterial.SetTexture("_MainTex", MainRT);
        _dayNightMaterial.SetVector("_SunPosition", new Vector4(SunPosition.x, SunPosition.y, SunPosition.z, 0));

        GO.GetComponent<Renderer>().sharedMaterial = _dayNightMaterial;
        _frameCount = 0;
    }

    public void BlitTexture()
    {
        if (_noiseMaterial == null) return;

        _noiseMaterial.SetInt("_Frame", _frameCount);
        _noiseMaterial.SetTexture("_MainTex", MainRT);

        RenderTexture tempRT = RenderTexture.GetTemporary(Resolution, Resolution, 0, RenderTextureFormat.ARGBFloat);
        Graphics.Blit(MainRT, tempRT, _noiseMaterial);
        Graphics.Blit(tempRT, MainRT);
        RenderTexture.ReleaseTemporary(tempRT);

        _frameCount++;
    }

    private void CleanUp()
    {
        if (_noiseMaterial != null) DestroyImmediate(_noiseMaterial);
        if (_dayNightMaterial != null) DestroyImmediate(_dayNightMaterial);
        if (MainRT != null)
        {
            MainRT.Release();
            DestroyImmediate(MainRT);
        }
    }

    void OnDestroy()
    {
        CleanUp();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleBlit))]
public class SimpleBlitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SimpleBlit myScript = (SimpleBlit)target;
        
        if (GUILayout.Button("Init"))
        {
            myScript.Init();
        }
        
        if (GUILayout.Button("Blit Texture"))
        {
            myScript.BlitTexture();
        }
    }
}
#endif