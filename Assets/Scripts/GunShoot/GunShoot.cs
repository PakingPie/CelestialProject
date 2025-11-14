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
    public GlobalHelper.Faction FireTarget = GlobalHelper.Faction.Foe;
    public GlobalHelper.GuidanceType GuidanceType = GlobalHelper.GuidanceType.Lead;

    private Vector3 _targetPosLastFrame;


    void Start()
    {
        InvokeRepeating("Shoot", 0.0f, GunShootInterval);
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        InvokeRepeating("LockOn", 0f, 1.0f / UpdateRate);
        if(_targetPoint != null)
        {
            _targetPosLastFrame = _targetPoint.position;
        }
    }

    void LockOn()
    {
        if (_targetPoint == null)
        {
            return;
        }
        if (GuidanceType == GlobalHelper.GuidanceType.Pursuit)
        {
            Vector3 dir = _targetPoint.position - transform.position;
            Quaternion look_rotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
            transform.rotation = Quaternion.Euler(rotation);
        }
        else
        {
            // Get where target will be in one second.
            Vector3 targetVelocity = _targetPoint.position - _targetPosLastFrame;
            targetVelocity /= 1;
            //=====================================================

            // Figure out time to impact based on distance.          
            float bulletSpeed = BulletPrefab.GetComponent<BulletPhysics>().Speed;
            float distanceToTarget = Vector3.Distance(transform.position, _targetPoint.position);
            float timeToImpact = distanceToTarget / bulletSpeed;
            Vector3 futureTargetPos = _targetPoint.position + targetVelocity * timeToImpact;
            Vector3 dir = futureTargetPos - transform.position;
            Quaternion look_rotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
            transform.rotation = Quaternion.Euler(rotation);
        }
    }

    public void Shoot()
    {
        if (_targetPoint != null && Vector3.Distance(transform.position, _targetPoint.position) < ActiveRange.y)
        {
            var bulletPrefab = Instantiate(BulletPrefab, BulletSpawnPoint.position, BulletSpawnPoint.rotation);
            bulletPrefab.GetComponent<BulletPhysics>().FireTarget = FireTarget;
            bulletPrefab.GetComponent<BulletPhysics>().TargetObject = _targetPoint;
        }
    }

    public void StopShooting()
    {
        CancelInvoke("Shoot");
    }

    public void StartShooting()
    {
        InvokeRepeating("Shoot", 0.0f, GunShootInterval);
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

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);

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

    void OnDrawGizmos()
    {
        // Draw a line from the gun to the target point
        if (_targetPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _targetPoint.position);
        }
    }
}