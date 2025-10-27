using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
public class ShieldHitEffect : MonoBehaviour
{
    public int TextureSize = 128;
    public RenderTexture ShieldHitTexture;
    public GameObject ShieldObject;
    public Shader CombineShader;
    private RenderTexture _currentTexture, _previousTexture, _tempTexture;
    private Material _shieldMaterial, _combineMaterial;

    void Start()
    {
        _currentTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _previousTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _tempTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _shieldMaterial = ShieldObject.GetComponent<MeshRenderer>().sharedMaterial;
        _combineMaterial = new Material(CombineShader);
    }
}