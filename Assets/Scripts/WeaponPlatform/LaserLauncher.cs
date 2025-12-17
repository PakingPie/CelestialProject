using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GlobalHelper;
using UnityEngine.VFX;

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
    private float _fireCooldownTimer = 0f;
    private bool _isOnCooldown = false;

    private RaycastHit _hit;

    public bool ReadyToFire => !_isOnCooldown;

    void Start()
    {
        LaserLineRenderer.SetPosition(0, LaserOrigin.position);
        // Instantiate Laser Launch Effect
        LaserLaunchEffect = Instantiate(LaserLaunchEffect, LaserOrigin.position, Quaternion.identity, LaserOrigin);
    }

    void Update()
    {
        if (!IsFunctional)
        {
            _laserDurationTimer = LaserEffectDuration;
            _isOnCooldown = true;
            _fireCooldownTimer = 0f;
            LaserLaunchEffect.Stop();
            return;
        }

        // Handle cooldown
        if (_isOnCooldown)
        {
            _fireCooldownTimer += Time.deltaTime;
            if (_fireCooldownTimer >= FireInterval)
            {
                _isOnCooldown = false;
                _fireCooldownTimer = 0f;
            }
        }

        IsFiring = IsAimed && ReadyToFire;

        if (IsFiring && Targeted != null)
        {
            LaserLineRenderer.enabled = true;
            LaserLaunchEffect.transform.position = LaserOrigin.position;
            LaserLaunchEffect.Play();
            Shoot();
        }
        else if (Targeted == null && _laserDurationTimer > 0.0f)
        {
            // Fade out laser when target lost mid-fire
            _laserDurationTimer += Time.deltaTime;
            LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));

            // Reset if fade complete
            if (_laserDurationTimer >= LaserEffectDuration)
            {
                _laserDurationTimer = 0f;
                LaserLineRenderer.enabled = false;
                LaserLaunchEffect.Stop();
            }
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

            // Fade in/out effect
            if (_laserDurationTimer > LaserEffectDuration / 2f)
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)));
            }
            else
            {
                LaserLineRenderer.material.SetFloat("_Active_Time", Mathf.Clamp01(_laserDurationTimer));
            }

            // Deal damage
            var enemyVehicle = Targeted.gameObject.GetComponent<VehicleBase>();
            if (enemyVehicle == null)
            {
                // if entity is destroyed or missing VehicleBase, stop firing
                _laserDurationTimer = LaserEffectDuration;
                _isOnCooldown = true;
                _fireCooldownTimer = 0f;
                return;
            }

            if (_laserDurationTimer > 0.2f && _laserDurationTimer < LaserEffectDuration - 0.2f && _laserDamageTimer >= MinimumLaserDamageInterval)
            {
                _laserDamageTimer = 0f;

                if (enemyVehicle.ShieldPoints > 0)
                {
                    var ownerShip = enemyVehicle.OwnerShip.GetComponent<VehicleBase>();
                    Vector3 dir = (ownerShip.transform.position - transform.position).normalized;
                    Physics.Raycast(transform.position, dir, out _hit);

                    if (_hit.collider != null && _hit.collider.GetComponent<ShieldHitEffect>())
                    {
                        _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
                    }
                }
                enemyVehicle.TakeDamage(LaserDPS, AmmoType.Energy);
            }
        }
        else
        {
            // Laser duration complete - start cooldown
            _laserDurationTimer = 0f;
            _isOnCooldown = true;
            _fireCooldownTimer = 0f;
            LaserLineRenderer.enabled = false;
            LaserLaunchEffect.Stop();
        }
    }
}