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

    public GameObject BulletPrefab;
    public Transform BulletSpawnPoint;


    void Start()
    {
        InvokeRepeating("Shoot", 0.0f, GunShootInterval);
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        InvokeRepeating("LockOn", 0f, 1.0f / UpdateRate);
    }

    void LockOn()
    {
        if(_targetPoint == null)
        {
            return;
        }
        Vector3 dir = _targetPoint.position - transform.position;
        Quaternion look_rotation = Quaternion.LookRotation(dir);
        Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
        transform.rotation = Quaternion.Euler(rotation);
    }

    public void Shoot()
    {
        if (_targetPoint != null && Vector3.Distance(transform.position, _targetPoint.position) < ActiveRange.y)
        {
            GameObject fired_object = Instantiate(BulletPrefab, BulletSpawnPoint.position, BulletSpawnPoint.rotation);
        }
    }

    public void UpdateTarget()
    {
        // if (_targetPoint != null)
        // {
        //     if (Vector3.Distance(transform.position, _targetPoint.position) > ActiveRange.y)
        //     {
        //         _targetPoint = null;
        //     }
        //     else
        //     {
        //         return;
        //     }
        // }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Foe");

        if (enemies.Length == 0)
        {
            _targetPoint = null;
            return;
        }
        
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