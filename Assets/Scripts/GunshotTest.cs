using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public class GunshotTest : MonoBehaviour
{
    public float RippleEffectTime = 1.0f;
    public float RippleRadius = 0.1f;
    public void Shoot()
    {
        RaycastHit hit;
        Physics.Raycast(transform.position, transform.forward, out hit);
        if (hit.collider != null)
        {
            hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetVector("_Center", hit.point);
            StartCoroutine(RippleEffect(hit));
        }
    }

    IEnumerator RippleEffect(RaycastHit hit)
    {
        float elapsed = 0f;
        while (elapsed < RippleEffectTime)
        {
            elapsed += Time.deltaTime;
            float rippleStrength = Mathf.Lerp(0.0f, RippleRadius, elapsed / RippleEffectTime);
            hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetFloat("_Radius", rippleStrength);
            yield return null;
        }
        hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetFloat("_Radius", 0f);
    }
}

[CustomEditor(typeof(GunshotTest))]
public class GunshotEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GunshotTest gunshotTest = (GunshotTest)target;
        if (GUILayout.Button("Shoot"))
        {
            gunshotTest.Shoot();
        }
    }
}