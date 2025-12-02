using UnityEngine;
using System.Collections.Generic;

public class Gun : WeaponBase
{
    [Header("Ballistics")]
    [Tooltip("Time (s) between each shot.")]
    public float FireDelay = .2f;
    [Tooltip("Speed (m/s) that the bullet is fired from the barrel.")]
    public float MuzzleVelocity = 200f;
    [Tooltip("Amount of spread the gun has. Higher values result in more spread.")]
    public float Deviation = .1f;
    [Tooltip("Automatically inherit the velocity of a parent Rigidbody when firing bullets.")]
    public bool AutoInheritVelocity = true;

    [Header("Gimballing")]
    [Tooltip("When true, gun will try to gimbal towards the given target position.")]
    public bool UseGimballedAiming = false;
    [Tooltip("When true, the gun will gimbal towards the target ONLY when the target is within gimbal range.")]
    public bool GimbalOnlyWhenInRange = false;
    [Tooltip("How much the gun is allowed to gimbal. Use SetGimbal")]
    [Range(0f, 180f)] public float GimbalRange = 10f;
    [Tooltip("Position gun will try to fire bullets towards.")]
    public Vector3 GimbalTarget = Vector3.zero;

    [Header("Fire Points")]
    [Tooltip("Cycle between fire points when firing rather than firing from all at once.")]
    public bool IsSequentialFiring = false;
    public float SequentialFiringInterval = 0.1f;
    [Tooltip("Where bullets will be fired from. When left blank, this component's transform is used.")]
    [UnityEngine.Serialization.FormerlySerializedAs("Barrels")]
    [SerializeField] private List<Transform> FirePoints = new List<Transform>();

    [Header("Barrel Visuals")]
    [Tooltip("How far back (m) the barrel gets pushed back when fired.")]
    public float RecoilLength = 0.3f;
    [Tooltip("How quickly (m/s) the barrel moves back towards its resting position.")]
    public float RecoilRecoverSpeed = 1f;
    [Tooltip("The list of the Barrels used for visually recoiling Barrels. This list of Barrels should map 1:1 with fire points.")]
    [SerializeField] private List<Transform> RecoilingBarrels = new List<Transform>();

    [Header("Firing")]
    public BulletPhysics BulletPrefab;
    [SerializeField] private ParticleSystem MuzzleFlashPrefab = null;
    [Tooltip("Fire bullets from FixedUpdate. If using a physics based project, this should usually be set to true.")]
    public bool FireInFixed = true;
    [Tooltip("Set to true to fire the gun automatically.")]
    public bool IsFiring = false;

    [Header("Ammo")]
    public bool UseAmmo = false;
    public int MaxAmmo = 10000;

    // Add these fields at the top of Gun.cs with other fields
    [Header("Manual Control")]
    [Tooltip("When true, gun is in manual firing mode controlled by player")]
    public bool IsManualMode = false;

    private bool _isManualFiring = false;
    private Vector3 _manualAimPosition = Vector3.zero;
    public float LastShotTime => _lastShotTime;



    private Dictionary<Transform, ParticleSystem> firePointToMuzzleFlash = new Dictionary<Transform, ParticleSystem>();
    private List<GunBarrel> barrelVisuals = new List<GunBarrel>();

    private float _lastShotTime = -float.MaxValue;
    private int _firePointIndex = 0;

    /// <summary>
    /// Value used when firing for inherited velocity. Normally this is filled in automatically
    /// by a parent Rigidbody, but if no such rigidbody exists, this value can be manually set.
    /// </summary>
    public Vector3 InheritedVelocity { get; set; } = Vector3.zero;

    public bool ReadyToFire => Time.time - _lastShotTime >= FireDelay && HasAmmo;

    public bool HasAmmo => !UseAmmo || (UseAmmo && AmmoCount > 0);
    public int AmmoCount { get; private set; } = 10000;

    private Vector3 _targetPosLastFrame;

    private void Start()
    {
        if (Targeted != null)
        {
            _targetPosLastFrame = Targeted.position;
        }
    }

    private void Awake()
    {
        base.Awake();
        if (FirePoints.Count == 0)
        {
            // If no fire points were assigned, fall back on self as a barrel.
            RegisterFirePoint(transform);
        }
        else
        {
            foreach (var firePoint in FirePoints)
                RegisterFirePoint(firePoint);
        }

        if (RecoilingBarrels.Count > 0)
        {
            foreach (var barrel in RecoilingBarrels)
                RegisterRecoilingBarrel(barrel);
        }
    }

    private void Update()
    {
        // Handle manual vs automatic mode
        if (IsManualMode)
        {
            if (_manualAimPosition != Vector3.zero)
            {
                // Always try to rotate towards target
                RotateBaseToFaceTarget(_manualAimPosition);

                if (HasBarrels)
                    RotateBarrelsToFaceTarget(_manualAimPosition);

                GimbalTarget = _manualAimPosition;
                UseGimballedAiming = true;

                IsBarrelAtRest = false;
                IsBaseAtRest = false;
            }

            // Only fire if clicking AND target is within traverse limits
            IsFiring = _isManualFiring && IsTargetWithinTraverseLimits(_manualAimPosition);
        }
        else
        {
            IsFiring = IsAimed;

            if (IsIdle || Targeted == null)
            {
                if (!IsTurretAtRest)
                    RotateTurretToIdle();
                IsAimed = false;
            }
            else
            {
                Vector3 aimPosition = Targeted.position;
                if (GuidanceType == GlobalHelper.GuidanceType.Lead)
                {
                    // Calculate where to aim based on target movement
                    Vector3 targetVelocity = Targeted.position - _targetPosLastFrame;
                    targetVelocity /= Time.deltaTime;
                    // Figure out time to impact based on distance.          
                    float bulletSpeed = BulletPrefab.GetComponent<BulletPhysics>().Speed;
                    float distanceToTarget = Vector3.Distance(transform.position, Targeted.position);
                    float timeToImpact = distanceToTarget / bulletSpeed;
                    Vector3 futureTargetPos = Targeted.position + targetVelocity * timeToImpact;
                    aimPosition = futureTargetPos;

                    _targetPosLastFrame = Targeted.position;
                }

                RotateBaseToFaceTarget(aimPosition);

                if (HasBarrels)
                    RotateBarrelsToFaceTarget(aimPosition);

                // Turret is considered "aimed" when it's pointed at the target.
                AngleToTarget = GetTurretAngleToTarget(aimPosition);

                // Turret is considered "aimed" when it's pointed at the target.
                IsAimed = AngleToTarget < AimedThreshold;

                IsBarrelAtRest = false;
                IsBaseAtRest = false;
            }
        }

        if (!FireInFixed)
        {
            if (IsFiring)
                AttemptFireShot(InheritedVelocity);

            foreach (var barrel in barrelVisuals)
                barrel.ResetBarrelOverTime(Time.deltaTime);
        }
    }

    private void RegisterFirePoint(Transform firePoint)
    {
        if (firePoint == null)
            return;

        if (MuzzleFlashPrefab != null)
        {
            var muzzleFlash = Instantiate(MuzzleFlashPrefab, firePoint, false);
            firePointToMuzzleFlash.Add(firePoint, muzzleFlash);
        }
    }

    private void RegisterRecoilingBarrel(Transform barrel)
    {
        if (barrel == null)
            return;

        var recoilingBarrel = new GunBarrel(barrel, RecoilLength, RecoilRecoverSpeed);
        barrelVisuals.Add(recoilingBarrel);
    }

    private void FixedUpdate()
    {
        if (FireInFixed)
        {
            if (IsFiring)
                AttemptFireShot(InheritedVelocity);

            foreach (var barrel in barrelVisuals)
                barrel.ResetBarrelOverTime(Time.deltaTime);
        }
    }

    /// <summary>
    /// Restores the ammo count to <see cref="MaxAmmo"/>.
    /// </summary>
    public void ReloadAmmo()
    {
        AmmoCount = MaxAmmo;
    }

    /// <summary>
    /// Directly set the ammo count.
    /// </summary>
    /// <remarks>This ignores the maximum ammo count setting, allowing for the weapon to
    /// be filled with more ammo than <see cref="MaxAmmo"/></remarks>
    public void SetAmmo(int ammo)
    {
        AmmoCount = ammo;
    }


    /// <summary>
    /// Fire a single shot. Automatically takes into account inherited velocity.
    /// </summary>
    /// <remarks>For automatic fire, use <see cref="IsFiring"/>.</remarks>
    /// <returns><see langword="true"/> if the shot was fired successfully.</returns>
    public bool FireSingleShot()
    {
        return AttemptFireShot(InheritedVelocity);
    }

    private bool AttemptFireShot(Vector3 inheritedVelocity)
    {
        if (!ReadyToFire)
            return false;

        if (IsSequentialFiring)
        {
            // Cycle between all the fire points.
            var firePoint = FirePoints[_firePointIndex % FirePoints.Count];
            FireBulletFromFirePoint(firePoint, inheritedVelocity);
            _firePointIndex += 1;

            AmmoCount -= 1;
            // If use sequential firing, add a small delay between each of the shots. Then use the main delay after all Barrels have fired.
            if (_firePointIndex % FirePoints.Count == 0)
            {
                _lastShotTime = Time.time;
            }
            else
            {
                _lastShotTime = Time.time - FireDelay + SequentialFiringInterval;
            }
        }
        else
        {
            // Fire from all Barrels at once.
            foreach (var firePoint in FirePoints)
            {
                FireBulletFromFirePoint(firePoint, inheritedVelocity);
                _firePointIndex += 1;

                AmmoCount -= 1;
            }
            _lastShotTime = Time.time;
        }

        return true;
    }

    private void FireBulletFromFirePoint(Transform firePoint, Vector3 velocity)
    {
        var bullet = Instantiate(BulletPrefab, firePoint.transform.position, firePoint.transform.rotation);
        bullet.GetComponent<BulletPhysics>().FireTarget = FireTarget;

        var bulletRotation = firePoint.transform.rotation;

        var isGimballingAllowed = UseGimballedAiming;
        if (isGimballingAllowed && GimbalOnlyWhenInRange)
        {
            var angleToTarget = Vector3.Angle(
                from: GimbalTarget - firePoint.position,
                to: firePoint.forward);

            isGimballingAllowed = angleToTarget < GimbalRange;
        }

        if (isGimballingAllowed)
        {
            bulletRotation = Quaternion.RotateTowards(
                from: bulletRotation,
                to: Quaternion.LookRotation(GimbalTarget - firePoint.position, firePoint.up),
                maxDegreesDelta: GimbalRange);
        }

        if (barrelVisuals.Count > 0)
            barrelVisuals[_firePointIndex % barrelVisuals.Count].FireRecoil();

        if (firePointToMuzzleFlash.ContainsKey(firePoint))
            firePointToMuzzleFlash[firePoint].Play();
    }

    public override void ManagedUpdateTarget()
    {
        base.ManagedUpdateTarget();

        if (Targeted == null)
            IsFiring = false;
    }

    /// <summary>
    /// Called by GunController to set manual firing state
    /// </summary>
    public void SetManualFiring(bool isFiring, Vector3 aimPosition)
    {
        IsManualMode = true;
        _isManualFiring = isFiring;
        _manualAimPosition = aimPosition;
    }

    /// <summary>
    /// Switch back to automatic mode
    /// </summary>
    public void SetAutomaticMode()
    {
        IsManualMode = false;
        _isManualFiring = false;
    }

    /// <summary>
    /// Check if target position is within the turret's traverse and elevation limits
    /// </summary>
    public bool IsTargetWithinTraverseLimits(Vector3 targetPosition)
    {
        // Calculate direction to target
        Vector3 vecToTarget = targetPosition - TurretBase.position;
        Vector3 flattenedVecForBase = Vector3.ProjectOnPlane(vecToTarget, transform.up);

        // Calculate azimuth (horizontal angle)
        float azimuth = Vector3.SignedAngle(transform.forward, flattenedVecForBase, transform.up);

        // Check horizontal limits
        if (HasLimitedTraverse)
        {
            if (azimuth < -LeftLimit || azimuth > RightLimit)
                return false;
        }

        // Calculate elevation (vertical angle)
        if (HasBarrels && Barrels != null)
        {
            Vector3 localTargetPos = TurretBase.InverseTransformDirection(targetPosition - Barrels.position);
            Vector3 flattenedVecForBarrels = Vector3.ProjectOnPlane(localTargetPos, Vector3.up);

            float elevation = Vector3.Angle(flattenedVecForBarrels, localTargetPos);
            elevation *= Mathf.Sign(localTargetPos.y);

            // Check vertical limits
            if (elevation > MaxElevation || elevation < -MaxDepression)
                return false;
        }

        return true;
    }
}