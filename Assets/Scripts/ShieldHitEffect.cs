using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    public int TextureSize = 128;
    private RenderTexture _shieldHitTexture;
    public GameObject ShieldObject;
    public Shader HitEffectShader;
    public Shader CombineShader;

    private RenderTexture _currentTexture, _previousTexture, _tempTexture;
    private Material _hitEffectMaterial, _combineMaterial;

    void Start()
    {
        _currentTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _previousTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _tempTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RFloat);
        _hitEffectMaterial = new Material(HitEffectShader);
        _combineMaterial = new Material(CombineShader);
        _shieldHitTexture = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGB32);

        ShieldObject.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_ShieldHitTex", _currentTexture);

        StartCoroutine(ShieldHit());
    }

    IEnumerator ShieldHit()
    {
        _combineMaterial.SetTexture("_ObjectRT", _shieldHitTexture);
        _combineMaterial.SetTexture("_CurrentTex", _currentTexture);
        Graphics.Blit(null, _tempTexture, _combineMaterial);

        RenderTexture rt0 = _tempTexture;
        _tempTexture = _currentTexture;
        _currentTexture = rt0;

        _hitEffectMaterial.SetTexture("_PreviousTex", _previousTexture);
        _hitEffectMaterial.SetTexture("_CurrentTex", _currentTexture);
        Graphics.Blit(null, _tempTexture, _hitEffectMaterial);
        Graphics.Blit(_tempTexture, _previousTexture);

        RenderTexture rt1 = _previousTexture;
        _previousTexture = _currentTexture;
        _currentTexture = rt1;

        yield return null;
        StartCoroutine(ShieldHit());
    }
}