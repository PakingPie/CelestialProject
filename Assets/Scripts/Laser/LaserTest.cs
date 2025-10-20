using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// [ExecuteInEditMode]
public class LaserTest : MonoBehaviour
{

    public LineRenderer LaserLineRenderer;
    [Header("Laser Settings")]
    [Tooltip("Number of updates per second for the laser targeting system.")]
    public int UpdateRate = 60;

    [Tooltip("The range within which the laser can target enemies.")]
    public Vector2 LaserActiveRange = new Vector2(50f, 500f);
    [Tooltip("Duration of the laser effect in seconds.")]
    public float LaserEffectDuration = 2.5f;

    private Transform _targetPoint;


    void Start()
    {
        InvokeRepeating("Shoot", 0.0f, 5.0f);
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        LaserLineRenderer.SetPosition(0, transform.position);
        LaserLineRenderer.SetPosition(1, _targetPoint.position);
    }
    void Update()
    {
        LaserLineRenderer.SetPosition(0, transform.position);
        LaserLineRenderer.SetPosition(1, _targetPoint.position);
    }
    public void Shoot()
    {
        StartCoroutine(LaserBeam());
    }

    public void UpdateTarget()
    {
        if (_targetPoint != null)
        {
            if (Vector3.Distance(transform.position, _targetPoint.position) > LaserActiveRange.y)
            {
                _targetPoint = null;
                LaserDisable();
            }
            else
            {
                return;
            }
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Foe");
        float shortest_distance = Mathf.Infinity;
        GameObject nearest_enemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance_to_enemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance_to_enemy < shortest_distance)
            {
                shortest_distance = distance_to_enemy;
                nearest_enemy = enemy;
            }
        }

        if (nearest_enemy && shortest_distance <= LaserActiveRange.y)
        {
            _targetPoint = nearest_enemy.transform;
            Debug.Log("Target Acquired: " + nearest_enemy.name);
        }
        else
        {
            _targetPoint = null;
        }
    }


    public void LaserEnable()
    {
        if (!LaserLineRenderer.enabled)
        {
            LaserLineRenderer.enabled = true;
        }
        StartCoroutine(LaserBeam());
    }

    public void LaserDisable()
    {
        if (LaserLineRenderer.enabled)
        {
            LaserLineRenderer.enabled = false;
        }
    }

    IEnumerator LaserBeam()
    {
        LaserLineRenderer.material.SetFloat("_Active_Time", 0.0f);
        for (float t = 0.0f; t <= LaserEffectDuration; t += Time.deltaTime)
        {
            yield return null;
            if (t > LaserEffectDuration / 2f)   // Fade out
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (t - LaserEffectDuration / 2f)));
            }
            else                            // Fade in
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(t));
            }
        }
        // yield return new WaitForSeconds(LaserEffectDuration);
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