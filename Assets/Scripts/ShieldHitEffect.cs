using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEngine.UI;

// [ExecuteInEditMode]
#if Test1
public class ShieldHitEffect : MonoBehaviour
{
    public float HitImpactDuration = 0.1f;
    public float HitImpactScale = 0.1f;
    public GameObject ShieldGO;

    public void GetHit(RaycastHit hit)
    {
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetVector("_Center", hit.point);
        StartCoroutine(RippleEffect(hit));
    }

    IEnumerator RippleEffect(RaycastHit hit)
    {
        float elapsed = 0f;
        while (elapsed < HitImpactDuration)
        {
            elapsed += Time.deltaTime;
            float rippleStrength = Mathf.Lerp(HitImpactScale, 0.0f, elapsed / HitImpactDuration);
            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Radius", 1 - rippleStrength);
            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Hardness", Mathf.Clamp01((elapsed / HitImpactDuration)));
            yield return null;
        }
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Radius", 0.0f);
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Hardness", 1.0f);
    }

}
#else
public class ShieldHitEffect : MonoBehaviour
{
    public GameObject ShieldGO;
    [Header("Settings")]
    public int TextureSize = 64;
    public float HitImpactScale = 0.1f;
    public float HitImpactDuration = 1.0f;

    [Header("Decay Settings")]
    [Range(0.1f, 0.99f)]
    public float DecayPerSecond = 0.75f;
    [Range(0.0f, 0.1f)]
    public float MinDecayThreshold = 0.02f;
    public float ForceClearInterval = 3.0f;

    [Header("Shaders")]
    public Shader HitEffectShader;
    public Shader CumulativeShader;

    [Header("Debug")]
    public bool EnableDebugMode = false;
    public GameObject DebugQuad;

    private RenderTexture _cumulativeRT;
    private RenderTexture _outputRT;
    private RenderTexture _singleEffectRT;
    private RenderTexture _tempRT;

    private Texture2D _blackTex;

    private Material _hitEffectMat;
    private Material _cumulativeMat;

    private List<ActiveRipple> _activeRipples = new List<ActiveRipple>();

    private bool _isInitialized = false;

    private float _forceClearTimer = 0f;

    private class ActiveRipple
    {
        public Vector2 uv;
        public float timer;
    }


    private RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    private void Init()
    {
        if (_cumulativeRT == null)
        {
            _cumulativeRT = CreateRT();
        }
        if (_outputRT == null)
        {
            _outputRT = CreateRT();
        }
        if (_singleEffectRT == null)
        {
            _singleEffectRT = CreateRT();
        }
        if (_tempRT == null)
        {
            _tempRT = CreateRT();
        }

        _blackTex = new Texture2D(2, 2);
        _blackTex.SetPixel(0, 0, Color.black);
        _blackTex.SetPixel(1, 0, Color.black);
        _blackTex.SetPixel(0, 1, Color.black);
        _blackTex.SetPixel(1, 1, Color.black);
        _blackTex.Apply();

        if (_cumulativeMat == null)
        {
            _cumulativeMat = new Material(CumulativeShader);
        }
        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);
        }

        // if (GetComponent<MeshRenderer>() != null)
        //     GetComponent<MeshRenderer>().sharedMaterial = _cumulativeMat;

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);

        // _cumulativeMat.SetFloat("_HitTexScale", 0.5f);

        _hitEffectMat.SetFloat("_EdgeMax", 0.05f);
        _hitEffectMat.SetFloat("_Thickness", 0.01f);

        _hitEffectMat.DisableKeyword("_CIRCLE_FILL");
        _hitEffectMat.DisableKeyword("_CIRCLE_FILL_SDF");
        _hitEffectMat.EnableKeyword("_CIRCLE_STROKE");
        _hitEffectMat.DisableKeyword("_CIRCLE_STROKE_SDF");

        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitAreaTex", _cumulativeRT);
        _isInitialized = true;

        if (EnableDebugMode && DebugQuad != null)
            DebugQuad.GetComponent<MeshRenderer>().sharedMaterial = _hitEffectMat;
    }

    public void ClearAll()
    {
        if (_cumulativeRT != null) _cumulativeRT.Release();
        if (_outputRT != null) _outputRT.Release();
        if (_singleEffectRT != null) _singleEffectRT.Release();
        if (_tempRT != null) _tempRT.Release();

        if (_blackTex != null) Destroy(_blackTex);

        if (_hitEffectMat != null) Destroy(_hitEffectMat);
        if (_cumulativeMat != null) Destroy(_cumulativeMat);

        _isInitialized = false;
    }

    public void GetHit(RaycastHit hit)
    {
        if (!_isInitialized)
        {
            Init();
        }
        _activeRipples.Add(new ActiveRipple
        {
            uv = hit.textureCoord,
            timer = 0.0f
        });

        _forceClearTimer = 0f;
        // StartCoroutine(AnimateSingleHit(hit.textureCoord));
    }



    void Update()
    {
        if (!_isInitialized || _cumulativeMat == null || _hitEffectMat == null) return;

        if (_activeRipples.Count == 0)
        {
            _forceClearTimer += Time.deltaTime;
            if (_forceClearTimer > ForceClearInterval)
            {
                Graphics.Blit(_blackTex, _cumulativeRT);
                _forceClearTimer = 0.0f;
                return;
            }
        }
        else
        {
            _forceClearTimer = 0f;
        }

        // Each frame, darken the entire texture slightly.
        // Here, _HitTex is set to a black texture, indicating no new additions, only decay.
        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _blackTex);

        float frameDecay = Mathf.Pow(DecayPerSecond, Time.deltaTime * 60f);
        _cumulativeMat.SetFloat("_Decay", frameDecay);
        _cumulativeMat.SetFloat("_MinThreshold", MinDecayThreshold);

        Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
        Graphics.Blit(_tempRT, _cumulativeRT); // Swap back to _cumulativeRT
        // --- Step 2: Process and blend all active ripples ---
        // Iterate in reverse order for safe removal
        for (int i = _activeRipples.Count - 1; i >= 0; i--)
        {
            ActiveRipple ripple = _activeRipples[i];
            ripple.timer += Time.deltaTime;

            // If the ripple duration has ended, remove it from the list
            if (ripple.timer > HitImpactDuration)
            {
                _activeRipples.RemoveAt(i);
                continue;
            }

            // Calculate the current state of the ripple
            float progress = ripple.timer / HitImpactDuration;
            float currentSize = HitImpactScale * progress;
            float currentFade = 1.0f - progress;

            // 2.1 Draw single ripple to _singleEffectRT
            _hitEffectMat.SetVector("_HitUV", ripple.uv);
            _hitEffectMat.SetFloat("_Size", currentSize);
            _hitEffectMat.SetFloat("_Fade", currentFade);

            // This step generates a black background on _singleEffectRT, with only the current white circle
            Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

            // 2.2 Blend the single ripple onto the cumulative texture
            // Key: Here _Decay is set to 1.0 because we don't want the old texture to darken again (step 1 already darkened it)
            // We only want to max blend the new white circle.
            _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
            _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);
            _cumulativeMat.SetFloat("_Decay", 1.0f);
            _cumulativeMat.SetFloat("_MinThreshold", 0.0f);

            Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
            Graphics.Blit(_tempRT, _cumulativeRT);
        }
    }

    // void Update()
    // {
    //     if (!_isInitialized || _cumulativeMat == null || _hitEffectMat == null) return;
    //     // Debug.Log("Active Ripples Count: " + _activeRipples.Count);
    //     // --- Step 1: Global Decay ---
    //     // Each frame, darken the entire texture slightly.
    //     // Here, _HitTex is set to a black texture, indicating no new additions, only decay.
    //     _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
    //     _cumulativeMat.SetTexture("_HitTex", _blackTex);

    //     Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
    //     Graphics.Blit(_tempRT, _cumulativeRT); // Swap back to _cumulativeRT

    //     // --- Step 2: Process and blend all active ripples ---
    //     // Iterate in reverse order for safe removal
    //     for (int i = _activeRipples.Count - 1; i >= 0; i--)
    //     {
    //         ActiveRipple ripple = _activeRipples[i];
    //         ripple.timer += Time.deltaTime;

    //         // If the ripple duration has ended, remove it from the list
    //         if (ripple.timer > HitImpactDuration)
    //         {
    //             _activeRipples.RemoveAt(i);
    //             continue;
    //         }

    //         // Calculate the current state of the ripple
    //         float progress = ripple.timer / HitImpactDuration;
    //         float currentSize = HitImpactScale * progress;
    //         float currentFade = 1.0f - progress;

    //         // 2.1 Draw single ripple to _singleEffectRT
    //         _hitEffectMat.SetVector("_HitUV", ripple.uv);
    //         _hitEffectMat.SetFloat("_Size", currentSize);
    //         _hitEffectMat.SetFloat("_Fade", currentFade);

    //         // This step generates a black background on _singleEffectRT, with only the current white circle
    //         Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

    //         // 2.2 Blend the single ripple onto the cumulative texture
    //         // Key: Here _Decay is set to 1.0 because we don't want the old texture to darken again (step 1 already darkened it)
    //         // We only want to max blend the new white circle.
    //         _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
    //         _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);
    //         _cumulativeMat.SetFloat("_Decay", DecayPerSecond);

    //         Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
    //         Graphics.Blit(_tempRT, _cumulativeRT);
    //     }
    // }

    // Coroutine to animate a single hit effect over time. Not used in current implementation.
    IEnumerator AnimateSingleHit(Vector2 hitUV)
    {
        float timer = 0.0f;
        Material hitInstanceMat = new Material(HitEffectShader);
        hitInstanceMat.SetVector("_HitUV", hitUV);
        while (timer < HitImpactDuration)
        {
            timer += Time.deltaTime;
            float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
            strength = Mathf.Clamp(1 - strength, 0.0f, 1.0f);
            hitInstanceMat.SetFloat("_Size", strength);
            hitInstanceMat.SetFloat("_Fade", 1 - strength);

            Graphics.Blit(null, _singleEffectRT, hitInstanceMat, pass: 0);   // Get single hit effect

            RenderTexture tempRT = RenderTexture.GetTemporary(_cumulativeRT.descriptor);
            Graphics.Blit(_cumulativeRT, tempRT, _cumulativeMat, pass: 0);    // Get cumulative effect
            Graphics.Blit(tempRT, _cumulativeRT);                          // Copy back to cumulative RT
            RenderTexture.ReleaseTemporary(tempRT);

            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitAreaTex", _cumulativeRT);

            yield return null;
        }

        Destroy(hitInstanceMat);
    }

    void OnDestroy()
    {
        ClearAll();
    }

    void OnGUI()
    {
        if (_cumulativeRT != null && Event.current.type == EventType.Repaint)
        {
            GUI.DrawTexture(new Rect(10, 10, 128, 128), _cumulativeRT);
            GUI.Label(new Rect(10, 145, 200, 20), $"Active Ripples: {_activeRipples.Count}");
            GUI.Label(new Rect(10, 165, 200, 20), $"Clear Timer: {_forceClearTimer:F2}s");
        }
    }
}

[CustomEditor(typeof(ShieldHitEffect))]
public class ShieldHitEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShieldHitEffect shieldHitEffect = (ShieldHitEffect)target;
        if (GUILayout.Button("Clear All"))
        {
            shieldHitEffect.ClearAll();
        }
    }
}

#endif