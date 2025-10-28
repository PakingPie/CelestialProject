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
    private RenderTexture _hitEffectRT;
    private RenderTexture _currRT;
    private RenderTexture _prevRT;
    private RenderTexture _tempRT;

    public GameObject TempGO;

    private Material _material, _combineMaterial, _hitEffectMaterial;
    // void Start()
    // {
    //     _hitEffectRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.RHalf);
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
        _currRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.R8);
        TempGO.GetComponent<MeshRenderer>().material.SetTexture("_HitEffectRT", _currRT);

        Debug.Log(_currRT.isReadable);
        _hitEffectMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        _hitEffectMaterial.SetVector("_Center", hit.point);

        StartCoroutine(RippleEffect(hit));


        // StartCoroutine(HitEffect(hit));

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
            Graphics.Blit(_currRT, _currRT, _hitEffectMaterial);
            yield return null;
        }
        _hitEffectMaterial.SetFloat("_Radius", 0f);

        yield return null;
        Graphics.Blit(_currRT, _currRT, _hitEffectMaterial);
    }

    IEnumerator HitEffect(RaycastHit hit)
    {
        _combineMaterial.SetTexture("_ObjectRT", _hitEffectRT);
        _combineMaterial.SetTexture("_CurrentRT", _currRT);
        Graphics.Blit(null, _tempRT, _combineMaterial);

        RenderTexture temp1 = _tempRT;
        _tempRT = _currRT;
        _currRT = temp1;

        StartCoroutine(RippleEffect(hit));
        Graphics.Blit(null, _tempRT, _hitEffectMaterial);
        Graphics.Blit(_tempRT, _prevRT);

        RenderTexture temp2 = _tempRT;
        _tempRT = _currRT;
        _currRT = temp2;

        yield return null;
        StartCoroutine(HitEffect(hit));
    }
}