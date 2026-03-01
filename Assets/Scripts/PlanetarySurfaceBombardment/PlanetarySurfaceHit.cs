using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlanetarySurfaceHit : MonoBehaviour
{
    public GameObject PlanetGO;
    [Header("Settings")]
    public int TextureSize = 64;
    [Range(0.001f, 1.0f)]
    public float HitImpactScale = 0.1f;

    [Header("Shaders")]
    public Shader HitEffectShader;
    public Shader CumulativeShader;

    [Header("Debug")]
    public bool EnableDebugMode = false;
    public GameObject DebugQuad;
    public Transform ShootFrom;

    private RenderTexture _cumulativeRT;
    private RenderTexture _singleEffectRT;
    private RenderTexture _tempRT;

    private MaterialPropertyBlock _planetPropBlock;
    private MeshRenderer _planetRenderer;

    private Material _hitEffectMat;
    private Material _cumulativeMat;

    private bool _isInitialized = false;

    private Vector2 _hitUV;

    private RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    private void ReleaseRT(RenderTexture rt)
    {
        if (rt != null)
        {
            if (RenderTexture.active == rt)
                RenderTexture.active = null;
            rt.Release();
            rt = null;
        }
    }

    private void Init()
    {
        if (_isInitialized)
            return;

        if (_cumulativeRT == null) _cumulativeRT = CreateRT();
        if (_singleEffectRT == null) _singleEffectRT = CreateRT();
        if (_tempRT == null) _tempRT = CreateRT();

        if (_cumulativeMat == null) _cumulativeMat = new Material(CumulativeShader);

        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);
        }

        if (_planetPropBlock == null) _planetPropBlock = new MaterialPropertyBlock();
        if (PlanetGO != null) _planetRenderer = PlanetGO.GetComponent<MeshRenderer>();

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);

        _isInitialized = true;
    }

    public void GetHit(RaycastHit hit)
    {
        _hitUV = hit.textureCoord;
        if (!_isInitialized)
        {
            Init();
        }

        if(DebugQuad != null && EnableDebugMode)
        {
            DebugQuad.GetComponent<MeshRenderer>().sharedMaterial = _hitEffectMat;
        }

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);

        _hitEffectMat.SetVector("_HitUV", _hitUV);
        _hitEffectMat.SetFloat("_Scale", HitImpactScale);
        Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);

        Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
        Graphics.Blit(_tempRT, _cumulativeRT);

        if (_planetRenderer != null)
        {
            _planetRenderer.sharedMaterial.SetTexture("_HitAreaTex", _cumulativeRT);
        }
    }

    public void ClearAll()
    {
        ReleaseRT(_cumulativeRT);
        ReleaseRT(_singleEffectRT);
        ReleaseRT(_tempRT);

        if (_hitEffectMat != null) DestroyImmediate(_hitEffectMat);
        if (_cumulativeMat != null) DestroyImmediate(_cumulativeMat);

        _isInitialized = false;
    }

    void OnDestroy()
    {
        ClearAll();
    }

    void OnDisable()
    {
        ClearAll();
    }

    void OnDrawGizmos()
    {
        if (ShootFrom != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(ShootFrom.position, ShootFrom.forward);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PlanetarySurfaceHit))]
public class PlanetarySurfaceHitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlanetarySurfaceHit planetarySurfaceHit = (PlanetarySurfaceHit)target;
        if (GUILayout.Button("Shoot Test Hit"))
        {
            RaycastHit hit;
            Physics.Raycast(planetarySurfaceHit.ShootFrom.position, planetarySurfaceHit.ShootFrom.forward, out hit);
            if (hit.collider != null)
            {
                planetarySurfaceHit.GetHit(hit);
            }
        }

        if (GUILayout.Button("Clear All"))
        {
            planetarySurfaceHit.ClearAll();
        }
    }
}
#endif