using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class GunshotTest : MonoBehaviour
{
    public float HitImpactDuration = 5.0f;
    public float HitImpactScale = 0.1f;

    public void AutoShoot()
    {
        InvokeRepeating("Shoot", 0.0f, 0.5f);   
    }
    public void StopAutoShoot()
    {
        CancelInvoke("Shoot");
    }
    public void Shoot()
    {
        RaycastHit hit;
        Physics.Raycast(transform.position - transform.forward, transform.forward, out hit);
        if (hit.collider != null)
        {
            // hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetVector("_Center", hit.point);
            // StartCoroutine(RippleEffect(hit));
            hit.collider.gameObject.GetComponent<ShieldHitEffect>().HitImpactDuration = HitImpactDuration;
            hit.collider.gameObject.GetComponent<ShieldHitEffect>().HitImpactScale = HitImpactScale;
            hit.collider.gameObject.GetComponent<ShieldHitEffect>().GetHit(hit);
        }
    }

    // IEnumerator RippleEffect(RaycastHit hit)
    // {
    //     float elapsed = 0f;
    //     while (elapsed < HitImpactDuration)
    //     {
    //         elapsed += Time.deltaTime;
    //         float rippleStrength = Mathf.Lerp(0.0f, HitImpactScale, elapsed / HitImpactDuration);
    //         hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetFloat("_Radius", rippleStrength);
    //         hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetFloat("_Hardness",1 - elapsed / HitImpactDuration);

    //         yield return null;
    //     }
    //     hit.collider.gameObject.GetComponent<MeshRenderer>().material.SetFloat("_Radius", 0.0f);
    // }
}

#if UNITY_EDITOR
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

        if(GUILayout.Button("Start Auto Shoot"))
        {
            gunshotTest.AutoShoot();
        }

        if(GUILayout.Button("Stop Auto Shoot"))
        {
            gunshotTest.StopAutoShoot();
        }
    }
}
#endif