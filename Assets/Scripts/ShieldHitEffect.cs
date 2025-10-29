using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// [ExecuteInEditMode]
public class ShieldHitEffect : MonoBehaviour
{
    public float RippleEffectTime = 0.1f;
    public float RippleRadius = 0.1f;
    public GameObject ShieldGO;

    public void GetHit(RaycastHit hit)
    {
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetVector("_Center", hit.point);
        StartCoroutine(RippleEffect(hit));
    }

    IEnumerator RippleEffect(RaycastHit hit)
    {
        float elapsed = 0f;
        while (elapsed < RippleEffectTime)
        {
            elapsed += Time.deltaTime;
            float rippleStrength = Mathf.Lerp(RippleRadius, 0.0f, elapsed / RippleEffectTime);
            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Radius", 1 - rippleStrength);
            ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Hardness", Mathf.Clamp01((elapsed / RippleEffectTime)));
            yield return null;
        }
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Radius", 0.0f);
        ShieldGO.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Hardness", 1.0f);
    }

}