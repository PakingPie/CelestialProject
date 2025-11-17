using UnityEngine;
using System.Collections.Generic;

public class Gun : MonoBehaviour
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
    [Tooltip("Where bullets will be fired from. When left blank, this component's transform is used.")]
    [UnityEngine.Serialization.FormerlySerializedAs("Barrels")]
    [SerializeField] private List<Transform> FirePoints = new List<Transform>();

    [Header("Barrel Visuals")]
    [Tooltip("How far back (m) the barrel gets pushed back when fired.")]
    public float RecoilLength = 0.3f;
    [Tooltip("How quickly (m/s) the barrel moves back towards its resting position.")]
    public float RecoilRecoverSpeed = 1f;
    [Tooltip("The list of the barrels used for visually recoiling barrels. This list of barrels should map 1:1 with fire points.")]
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

    private Dictionary<Transform, ParticleSystem> firePointToMuzzleFlash = new Dictionary<Transform, ParticleSystem>();
    private List<GunBarrel> barrelVisuals = new List<GunBarrel>();

    private float lastShotTime = -float.MaxValue;
    private int firePointIndex = 0;

    /// <summary>
    /// Value used when firing for inherited velocity. Normally this is filled in automatically
    /// by a parent Rigidbody, but if no such rigidbody exists, this value can be manually set.
    /// </summary>
    public Vector3 InheritedVelocity { get; set; } = Vector3.zero;

    public bool ReadyToFire => Time.time - lastShotTime >= FireDelay && HasAmmo;

    public bool HasAmmo => !UseAmmo || (UseAmmo && AmmoCount > 0);
    public int AmmoCount { get; private set; } = 10000;

    [Header("Targeting")]
    public Transform Targeted;
    public GlobalHelper.Faction FireTarget = GlobalHelper.Faction.Foe;
    public Vector2 ActiveRange = new Vector2(5f, 1000f);
    public int UpdateRate = 60;
    public float TurretRotateSpeed = 5f;
    public GlobalHelper.GuidanceType GuidanceType = GlobalHelper.GuidanceType.Lead;

    [Header("Turret")]
    [Tooltip("Transform of the turret's azimuthal rotations.")]
    [SerializeField] private Transform turretBase = null;
    [Tooltip("Transform of the turret's elevation rotations. ")]
    [SerializeField] private Transform barrels = null;
    [Tooltip("Speed at which the turret's guns elevate up and down.")]
    public float ElevationSpeed = 30f;
    [Tooltip("Highest upwards elevation the turret's barrels can aim.")]
    public float MaxElevation = 60f;
    [Tooltip("Lowest downwards elevation the turret's barrels can aim.")]
    public float MaxDepression = 5f;
    [Tooltip("Speed at which the turret can rotate left/right.")]
    public float TraverseSpeed = 60f;
    [Tooltip("When true, the turret can only rotate horizontally with the given limits.")]
    [SerializeField] private bool hasLimitedTraverse = false;
    [Range(0, 179)] public float LeftLimit = 120f;
    [Range(0, 179)] public float RightLimit = 120f;
    [Tooltip("When idle, the turret does not aim at anything and simply points forwards.")]
    public bool IsIdle = false;
    [Tooltip("Position the turret will aim at when not idle. Set this to whatever you want" +
        "the turret to actively aim at.")]
    public Vector3 AimPosition = Vector3.zero;
    [Tooltip("When the turret is within this many degrees of the target, it is considered aimed.")]
    [SerializeField] private float aimedThreshold = 5f;
    private float limitedTraverseAngle = 0f;
    private float angleToTarget = 0f;
    private float elevation = 0f;

    private bool hasBarrels = false;

    private bool isAimed = false;
    private bool isBaseAtRest = false;
    private bool isBarrelAtRest = false;


    private Vector3 _targetPosLastFrame;

    private void Start()
    {
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        // InvokeRepeating("LockOn", 0f, 1.0f / UpdateRate);
        if (Targeted != null)
        {
            _targetPosLastFrame = Targeted.position;
        }
    }

    private void Awake()
    {
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

    private void Update()
    {
        if (!FireInFixed)
        {
            if (IsFiring)
                AttemptFireShot(InheritedVelocity);

            foreach (var barrel in barrelVisuals)
                barrel.ResetBarrelOverTime(Time.deltaTime);
        }

        if (Targeted != null)
        {
            RotateBarrelsToFaceTarget(Targeted.position);
            RotateBaseToFaceTarget(Targeted.position);
        }
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
            var firePoint = FirePoints[firePointIndex % FirePoints.Count];
            FireBulletFromFirePoint(firePoint, inheritedVelocity);
            firePointIndex += 1;

            AmmoCount -= 1;
        }
        else
        {
            // Fire from all barrels at once.
            foreach (var firePoint in FirePoints)
            {
                FireBulletFromFirePoint(firePoint, inheritedVelocity);
                firePointIndex += 1;

                AmmoCount -= 1;
            }
        }

        lastShotTime = Time.time;
        return true;
    }

    private void FireBulletFromFirePoint(Transform firePoint, Vector3 velocity)
    {
        var bullet = Instantiate(BulletPrefab, firePoint.transform.position, firePoint.transform.rotation);

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
            barrelVisuals[firePointIndex % barrelVisuals.Count].FireRecoil();

        if (firePointToMuzzleFlash.ContainsKey(firePoint))
            firePointToMuzzleFlash[firePoint].Play();
    }

    void LockOn()
    {
        if (Targeted == null)
        {
            return;
        }
        if (GuidanceType == GlobalHelper.GuidanceType.Pursuit)
        {
            Vector3 dir = Targeted.position - transform.position;
            Quaternion look_rotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
            transform.rotation = Quaternion.Euler(rotation);
        }
        else
        {
            // Get where target will be in one second.
            Vector3 targetVelocity = Targeted.position - _targetPosLastFrame;
            targetVelocity /= 1;
            //=====================================================

            // Figure out time to impact based on distance.          
            float bulletSpeed = BulletPrefab.GetComponent<BulletPhysics>().Speed;
            float distanceToTarget = Vector3.Distance(transform.position, Targeted.position);
            float timeToImpact = distanceToTarget / bulletSpeed;
            Vector3 futureTargetPos = Targeted.position + targetVelocity * timeToImpact;
            Vector3 dir = futureTargetPos - transform.position;
            Quaternion look_rotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
            transform.rotation = Quaternion.Euler(rotation);
        }
    }

    public void UpdateTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);

        if (enemies.Length == 0)
        {
            Targeted = null;
            IsFiring = false;
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
            Targeted = nearest_enemy.transform;
            IsFiring = true;
        }
        else
        {
            Targeted = null;
            IsFiring = false;
        }
    }

    private void RotateBarrelsToFaceTarget(Vector3 targetPosition)
    {
        Vector3 localTargetPos = turretBase.InverseTransformDirection(targetPosition - barrels.position);
        Vector3 flattenedVecForBarrels = Vector3.ProjectOnPlane(localTargetPos, Vector3.up);

        float targetElevation = Vector3.Angle(flattenedVecForBarrels, localTargetPos);
        targetElevation *= Mathf.Sign(localTargetPos.y);

        targetElevation = Mathf.Clamp(targetElevation, -MaxDepression, MaxElevation);
        elevation = Mathf.MoveTowards(elevation, targetElevation, ElevationSpeed * Time.deltaTime);

        if (Mathf.Abs(elevation) > Mathf.Epsilon)
            barrels.localEulerAngles = Vector3.right * -elevation;
    }

    private void RotateBaseToFaceTarget(Vector3 targetPosition)
    {
        Vector3 turretUp = transform.up;

        Vector3 vecToTarget = targetPosition - turretBase.position;
        Vector3 flattenedVecForBase = Vector3.ProjectOnPlane(vecToTarget, turretUp);

        if (hasLimitedTraverse)
        {
            Vector3 turretForward = transform.forward;
            float targetTraverse = Vector3.SignedAngle(turretForward, flattenedVecForBase, turretUp);

            targetTraverse = Mathf.Clamp(targetTraverse, -LeftLimit, RightLimit);
            limitedTraverseAngle = Mathf.MoveTowards(
                limitedTraverseAngle,
                targetTraverse,
                TraverseSpeed * Time.deltaTime);

            if (Mathf.Abs(limitedTraverseAngle) > Mathf.Epsilon)
                turretBase.localEulerAngles = Vector3.up * limitedTraverseAngle;
        }
        else
        {
            turretBase.rotation = Quaternion.RotateTowards(
                Quaternion.LookRotation(turretBase.forward, turretUp),
                Quaternion.LookRotation(flattenedVecForBase, turretUp),
                TraverseSpeed * Time.deltaTime);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (turretBase != null)
        {
            const float kArcSize = 10f;
            Color colorTraverse = new Color(1f, .5f, .5f, .1f);
            Color colorElevation = new Color(.5f, 1f, .5f, .1f);
            Color colorDepression = new Color(.5f, .5f, 1f, .1f);

            Transform arcRoot = barrels != null ? barrels : turretBase;

            // Red traverse arc
            UnityEditor.Handles.color = colorTraverse;
            if (hasLimitedTraverse)
            {
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, turretBase.up,
                    transform.forward, RightLimit,
                    kArcSize);
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, turretBase.up,
                    transform.forward, -LeftLimit,
                    kArcSize);
            }
            else
            {
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, turretBase.up,
                    transform.forward, 360f,
                    kArcSize);
            }

            if (barrels != null)
            {
                // Green elevation arc
                UnityEditor.Handles.color = colorElevation;
                UnityEditor.Handles.DrawSolidArc(
                    barrels.position, barrels.right,
                    turretBase.forward, -MaxElevation,
                    kArcSize);

                // Blue depression arc
                UnityEditor.Handles.color = colorDepression;
                UnityEditor.Handles.DrawSolidArc(
                    barrels.position, barrels.right,
                    turretBase.forward, MaxDepression,
                    kArcSize);
            }
        }
    }
}

public class GunBarrel
{
    public float RecoilLength = 0.3f;
    public float RecoverSpeed = 1f;

    private Transform barrel = null;
    private Vector3 startLocalPosition = Vector3.zero;
    private float recoil = 0f;

    public GunBarrel(Transform barrel, float recoilLength, float recoverSpeed)
    {
        this.barrel = barrel;
        RecoilLength = recoilLength;
        RecoverSpeed = recoverSpeed;
        startLocalPosition = this.barrel.localPosition;
    }

    public void FireRecoil()
    {
        recoil = RecoilLength;
    }

    public void ResetBarrelOverTime(float deltaTime)
    {
        recoil = Mathf.MoveTowards(recoil, 0f, RecoverSpeed * deltaTime);

        // This means that when a barrel is fully reset it'll never be EXACTLY
        // back at where it started, but this distance should be small enough
        // that hopefully it won't be noticeable.
        if (recoil > 0f)
            barrel.transform.localPosition = startLocalPosition + (Vector3.back * recoil);
    }
}