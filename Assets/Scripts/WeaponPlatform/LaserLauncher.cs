using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GlobalHelper;
using UnityEngine.VFX;

public class LaserLauncher : WeaponBase
{
    [Header("Laser Turret Settings")]
    [Tooltip("Maximum thickness when the laser is at full power.")]
    public float MaxThickness = 1.0f;

    [Header("Laser Settings")]
    [Tooltip("Duration of the laser effect in seconds.")]
    public float LaserEffectDuration = 2.5f;
    public float MinimumLaserDamageInterval = 0.1f;
    public float FireInterval = 2.0f;
    public int LaserDPS = 1;
    public bool IsFiring = false;
    public Transform LaserOrigin;
    public VisualEffect LaserVFX;

    private float _laserDurationTimer = 0.0f;
    private float _laserDamageTimer = 0f;
    private float _fireCooldownTimer = 0f;
    private bool _isOnCooldown = false;

    private RaycastHit _hit;
    private bool _laserVFXPlaying = false;

    public bool ReadyToFire => !_isOnCooldown;

    public AmmoType LaserType = AmmoType.Energy;

    void Start()
    {
        // Instantiate Laser VFX at world origin (not parented, since positions are set in world space)
        if (LaserVFX != null)
        {
            LaserVFX = Instantiate(LaserVFX);
            LaserVFX.transform.position = Vector3.zero;
            LaserVFX.transform.rotation = Quaternion.identity;
            LaserVFX.transform.localScale = Vector3.one;
        }
    }

    void Update()
    {
        if (!IsFunctional)
        {
            _laserDurationTimer = LaserEffectDuration;
            _isOnCooldown = true;
            _fireCooldownTimer = 0f;
            if (LaserVFX != null) { LaserVFX.Stop(); _laserVFXPlaying = false; }
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
            if (LaserVFX != null && !_laserVFXPlaying)
            {
                LaserVFX.Play();
                _laserVFXPlaying = true;
            }
            Shoot();
        }
        else if (Targeted == null && _laserDurationTimer > 0.0f)
        {
            // Fade out laser when target lost mid-fire
            _laserDurationTimer += Time.deltaTime;
            float fade = Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)) * MaxThickness;
            if (LaserVFX != null) LaserVFX.SetFloat("Fade", fade);

            // Reset if fade complete
            if (_laserDurationTimer >= LaserEffectDuration)
            {
                _laserDurationTimer = 0f;
                if (LaserVFX != null) { LaserVFX.SetFloat("Fade", 0f); LaserVFX.Stop(); _laserVFXPlaying = false; }
            }
        }
        else
        {
            if (LaserVFX != null) { LaserVFX.SetFloat("Fade", 0f); LaserVFX.Stop(); _laserVFXPlaying = false; }
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

    void LateUpdate()
    {
        // Always update beam positions while VFX is playing, not just when firing
        if (_laserVFXPlaying && LaserVFX != null)
        {
            Vector3 startPos = LaserOrigin.position;
            Vector3 endPosition;

            if (Targeted != null)
            {
                // Use barrel forward direction, not direction-to-target, so beam follows actual aim
                Vector3 fireDir = LaserOrigin.forward;
                var targetVehicle = Targeted.GetComponent<VehicleBase>();

                if (targetVehicle != null)
                {
                    if (targetVehicle.RaycastBounds(startPos, fireDir, out Vector3 hitPt, out float hitDist))
                    {
                        endPosition = hitPt;
                    }
                    else
                    {
                        // Beam missed OBB — project to target distance along barrel
                        float dist = Vector3.Distance(startPos, Targeted.position);
                        endPosition = startPos + fireDir * dist;
                    }
                }
                else
                {
                    endPosition = Targeted.position;
                }
            }
            else
            {
                // Target lost — extend beam forward along barrel a fixed distance
                endPosition = startPos + LaserOrigin.forward * 100f;
            }

            LaserVFX.SetVector3("StartPosition", startPos);
            LaserVFX.SetVector3("EndPosition", endPosition);

            float distance = Vector3.Distance(startPos, endPosition);
            LaserVFX.SetVector2("NoiseUVScale", new Vector2(
                1.0f,
                Mathf.Max(1f, distance)
            ));
        }
    }

    public void Shoot()
    {

        if (_laserDurationTimer < LaserEffectDuration)
        {
            _laserDurationTimer += Time.deltaTime;
            _laserDamageTimer += Time.deltaTime;

            // Fade in/out effect via VFX Fade
            float fade;
            if (_laserDurationTimer > LaserEffectDuration / 2f)
                fade = Mathf.Clamp01(LaserEffectDuration / 2f - (_laserDurationTimer - LaserEffectDuration / 2f)) * MaxThickness;
            else
                fade = Mathf.Clamp01(_laserDurationTimer) * MaxThickness;
            if (LaserVFX != null) LaserVFX.SetFloat("Fade", fade);

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

                Vector3 impactPoint = enemyVehicle.ClosestBoundsPoint(LaserOrigin.position);

                if (enemyVehicle.ShieldPoints > 0)
                {
                    Vector3 dir = (impactPoint - transform.position).normalized;
                    Physics.Raycast(transform.position, dir, out _hit);

                    if (_hit.collider != null && _hit.collider.GetComponent<ShieldHitEffect>())
                    {
                        _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
                    }
                }
                enemyVehicle.TakeDamageAtPoint(LaserDPS, LaserType, impactPoint);
            }
        }
        else
        {
            // Laser duration complete - start cooldown
            _laserDurationTimer = 0f;
            _isOnCooldown = true;
            _fireCooldownTimer = 0f;
            if (LaserVFX != null) { LaserVFX.SetFloat("Fade", 0f); LaserVFX.Stop(); _laserVFXPlaying = false; }
        }
    }

    private void OnDestroy()
    {
        if (LaserVFX != null)
            Destroy(LaserVFX.gameObject);
    }
}