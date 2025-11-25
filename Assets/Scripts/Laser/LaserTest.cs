using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static GlobalHelper;
// [ExecuteInEditMode]
public class LaserTest : WeaponBase
{
    [Header("Laser Turret Settings")]
    public LineRenderer LaserLineRenderer;
    [Header("Laser Settings")]

    [Tooltip("Duration of the laser effect in seconds.")]
    public float LaserEffectDuration = 2.5f;
    public float TurretRotateSpeed = 5f;
    public int LaserDamageCap = 10;
    public int LaserDPS = 1;
    public bool IsFiring = false;


    void Start()
    {
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        LaserLineRenderer.SetPosition(0, transform.position);

        if (Targeted != null)
        {
            LaserLineRenderer.SetPosition(1, Targeted.position);
        }
    }
    void Update()
    {
        LaserLineRenderer.SetPosition(0, transform.position);
        LaserLineRenderer.SetPosition(1, Targeted.position);

        if (!IsAimed)
            IsFiring = false;
        else
            IsFiring = true;

        if (IsFiring)
        {
            LaserEnable();
        }
        else
        {
            LaserDisable();
        }

        if (IsIdle || Targeted == null)
        {
            if (!IsTurretAtRest)
                RotateTurretToIdle();
            IsAimed = false;
        }
        else
        {
            Vector3 aimPosition = Targeted.position;
            RotateBaseToFaceTarget(aimPosition);

            if (HasBarrels)
                RotateBarrelsToFaceTarget(aimPosition);
            
            AngleToTarget = GetTurretAngleToTarget(aimPosition);

            // Turret is considered "aimed" when it's pointed at the target.
            IsAimed = AngleToTarget < AimedThreshold;

            IsBarrelAtRest = false;
            IsBaseAtRest = false;
        }
    }

    void LockOn()
    {
        if (Targeted == null)
        {
            return;
        }
        Vector3 dir = Targeted.position - transform.position;
        Quaternion look_rotation = Quaternion.LookRotation(dir);
        Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
        transform.rotation = Quaternion.Euler(0f, rotation.y, 0f);
    }

    public void Shoot()
    {
        StartCoroutine(LaserBeam());
    }

    public void UpdateTarget()
    {
        Targeted = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);

        if (enemies.Length == 0)
        {
            Targeted = null;
            IsAimed = false;
            return;
        }

        float shortest_distance = Mathf.Infinity;
        GameObject nearest_enemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance_to_enemy = Vector3.Distance(transform.position, enemy.transform.position);
            Vector2 anglesToEnemy = CalcuateRelativeAngles(enemy.transform);
            if (anglesToEnemy.y > MaxElevation || anglesToEnemy.y < -MaxDepression)
            {
                continue;
            }

            if (HasLimitedTraverse)
            {
                if (anglesToEnemy.x > RightLimit || anglesToEnemy.x < -LeftLimit)
                {
                    continue;
                }
            }

            if (distance_to_enemy < ActiveRange.y && distance_to_enemy < shortest_distance)
            {
                shortest_distance = distance_to_enemy;
                nearest_enemy = enemy;
            }
        }

        if (nearest_enemy != null)
        {
            Targeted = nearest_enemy.transform;
        }
        else
        {
            IsAimed = false;
            Targeted = null;
            LaserDisable();
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
        StopCoroutine(LaserBeam());
    }

    IEnumerator LaserBeam()
    {
        LaserLineRenderer.material.SetFloat("_Active_Time", 0.0f);
        // int LaserDamageDealt = 0;
        for (float t = 0.0f; t <= LaserEffectDuration; t += Time.deltaTime)
        {
            yield return null;
            if (t > LaserEffectDuration / 2f)   // Fade out
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (t - LaserEffectDuration / 2f)));
            }
            else                                // Fade in
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(t));
            }

            if (t > 0.5f && t < LaserEffectDuration - 0.5f)
            {
                var enemyVehicle = Targeted.gameObject.GetComponent<EnemyVehicle>();
                if (enemyVehicle != null)
                {
                    enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
                }
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