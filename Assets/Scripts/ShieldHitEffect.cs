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
#elif Test2
public class ShieldHitEffect : MonoBehaviour
{
    public float HitImpactDuration = 1.0f;
    public float HitImpactScale = 0.1f;
    private RenderTexture _currRT;

    public GameObject ShieldGO;

    public void GetHit(RaycastHit hit)
    {
        if (_currRT == null)
        {
            _currRT = new RenderTexture(128, 128, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        }
        GetComponent<MeshRenderer>().sharedMaterial.SetVector("_Center", hit.textureCoord);
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", _currRT);

        RenderTexture tempRT = RenderTexture.GetTemporary(128, 128, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(_currRT, tempRT, GetComponent<MeshRenderer>().sharedMaterial);
        Graphics.Blit(tempRT, _currRT);

        RenderTexture.ReleaseTemporary(tempRT);
    }

}
#else
public class ShieldHitEffect : MonoBehaviour
{
    public GameObject ShieldGO;
    // public GameObject TempGO;
    public float HitImpactDuration = 1.0f;
    public float HitImpactScale = 0.1f;
    public int TextureSize = 64;
    public Shader HitEffectShader;
    public Shader CumulativeShader;

    public GameObject DebugQuad;

    private RenderTexture _cumulativeRT;
    private RenderTexture _outputRT;
    private RenderTexture _singleEffectRT;
    private RenderTexture _tempRT;
    private Texture2D _blackRT;
    private Material _hitEffectMat;
    private Material _cumulativeMat;

    private bool _isInitialized = false;

    private class ActiveRipple
    {
        public Vector2 uv;
        public float timer;
    }
    private List<ActiveRipple> _activeRipples = new List<ActiveRipple>();

    private RenderTexture CreateRT()
    {
        RenderTexture rt = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
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

        Texture2D _clearTex = new Texture2D(1, 1);
        _clearTex.SetPixel(0, 0, Color.black);
        _clearTex.Apply();

        if (_cumulativeMat == null)
        {
            _cumulativeMat = new Material(CumulativeShader);
        }
        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);
        }

        if (GetComponent<MeshRenderer>() != null)
            GetComponent<MeshRenderer>().sharedMaterial = _cumulativeMat;

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);

        _cumulativeMat.SetFloat("_HitTexScale", 0.5f);

        _hitEffectMat.SetFloat("_EdgeMax", 0.0f);
        _hitEffectMat.SetFloat("_Thickness", 0.1f);

        _hitEffectMat.DisableKeyword("_CIRCLE_FILL");
        _hitEffectMat.DisableKeyword("_CIRCLE_FILLSDF");
        _hitEffectMat.EnableKeyword("_CIRCLE_STROKE");
        _hitEffectMat.DisableKeyword("_CIRCLE_STROKESDF");

        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitAreaTex", _cumulativeRT);
        _isInitialized = true;

        DebugQuad.GetComponent<MeshRenderer>().sharedMaterial = _hitEffectMat;
    }

    public void ClearAll()
    {
        if (_cumulativeRT != null) _cumulativeRT.Release();
        if (_outputRT != null) _outputRT.Release();
        if (_singleEffectRT != null) _singleEffectRT.Release();
        if (_tempRT != null) _tempRT.Release();

        if (_blackRT != null) Destroy(_blackRT);

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
        // StartCoroutine(AnimateSingleHit(hit.textureCoord));
    }



    void Update()
    {
        if (!_isInitialized || _cumulativeMat == null || _hitEffectMat == null) return;

        // --- 步骤 1: 全局衰减 (Global Decay) ---
        // 每一幀都让整张图变暗一点点。
        // 这里 _HitTex 传入黑图，表示这步不添加新内容，只做衰减。
        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_HitTex", _blackRT);

        Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
        Graphics.Blit(_tempRT, _cumulativeRT); // 交换回 _cumulativeRT

        // --- 步骤 2: 处理并叠加所有活跃波纹 ---
        // 倒序遍历以便安全移除
        for (int i = _activeRipples.Count - 1; i >= 0; i--)
        {
            ActiveRipple ripple = _activeRipples[i];
            ripple.timer += Time.deltaTime;

            // 如果波纹时间结束，从列表中移除
            if (ripple.timer > HitImpactDuration)
            {
                _activeRipples.RemoveAt(i);
                continue;
            }

            // 计算当前波纹的状态
            float progress = ripple.timer / HitImpactDuration;
            float currentSize = HitImpactScale * progress;
            float currentFade = 1.0f - progress;

            // 2.1 绘制单个波纹到 _singleEffectRT
            _hitEffectMat.SetVector("_HitUV", ripple.uv);
            _hitEffectMat.SetFloat("_Size", currentSize);
            _hitEffectMat.SetFloat("_Fade", currentFade);

            // 这一步生成的 _singleEffectRT 背景是黑的，只有当前这一个白圈
            Graphics.Blit(null, _singleEffectRT, _hitEffectMat);

            // 2.2 将单个波纹叠加到累积图上
            // 关键：这里 _Decay 设为 1.0，因为我们不想让旧的图再变暗一次（步骤1已经暗过了）
            // 我们只想把新的白圈 Max Blend 上去。
            _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
            _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);
            _cumulativeMat.SetFloat("_Decay", 1.0f);

            Graphics.Blit(_cumulativeRT, _tempRT, _cumulativeMat);
            Graphics.Blit(_tempRT, _cumulativeRT);
        }
    }


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

    // IEnumerator GetHit(float timer)
    // {
    //     _coroutineCount++;
    //     timer += Time.deltaTime;
    //     if (timer < HitImpactDuration)
    //     {
    //         float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
    //         strength = Mathf.Clamp(1 - strength, 0.0f, 1.0f);
    //         _hitEffectMat.SetFloat("_Size", strength);
    //         _hitEffectMat.SetFloat("_Fade", 1 - strength);
    //     }
    //     if(_singleEffectRT == null)
    //         _singleEffectRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);


    //     Graphics.Blit(null, _singleEffectRT, _hitEffectMat, pass: 0);   // Get single hit effect

    //     Graphics.Blit(null, _cumulativeRT, _cumulativeMat, pass: 0);    // Get cumulative effect
    //     Graphics.Blit(_cumulativeRT, _currRT);                          // Copy cumulative to current RT

    //     RenderTexture swap = _currRT;   // Swap current and previous RTs
    //     _currRT = _cumulativeRT;
    //     _cumulativeRT = swap;

    //     if (_singleEffectRT != null)
    //         _singleEffectRT.Release();

    //     yield return null;
    //     StartCoroutine(GetHit(timer));
    // }

    // void OnDestroy()
    // {
    //     ClearAll();        
    // }

    // IEnumerator GetHit(float timer)
    // {
    //     timer += Time.deltaTime;

    //     RenderTexture tempRT = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    //     Graphics.Blit(_cumulativeRT, tempRT, _cumulativeMat, pass: 0);
    //     Graphics.Blit(tempRT, _cumulativeRT);
    //     RenderTexture.ReleaseTemporary(tempRT);

    //     if (timer < HitImpactDuration)
    //     {
    //         float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
    //         strength = Mathf.Clamp(1 - strength, 0.0f, 0.7f);
    //         _hitEffectMat.SetFloat("_Size", strength);
    //         _hitEffectMat.SetFloat("_Fade", 1 - strength);
    //     }

    //     _currRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
    //     _singleEffectRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

    //     Graphics.Blit(null, _singleEffectRT, _hitEffectMat, pass: 0);
    //     Graphics.Blit(_singleEffectRT, _currRT);

    //     _cumulativeMat.SetTexture("_HitTex", _currRT);
    //     _cumulativeMat.SetTexture("_LastFrameTex", _prevRT);

    //     RenderTexture swap = _prevRT;
    //     _prevRT = _cumulativeRT;
    //     _cumulativeRT = swap;

    //     // else
    //     // {
    //     //     _hitEffectMat.SetFloat("_Size", 0.0f);
    //     //     _hitEffectMat.SetFloat("_EdgeMax", 0.0f);
    //     //     _hitEffectMat.SetFloat("_Thickness", 0.0f);

    //     //     Graphics.Blit(Texture2D.blackTexture, _singleEffectRT);
    //     //     Graphics.Blit(Texture2D.blackTexture, _prevRT);
    //     //     _cumulativeMat.SetFloat("_Fade", 0.0f);
    //     //     StopCoroutine(GetHit(timer));
    //     //     yield break;
    //     // }
    //     yield return null;
    //     StartCoroutine(GetHit(timer));
    // }

    /*
        RenderTexture tempRT = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(_cumulativeRT, tempRT, _cumulativeMat, pass: 0);
        Graphics.Blit(tempRT, _cumulativeRT);
        RenderTexture.ReleaseTemporary(tempRT);
        float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
        strength = Mathf.Clamp(1 - strength, 0.0f, 0.7f);
        _hitEffectMat.SetFloat("_Size", strength);

        _hitEffectRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        _singleEffectRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

        Graphics.Blit(null, _singleEffectRT, _hitEffectMat, pass: 0);
        Graphics.Blit(_singleEffectRT, _hitEffectRT);

        _cumulativeMat.SetTexture("_HitTex", _hitEffectRT);
    */

    IEnumerator EffectFade(float timer)
    {
        RenderTexture tempRT = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(_cumulativeRT, tempRT, _cumulativeMat, pass: 1);
        Graphics.Blit(tempRT, _cumulativeRT);
        RenderTexture.ReleaseTemporary(tempRT);
        yield return null;
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