using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static GlobalHelper;
using UnityEngine.VFX;
// [ExecuteInEditMode]
public class LaserLauncher : WeaponBase
{
    [Header("Laser Turret Settings")]
    public LineRenderer LaserLineRenderer;
    [Header("Laser Settings")]

    [Tooltip("Duration of the laser effect in seconds.")]
    public float LaserEffectDuration = 2.5f;
    public float MinimumLaserDamageInterval = 0.1f;
    public float FireInterval = 2.0f;
    public float TurretRotateSpeed = 5f;
    public int LaserDamageCap = 10;
    public int LaserDPS = 1;
    public bool IsFiring = false;
    public Transform LaserOrigin;
    public VisualEffect LaserLaunchEffect;
    private float _laserDurationTimer = 0.0f;
    private float _laserDamageTimer = 0f;

    private RaycastHit _hit;
    void Start()
    {
        LaserLineRenderer.SetPosition(0, LaserOrigin.position);
    }
    void Update()
    {
        // base.Update();
        IsFiring = IsAimed;

        if (IsFiring && Targeted != null)
        {
            LaserLineRenderer.enabled = true;
            LaserLaunchEffect.transform.position = LaserOrigin.position;
            LaserLaunchEffect.Play();
            Shoot();
        }
        else if (Targeted == null && _laserDurationTimer > 0.0f)
        {
            _laserDurationTimer += Time.deltaTime;
            LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));
        }
        else
        {
            LaserLineRenderer.enabled = false;
            LaserLaunchEffect.Stop();
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
            if (_laserDurationTimer > LaserEffectDuration / 2f) // Fade out
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));
            }
            else // Fade in
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(_laserDurationTimer));
            }

            var enemyVehicle = Targeted.gameObject.GetComponent<VehicleBase>();
            if (enemyVehicle == null)
                return;

            if (_laserDurationTimer > 0.2f && _laserDurationTimer < LaserEffectDuration - 0.2f && _laserDamageTimer >= MinimumLaserDamageInterval)
            {
                _laserDamageTimer = 0f;
                if (enemyVehicle != null)
                {
                    if (enemyVehicle.ShieldPoints > 0)
                    {
                        Vector3 dir = (enemyVehicle.transform.position - transform.position).normalized;
                        Physics.Raycast(transform.position, dir, out _hit);

                        if (_hit.collider != null && _hit.collider.GetComponent<ShieldHitEffect>())
                        {
                            _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
                        }
                    }
                    enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
                }
            }

            // Vector3 dir = (enemyVehicle.transform.position - transform.position).normalized;
            // Physics.Raycast(transform.position, dir, out _hit);

            // if (enemyVehicle.ShieldPoints > 0 && _hit.collider != null && _hit.collider.GetComponent<ShieldHitEffect>())
            // {
            //     LaserLineRenderer.SetPosition(1, _hit.point);
            //     if (_laserDurationTimer > 0.2f && _laserDurationTimer < LaserEffectDuration - 0.2f && _laserDamageTimer >= MinimumLaserDamageInterval)
            //     {
            //         _laserDamageTimer = 0f;
            //         if (enemyVehicle != null)
            //         {
            //             _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
            //             enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
            //         }
            //     }
            // }
            // else
            // {
            //     LaserLineRenderer.SetPosition(1, Targeted.position);
            //     if (_laserDurationTimer > 0.2f && _laserDurationTimer < LaserEffectDuration - 0.2f && _laserDamageTimer >= MinimumLaserDamageInterval)
            //     {
            //         _laserDamageTimer = 0f;
            //         if (enemyVehicle != null)
            //         {

            //             enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
            //         }
            //     }
            // }
        }
        else
        {
            _laserDurationTimer = 0.0f;
        }

    }
}