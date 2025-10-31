using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

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
    private RenderTexture _currRT;
    private RenderTexture _prevRT;
    private RenderTexture _tempRT;

    private Material _hitEffectMat;

    public void ClearAll()
    {
        if (_currRT != null)
        {
            _currRT.Release();
            _currRT = null;
        }
        if (_prevRT != null)
        {
            _prevRT.Release();
            _prevRT = null;
        }
        if (_tempRT != null)
        {
            _tempRT.Release();
            _tempRT = null;
        }

        if (_hitEffectMat != null)
        {
            _hitEffectMat = null;
        }
    }

    public void GetHit(RaycastHit hit)
    {
        if (_currRT == null)
        {
            _currRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        }
        if (_prevRT == null)
        {
            _prevRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        }
        if (_tempRT == null)
        {
            _tempRT = new RenderTexture(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        }
        if (_hitEffectMat == null)
        {
            _hitEffectMat = new Material(HitEffectShader);
        }

        TempGO.GetComponent<MeshRenderer>().sharedMaterial = _hitEffectMat;

        GetComponent<MeshRenderer>().sharedMaterial.SetVector("_HitUV", hit.textureCoord);
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_MainTex", _currRT);
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_HitTex", _prevRT);


        float elapsed = 0.0f;
        StartCoroutine(GetHit(elapsed));
    }

    IEnumerator GetHit(float timer)
    {
        RenderTexture tempRT = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(_currRT, tempRT, GetComponent<MeshRenderer>().sharedMaterial, pass: 0);
        Graphics.Blit(tempRT, _currRT);
        RenderTexture.ReleaseTemporary(tempRT);

        _hitEffectMat.SetFloat("_EdgeMax", 0.15f);
        _hitEffectMat.SetFloat("_Thickness", 0.01f);
        if (timer < HitImpactDuration)
        {
            timer += Time.deltaTime;
            float strength = Mathf.Lerp(HitImpactScale, 0.0f, timer / HitImpactDuration);
            _hitEffectMat.SetFloat("_Size", 1 - strength);
            Graphics.Blit(null, _tempRT, _hitEffectMat);
            Graphics.Blit(_tempRT, _prevRT);
            yield return null;
            _hitEffectMat.SetFloat("_Size", 0.0f);
            _hitEffectMat.SetFloat("_EdgeMax", 0.0f);
            _hitEffectMat.SetFloat("_Thickness", 0.0f);
            StartCoroutine(GetHit(timer));
        }
        else
        {
            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Fade", 1 - timer / HitImpactDuration);
            StartCoroutine(EffectFade(timer));
        }
    }

    IEnumerator EffectFade(float timer)
    {
        RenderTexture tempRT = RenderTexture.GetTemporary(TextureSize, TextureSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(_currRT, tempRT, GetComponent<MeshRenderer>().sharedMaterial, pass: 1);
        Graphics.Blit(tempRT, _currRT);
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