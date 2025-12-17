using UnityEngine;
using System.Collections.Generic;

public class Gun : WeaponBase
{
    [Header("Ballistics")]
    [Tooltip("Time (s) between each shot.")]
    [SerializeField] private float _fireDelay = .2f;
    [Tooltip("Speed (m/s) that the bullet is fired from the barrel.")]
    public float MuzzleVelocity = 200f;
    [Tooltip("Time (s) after which the bullet will self-destruct.")]
    public float SelfDestructionTime = 10f;
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

    [Header("Manual Control")]
    [Tooltip("When true, gun is in manual firing mode controlled by player")]
    public bool IsManualMode = false;

    public float FireDelay { get { return _fireDelay / Mathf.Clamp(Effectiveness, 0.1f, 1f); } set { _fireDelay = value; } }
    public float LastShotTime => _lastShotTime;

    // private Vector3 _smoothedTargetVelocity = Vector3.zero;
    // private bool _hasLastFramePos = false;
    // private const float TargetVelocitySmoothSpeed = 8f;

    private bool _isManualFiring = false;
    private Vector3 _manualAimPosition = Vector3.zero;
    private bool _isSequentialBurstActive = false;
    private int _sequentialBurstShotsRemaining = 0;
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
    public Vector3 ManualAimPosition => _manualAimPosition;

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
        if (!IsFunctional)
            return;
        // Auto-populate inherited velocity from parent rigidbody or ship movement
        if (AutoInheritVelocity)
        {
            UpdateInheritedVelocity();
        }
        // Handle manual vs automatic mode
        if (IsManualMode)
        {
            if (_manualAimPosition != Vector3.zero)
            {
                RotateBaseToFaceTarget(_manualAimPosition);

                if (HasBarrels)
                    RotateBarrelsToFaceTarget(_manualAimPosition);

                GimbalTarget = _manualAimPosition;
                UseGimballedAiming = true;

                IsBarrelAtRest = false;
                IsBaseAtRest = false;
            }

            bool withinLimits = IsTargetWithinTraverseLimits(_manualAimPosition);

            if (IsSequentialFiring)
            {
                // Start a new burst when clicking AND not already in a burst
                if (_isManualFiring && withinLimits && !_isSequentialBurstActive && ReadyToFire)
                {
                    _isSequentialBurstActive = true;
                    _sequentialBurstShotsRemaining = FirePoints.Count;
                }

                // Continue firing if burst is active
                IsFiring = _isSequentialBurstActive && withinLimits;
            }
            else
            {
                // Non-sequential: fire while holding
                IsFiring = _isManualFiring && withinLimits;
            }
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
                    Vector3 targetVelocity = LeadCalculator.GetTargetVelocity(Targeted);
                    Vector3 shipVelocity = AutoInheritVelocity ? InheritedVelocity : Vector3.zero;

                    Vector3 interceptPoint = LeadCalculator.CalculateInterceptPoint(
                        transform.position,
                        shipVelocity,
                        MuzzleVelocity,
                        Targeted.position,
                        targetVelocity,
                        5f
                    );

                    if (interceptPoint != Vector3.zero)
                    {
                        aimPosition = interceptPoint;
                    }
                    else
                    {
                        aimPosition = LeadCalculator.CalculateSimpleLead(
                            transform.position,
                            Targeted.position,
                            targetVelocity,
                            MuzzleVelocity,
                            5f
                        );
                    }
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

            // Decrement burst counter in manual mode
            if (IsManualMode && _isSequentialBurstActive)
            {
                _sequentialBurstShotsRemaining -= 1;
                if (_sequentialBurstShotsRemaining <= 0)
                {
                    _isSequentialBurstActive = false;
                }
            }

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
        var bulletPhysics = bullet.GetComponent<BulletPhysics>();
        bulletPhysics.FireTarget = FireTarget;

        // Start with the fire point's forward direction
        Vector3 fireDirection = firePoint.forward;

        // Apply gimballing if enabled
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
            // Calculate gimballed direction
            Vector3 toTarget = (GimbalTarget - firePoint.position).normalized;
            Quaternion gimballed = Quaternion.RotateTowards(
                from: Quaternion.LookRotation(fireDirection),
                to: Quaternion.LookRotation(toTarget, firePoint.up),
                maxDegreesDelta: GimbalRange);
            fireDirection = gimballed * Vector3.forward;
        }

        // Apply deviation (spread)
        if (Deviation > 0f)
        {
            Vector3 randomSpread = new Vector3(
                Random.Range(-Deviation, Deviation),
                Random.Range(-Deviation, Deviation),
                0f
            );
            fireDirection = Quaternion.Euler(randomSpread) * fireDirection;
        }

        // Override bullet speed with gun's muzzle velocity
        bulletPhysics.Speed = MuzzleVelocity;
        bulletPhysics.LifeTime = SelfDestructionTime;

        // Initialize bullet with direction and inherited velocity
        if (AutoInheritVelocity)
        {
            bulletPhysics.Initialize(fireDirection, velocity);
        }
        else
        {
            bulletPhysics.Initialize(fireDirection, Vector3.zero);
        }

        // Visual feedback
        if (barrelVisuals.Count > 0)
            barrelVisuals[_firePointIndex % barrelVisuals.Count].FireRecoil();

        if (firePointToMuzzleFlash.ContainsKey(firePoint))
            firePointToMuzzleFlash[firePoint].Play();
    }

    public override void ManagedUpdateTarget()
    {
        if (IsManualMode)
            return;

        // Check for missiles first if enabled
        if (CanTargetMissiles)
        {
            AAMissile nearestMissile = CombatRegistry.FindNearestHostileMissile(
                transform.position,
                MissileDetectionRange,
                GetOwnerFaction()
            );

            if (nearestMissile != null)
            {
                if (PrioritizeMissiles || Targeted == null)
                {
                    Targeted = nearestMissile.transform;
                    return;
                }
            }
        }

        // Fall back to normal vehicle targeting
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
        _isSequentialBurstActive = false;
        _sequentialBurstShotsRemaining = 0;
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

    private void UpdateInheritedVelocity()
    {

        PlayerShipMovement shipMovement = GetComponentInParent<PlayerShipMovement>();
        if (shipMovement != null)
        {
            InheritedVelocity = shipMovement.Velocity;
            return;
        }

        InheritedVelocity = Vector3.zero;
    }

    /// <summary>
    /// Calculates the intercept point for a projectile to hit a moving target.
    /// Accounts for shooter velocity (bullet inherits shooter momentum).
    /// </summary>
    private Vector3 CalculateInterceptPoint(
        Vector3 shooterPos,
        Vector3 shooterVelocity,
        float projectileSpeed,
        Vector3 targetPos,
        Vector3 targetVelocity)
    {
        // Relative position and velocity
        Vector3 relativePos = targetPos - shooterPos;
        Vector3 relativeVel = targetVelocity - shooterVelocity;

        // Quadratic equation coefficients: at² + bt + c = 0
        float a = Vector3.Dot(relativeVel, relativeVel) - (projectileSpeed * projectileSpeed);
        float b = 2f * Vector3.Dot(relativePos, relativeVel);
        float c = Vector3.Dot(relativePos, relativePos);

        // Handle case where a is nearly zero (relative velocity matches projectile speed)
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f)
                return Vector3.zero;

            float lt = -c / b;
            if (lt > 0f)
                return targetPos + targetVelocity * lt;
            return Vector3.zero;
        }

        float discriminant = b * b - 4f * a * c;

        // No real solution means target is unreachable
        if (discriminant < 0f)
            return Vector3.zero;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDiscriminant) / (2f * a);
        float t2 = (-b - sqrtDiscriminant) / (2f * a);

        // Choose the smallest positive time
        float t;
        if (t1 > 0f && t2 > 0f)
            t = Mathf.Min(t1, t2);
        else if (t1 > 0f)
            t = t1;
        else if (t2 > 0f)
            t = t2;
        else
            return Vector3.zero;

        // Cap prediction time to reasonable value
        t = Mathf.Min(t, 5f);

        return targetPos + targetVelocity * t;
    }
}