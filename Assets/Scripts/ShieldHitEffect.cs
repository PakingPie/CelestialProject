using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    public Shader CombineShader;
    public Shader HitEffectShader;
    private RenderTexture _hitEffectRT;

    private Material _material;

    private float _rippleTime = 100.0f;
    void Start()
    {
        _material = GetComponent<MeshRenderer>().sharedMaterial;
    }

    public void GetHit(RaycastHit hit)
    {
        _material.SetVector("_Ripple_Origin", hit.transform.position);
        _rippleTime = _material.GetFloat("_Ripple_Thickness") * -2.0f;
    }
    
    private void Update()
    {
        _rippleTime += Time.deltaTime;
        _material.SetFloat("_Ripple_Time", _rippleTime);
    }

}