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
    public float MinimumLaserDamageInterval = 0.1f;
    public float TurretRotateSpeed = 5f;
    public int LaserDamageCap = 10;
    public int LaserDPS = 1;
    public bool IsFiring = false;
    public Transform LaserOrigin;

    private float _laserDurationTimer = 0.0f;
    private float _laserDamageTimer = 0f;
    void Start()
    {
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        LaserLineRenderer.SetPosition(0, LaserOrigin.position);

        if (Targeted == null)
        {
            IsFiring = false;
        }
    }
    void Update()
    {
        if (!IsAimed)
            IsFiring = false;
        else
            IsFiring = true;

        if (IsFiring && Targeted != null)
        {
            LaserLineRenderer.enabled = true;
            Shoot();
        }
        else if(Targeted == null && _laserDurationTimer > 0.0f)
        {
            _laserDurationTimer += Time.deltaTime;
            LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));
        }
        else
        {
            LaserLineRenderer.enabled = false;
        }

        // Rotate turret logic
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

    public void Shoot()
    {
        LaserLineRenderer.SetPosition(0, LaserOrigin.position);
        LaserLineRenderer.SetPosition(1, Targeted.position);

        if (_laserDurationTimer < LaserEffectDuration)
        {
            _laserDurationTimer += Time.deltaTime;
            _laserDamageTimer += Time.deltaTime;
            if (_laserDurationTimer > LaserEffectDuration / 2f)   // Fade out
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));
            }
            else                                // Fade in
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(_laserDurationTimer));
            }

            if (_laserDurationTimer > 0.2f && _laserDurationTimer < LaserEffectDuration - 0.2f && _laserDamageTimer >= MinimumLaserDamageInterval)
            {
                _laserDamageTimer = 0f;
                var enemyVehicle = Targeted.gameObject.GetComponent<EnemyVehicle>();
                if (enemyVehicle != null)
                {
                    enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
                }
            }
        }
        else
        {
            _laserDurationTimer = 0.0f;
        }

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
        }
    }
}