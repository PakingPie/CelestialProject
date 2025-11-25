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


    [Header("Debug")]
    public bool DebugMode = false;
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

    private void Update()
    {

        if (!IsAimed)
            IsFiring = false;
        else
            IsFiring = true;

        if (!FireInFixed)
        {
            if (IsFiring)
                AttemptFireShot(InheritedVelocity);

            foreach (var barrel in barrelVisuals)
                barrel.ResetBarrelOverTime(Time.deltaTime);
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
            if( _firePointIndex % FirePoints.Count == 0)
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

    public void UpdateTarget()
    {
        // if (Targeted != null)
        // {
        //     float dist = Vector3.Distance(transform.position, Targeted.position);
        //     if (dist <= ActiveRange.y)
        //     {
        //         return;
        //     }
        // }

        Targeted = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);

        if (enemies.Length == 0)
        {
            Targeted = null;
            IsFiring = false;
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
            Targeted = null;
            IsFiring = false;
            IsAimed = false;
        }
        // if (DebugMode)
        //     Debug.Log(enemies.Length + " enemies found.");
        // if(Targeted != null && DebugMode)
        //     Debug.Log("Target acquired: " + Targeted.name);
    }

    private void OnDrawGizmos()
    {
        if (TurretBase != null)
        {
            const float kArcSize = 10f;
            Color colorTraverse = new Color(1f, .5f, .5f, .1f);
            Color colorElevation = new Color(.5f, 1f, .5f, .1f);
            Color colorDepression = new Color(.5f, .5f, 1f, .1f);

            Transform arcRoot = Barrels != null ? Barrels : TurretBase;

            // Red traverse arc
            UnityEditor.Handles.color = colorTraverse;
            if (HasLimitedTraverse)
            {
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, TurretBase.up,
                    transform.forward, RightLimit,
                    kArcSize);
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, TurretBase.up,
                    transform.forward, -LeftLimit,
                    kArcSize);
            }
            else
            {
                UnityEditor.Handles.DrawSolidArc(
                    arcRoot.position, TurretBase.up,
                    transform.forward, 360f,
                    kArcSize);
            }

            if (Barrels != null)
            {
                // Green elevation arc
                UnityEditor.Handles.color = colorElevation;
                UnityEditor.Handles.DrawSolidArc(
                    Barrels.position, Barrels.right,
                    TurretBase.forward, -MaxElevation,
                    kArcSize);

                // Blue depression arc
                UnityEditor.Handles.color = colorDepression;
                UnityEditor.Handles.DrawSolidArc(
                    Barrels.position, Barrels.right,
                    TurretBase.forward, MaxDepression,
                    kArcSize);
            }
        }

        if (Targeted != null)
        {
            if (FireTarget == GlobalHelper.Faction.Foe)
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.greenYellow;
            Gizmos.DrawLine(transform.position, Targeted.position);
        }
    }

    private void RotateTurretToIdle()
    {
        // Rotate the base to its default position.
        if (HasLimitedTraverse)
        {
            LimitedTraverseAngle = Mathf.MoveTowards(
                LimitedTraverseAngle, 0f,
                TraverseSpeed * Time.deltaTime);

            if (Mathf.Abs(LimitedTraverseAngle) > Mathf.Epsilon)
                TurretBase.localEulerAngles = Vector3.up * LimitedTraverseAngle;
            else
                IsBaseAtRest = true;
        }
        else
        {
            TurretBase.rotation = Quaternion.RotateTowards(
                TurretBase.rotation,
                transform.rotation,
                TraverseSpeed * Time.deltaTime);

            IsBaseAtRest = Mathf.Abs(TurretBase.localEulerAngles.y) < Mathf.Epsilon;
        }

        if (HasBarrels)
        {
            Elevation = Mathf.MoveTowards(Elevation, 0f, ElevationSpeed * Time.deltaTime);
            if (Mathf.Abs(Elevation) > Mathf.Epsilon)
                Barrels.localEulerAngles = Vector3.right * -Elevation;
            else
                IsBarrelAtRest = true;
        }
        else // Barrels automatically at rest if there are no Barrels.
            IsBarrelAtRest = true;
    }

    private float GetTurretAngleToTarget(Vector3 targetPosition)
    {
        float angle = 999f;

        if (HasBarrels)
        {
            angle = Vector3.Angle(targetPosition - Barrels.position, Barrels.forward);
        }
        else
        {
            Vector3 flattenedTarget = Vector3.ProjectOnPlane(
                targetPosition - TurretBase.position,
                TurretBase.up);

            angle = Vector3.Angle(
                flattenedTarget - TurretBase.position,
                TurretBase.forward);
        }

        return angle;
    }
}