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
    public GameObject TempGO;
    public float HitImpactDuration = 1.0f;
    public float HitImpactScale = 0.1f;
    public int TextureSize = 64;
    public Shader HitEffectShader;
    public Shader CumulativeShader;
    private RenderTexture _cumulativeRT;
    private RenderTexture _currRT;
    private RenderTexture _singleEffectRT;
    private RenderTexture _prevRT;

    private Material _hitEffectMat;
    private Material _cumulativeMat;
    private Material _combineMat;

    public void ClearAll()
    {
        if (_cumulativeRT != null)
        {
            _cumulativeRT.Release();
            _cumulativeRT = null;
        }
        if (_currRT != null)
        {
            _currRT.Release();
            _currRT = null;
        }
        if (_singleEffectRT != null)
        {
            _singleEffectRT.Release();
            _singleEffectRT = null;
        }
        if (_prevRT != null)
        {
            _prevRT.Release();
            _prevRT = null;
        }

        if (_hitEffectMat != null)
        {
            _hitEffectMat = null;
        }
        if (_cumulativeMat != null)
        {
            _cumulativeMat = null;
        }
        if (_combineMat != null)
        {
            _combineMat = null;
        }
    }

    public void GetHit(RaycastHit hit)
    {
        if (_cumulativeRT == null)
        {
            _cumulativeRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }
        if (_currRT == null)
        {
            _currRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }
        if (_singleEffectRT == null)
        {
            _singleEffectRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }
        if (_prevRT == null)
        {
            _prevRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }

        if (_cumulativeMat == null)
        {
            _cumulativeMat = new Material(CumulativeShader);
        }
        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);
        }
        if (_combineMat == null)
        {
            _combineMat = new Material(Shader.Find("Unlit/Combine"));
        }

        if (TempGO != null)
            TempGO.GetComponent<MeshRenderer>().sharedMaterial = _hitEffectMat;

        // ShieldGO.GetComponent<MeshRenderer>().sharedMaterial = _cumulativeMat;
        if (GetComponent<MeshRenderer>() != null)
            GetComponent<MeshRenderer>().sharedMaterial = _cumulativeMat;

        _cumulativeMat.SetTexture("_MainTex", _cumulativeRT);
        _cumulativeMat.SetTexture("_PrevTex", _prevRT);
        _cumulativeMat.SetTexture("_HitTex", _singleEffectRT);

        _cumulativeMat.SetVector("_HitUV", hit.textureCoord);
        _cumulativeMat.SetFloat("_HitTexScale", .05f);

        _hitEffectMat.SetFloat("_EdgeMax", 0.0f);
        _hitEffectMat.SetFloat("_Thickness", 0.02f);
        // _hitEffectMat.DisableKeyword("Circle_Fill");
        // _hitEffectMat.DisableKeyword("Circle_FillSDF");
        // _hitEffectMat.DisableKeyword("Circle_Stroke");
        // _hitEffectMat.EnableKeyword("Circle_StokeSDF");




        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitAreaTex", _currRT);

        float elapsed = 0.0f;
        StartCoroutine(GetHit(elapsed));
    }

    IEnumerator GetHit(float timer)
    {
        if(_singleEffectRT != null)
            Graphics.Blit(null, _singleEffectRT, _hitEffectMat, pass: 0);   // Get single hit effect

        Graphics.Blit(null, _cumulativeRT, _cumulativeMat, pass: 0);
        Graphics.Blit(_cumulativeRT, _currRT);

        // RenderTexture swap = _prevRT;
        // _prevRT = _cumulativeRT;
        // _cumulativeRT = swap;

        if(_singleEffectRT != null)
            _singleEffectRT.Release();

        yield return null;
        StartCoroutine(GetHit(timer));
    }

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