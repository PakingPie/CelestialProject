using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// [ExecuteInEditMode]
public class GunShoot : MonoBehaviour
{

    [Header("Gun Settings")]
    [Tooltip("Number of updates per second for the gun targeting system.")]
    public int UpdateRate = 60;

    [Tooltip("The range within which the gun can target enemies.")]
    public Vector2 ActiveRange = new Vector2(5f, 200f);
    [Tooltip("Duration of the gun shoot effect in seconds.")]
    public float GunShootInterval = 1.0f;
    public float TurretRotateSpeed = 5f;
    private Transform _targetPoint;


    void Start()
    {
        InvokeRepeating("Shoot", 0.0f, 5.0f);
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        InvokeRepeating("LockOn", 0f, 1.0f / UpdateRate);
    }
    void Update()
    {

    }

    void LockOn()
    {
        Vector3 dir = _targetPoint.position - transform.position;
        Quaternion look_rotation = Quaternion.LookRotation(dir);
        Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
        transform.rotation = Quaternion.Euler(0f, rotation.y, 0f);
    }
    
    public void Shoot()
    {
    }

    public void UpdateTarget()
    {
        if (_targetPoint != null)
        {
            if (Vector3.Distance(transform.position, _targetPoint.position) > ActiveRange.y)
            {
                _targetPoint = null;
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

        if (nearest_enemy && shortest_distance <= ActiveRange.y)
        {
            _targetPoint = nearest_enemy.transform;
            Debug.Log("Target Acquired: " + nearest_enemy.name);
        }
        else
        {
            _targetPoint = null;
        }
    }


    public void GunEnable()
    {

    }

    public void GunDisable()
    {

    }


}