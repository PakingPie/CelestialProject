using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    [SerializeField] private int _textureSize = 128;
    public Shader CombineShader;
    public Shader HitEffectShader;
    private RenderTexture _hitEffectRT;

    private Material _material, combineMaterial, hitEffectMaterial;
    void Start()
    {
        _material = GetComponent<MeshRenderer>().sharedMaterial;
        _hitEffectRT = new RenderTexture(_textureSize, _textureSize, 0, RenderTextureFormat.ARGB32);
        combineMaterial = new Material(CombineShader);
        hitEffectMaterial = new Material(HitEffectShader);
    }

    public void GetHit(RaycastHit hit)
    {
    }
    
    private void Update()
    {
    }

}