using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    public float RippleEffectTime = 5.0f;
    public float RippleRadius = 0.1f;
    [SerializeField] private int _textureSize = 128;
    public Shader CombineShader;
    public Shader HitEffectShader;
    private RenderTexture _objectRT;
    private RenderTexture _currRT;
    private RenderTexture _prevRT;
    private RenderTexture _tempRT;

    public GameObject CopyGO;
    public GameObject CombineGO;

    private Material _material, _combineMaterial;
    private Material _hitEffectMaterial, _hitEffectForCombineMaterial;

    // void Start()
    // {
    //     _objectRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf);
    //     _currRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf);
    //     _prevRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf);
    //     _tempRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf);

    //     _combineMaterial = new Material(CombineShader);
    //     _hitEffectMaterial = new Material(HitEffectShader);

    //     _material = GetComponent<MeshRenderer>().sharedMaterial;
    //     _material.SetTexture("_HitEffectRT", _currRT);
    // }

    public void GetHit(RaycastHit hit)
    {
        _currRT.Release();
        _objectRT.Release();
        _currRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        _objectRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);

        CombineGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_ObjectRT", _objectRT);
        CombineGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_CurrentRT", _currRT);

        CopyGO.GetComponent<MeshRenderer>().sharedMaterial.SetVector("_Center", hit.point);
        GetComponent<MeshRenderer>().sharedMaterial.SetVector("_Center", hit.point);

        _combineMaterial = new Material(CombineShader);

        // Initial _currRT with _hitEffectMaterial
        Graphics.Blit(null, _currRT, CopyGO.GetComponent<MeshRenderer>().sharedMaterial);

        // Blit _currRT to _objectRT using _combineMaterial
        Graphics.Blit(_currRT, _objectRT, _combineMaterial, pass: 0);
        // Graphics.Blit(null, _objectRT, _combineMaterial, pass: 1);
        // Graphics.Blit(_currRT, _objectRT);

        // StartCoroutine(RippleEffect(hit));

        // -------------------------------------
        // if (SourceRT != null)
        // {
        //     SourceRT.Release();
        // }
        // SourceRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        // SourceRT.Create();
        // SourceRT.filterMode = FilterMode.Bilinear;
        // SourceRT.wrapMode = TextureWrapMode.Clamp;
        // if (DestRT != null)
        // {
        //     DestRT.Release();
        // }
        // DestRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        // DestRT.Create();
        // DestRT.filterMode = FilterMode.Bilinear;
        // DestRT.wrapMode = TextureWrapMode.Clamp;

        // CombineGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitEffectRT", DestRT);
        // _hitEffectMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        // Graphics.Blit(null, SourceRT, _hitEffectMaterial);
        // Graphics.Blit(SourceRT, DestRT);

    }

    IEnumerator RippleEffect(RaycastHit hit)
    {
        float elapsed = 0f;
        while (elapsed < RippleEffectTime)
        {
            elapsed += Time.deltaTime;
            float rippleStrength = Mathf.Lerp(0.0f, RippleRadius, elapsed / RippleEffectTime);
            _hitEffectMaterial.SetFloat("_Radius", rippleStrength);
            _hitEffectMaterial.SetFloat("_Hardness", 1.0f - (elapsed / RippleEffectTime));
            _hitEffectForCombineMaterial.SetFloat("_Radius", rippleStrength);
            _hitEffectForCombineMaterial.SetFloat("_Hardness", 1.0f - (elapsed / RippleEffectTime));
            yield return null;
            Graphics.Blit(null, _currRT, _hitEffectForCombineMaterial);
        }
        _hitEffectMaterial.SetFloat("_Radius", 0f);
        _hitEffectForCombineMaterial.SetFloat("_Radius", 0f);

        yield return null;
        Graphics.Blit(null, _currRT, _hitEffectForCombineMaterial);
    }
    // This coroutine handles the multi-hit effect rendering    
    IEnumerator HitEffect(RaycastHit hit)
    {
        yield return StartCoroutine(RippleEffect(hit));
    }
}