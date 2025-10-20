using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class LaserTest : MonoBehaviour
{
    public Transform targetPoint;

    public LineRenderer LaserLineRenderer;

    public Vector2 LaserActiveRange = new Vector2(50f, 500f);

    public float LaserEffectDuration = 0.5f;
    private float _laserEffectTimer = 0f;

    void Start()
    {
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    public void UpdateTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Foe");
        float shortest_distance = Mathf.Infinity;
        GameObject nearest_enemy = null;

        foreach(GameObject enemy in enemies)
        {
            float distance_to_enemy = Vector3.Distance(transform.position, enemy.transform.position);
           if(distance_to_enemy < shortest_distance)
            {
                shortest_distance = distance_to_enemy;
                nearest_enemy = enemy;
            }
        }

        if (nearest_enemy && shortest_distance <= LaserActiveRange.y)
        {
            targetPoint = nearest_enemy.transform;
        }
        else
        {
            targetPoint = null;
        }
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