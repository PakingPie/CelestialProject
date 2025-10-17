using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class LaserTest : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Transform targetPoint;

    public LineRenderer LaserLineRenderer;

    public Vector2 LaserActiveRange = new Vector2(50f, 500f);

    public float LaserEffectDuration = 0.5f;
    private float _laserEffectTimer = 0f;

    public void UpdateTarget(Transform newTarget)
    {
        targetPoint = newTarget;
    }


    public void LaserEnable()
    {
        if (!LaserLineRenderer.enabled)
        {
            LaserLineRenderer.enabled = true;
        }

        LaserLineRenderer.SetPosition(0, transform.position);
        LaserLineRenderer.SetPosition(1, targetPoint.position);
    }

    public void LaserDisable()
    {
        if (LaserLineRenderer.enabled)
        {
            LaserLineRenderer.enabled = false;
        }
    }
}

[CustomEditor(typeof(LaserTest))]
public class LaserTestEditor : Editor
{
    LaserTest laserTest;
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        laserTest = (LaserTest)target;
        if (GUILayout.Button("Enable Laser"))
        {
            laserTest.LaserEnable();
        }

        if (GUILayout.Button("Disable Laser"))
        {
            laserTest.LaserDisable();
        }
    }
}