using UnityEngine;
using static GlobalHelper;
using System.Collections.Generic;
public class WeaponBase : MonoBehaviour
{
    [Header("Turret")]
    [Tooltip("Transform of the turret's azimuthal rotations.")]
    public Transform TurretBase = null;
    [Tooltip("Transform of the turret's elevation rotations. ")]
    public Transform Barrels = null;
    [Tooltip("Speed at which the turret's guns elevate up and down.")]
    public float ElevationSpeed = 30f;
    [Tooltip("Highest upwards elevation the turret's barrels can aim.")]
    public float MaxElevation = 60f;
    [Tooltip("Lowest downwards elevation the turret's barrels can aim.")]
    public float MaxDepression = 5f;
    [Tooltip("Speed at which the turret can rotate left/right.")]
    public float TraverseSpeed = 60f;
    [Tooltip("When true, the turret can only rotate horizontally with the given limits.")]
    public bool HasLimitedTraverse = false;
    [Range(0, 179)] public float LeftLimit = 120f;
    [Range(0, 179)] public float RightLimit = 120f;
    [Tooltip("When idle, the turret does not aim at anything and simply points forwards.")]
    public bool IsIdle = false;
    [Tooltip("Position the turret will aim at when not idle. Set this to whatever you want the turret to actively aim at.")]
    public Vector3 AimPosition = Vector3.zero;
    [Tooltip("When the turret is within this many degrees of the target, it is considered aimed.")]
    public float AimedThreshold = 5f;

    [Header("Targeting")]
    public Transform Targeted;
    [Tooltip("Faction that this weapon will fire upon.")]
    public GlobalHelper.Faction FireTarget = GlobalHelper.Faction.Foe;
    [Tooltip("The type of guidance this weapon uses to track targets.")]
    public GlobalHelper.GuidanceType GuidanceType = GlobalHelper.GuidanceType.Lead;
    [Tooltip("The range within which the gun can target enemies.")]
    public Vector2 ActiveRange = new Vector2(5f, 500f);
    [Tooltip("Number of updates per second for the turret targeting system.")]
    public int UpdateRate = 60;

    [Tooltip("Debug")]
    public bool ShowGizmos = true;

    private float _angleToTarget = 0f;
    private float _limitedTraverseAngle = 0f;
    private float _elevation = 0f;
    private bool _hasBarrels = true;
    private bool _isAimed = false;
    private bool _isBaseAtRest = false;
    private bool _isBarrelAtRest = false;

    public float AngleToTarget { get { return IsIdle ? 999f : _angleToTarget; } set { _angleToTarget = value; } }
    public float LimitedTraverseAngle { get { return _limitedTraverseAngle; } set { _limitedTraverseAngle = value; } }
    public float Elevation { get { return _elevation; } set { _elevation = value; } }
    public bool HasBarrels { get { return _hasBarrels; } }
    public bool IsAimed { get { return _isAimed; } set { _isAimed = value; } }
    public bool IsBaseAtRest { get { return _isBaseAtRest; } set { _isBaseAtRest = value; } }
    public bool IsBarrelAtRest { get { return _isBarrelAtRest; } set { _isBarrelAtRest = value; } }
    public bool IsTurretAtRest { get { return _isBarrelAtRest && _isBaseAtRest; } }

    // Cached values
    protected Transform _cachedTransform;
    protected VehicleBase _owner;
    protected float _maxRangeSqr;
    protected float _minRangeSqr;

    // Reusable list for nearby enemies - no allocations
    protected List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    [HideInInspector] public bool UseManagedUpdates = true;

    protected virtual void Awake()
    {
        _cachedTransform = transform;
        _owner = GetComponentInParent<VehicleBase>();
        _hasBarrels = Barrels != null;
        CacheRangeValues();
    }

    // protected virtual void Update()
    // {
    //     ManagedUpdateTarget();
    // }

    protected virtual void OnEnable()
    {
        if (UseManagedUpdates)
            CombatManager.Instance?.RegisterTurret(this);
    }

    protected virtual void OnDisable()
    {
        CombatManager.Instance?.UnregisterTurret(this);
    }

    protected void CacheRangeValues()
    {
        _maxRangeSqr = ActiveRange.y * ActiveRange.y;
        _minRangeSqr = ActiveRange.x * ActiveRange.x;
    }

    /// <summary>
    /// Called by CombatManager. Override in subclasses if needed.
    /// </summary>
    public virtual void ManagedUpdateTarget()
    {
        Vector3 myPosition = _cachedTransform.position;

        // Use spatial partitioning for efficiency
        CombatRegistry.GetNearbyEnemies(myPosition, ActiveRange.y, FireTarget, _nearbyEnemies);

        if (_nearbyEnemies.Count == 0)
        {
            Targeted = null;
            IsAimed = false;
            return;
        }

        float shortestDistanceSqr = Mathf.Infinity;
        Transform nearestEnemy = null;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];

            if (enemy == _owner) continue;

            Transform enemyTransform = enemy.transform;
            Vector3 enemyPos = enemyTransform.position;

            float distanceSqr = (enemyPos - myPosition).sqrMagnitude;

            if (distanceSqr < _minRangeSqr || distanceSqr >= shortestDistanceSqr)
                continue;

            Vector2 angles = CalcuateRelativeAngles(enemyTransform);

            if (angles.y > MaxElevation || angles.y < -MaxDepression)
                continue;

            if (HasLimitedTraverse && (angles.x > RightLimit || angles.x < -LeftLimit))
                continue;

            shortestDistanceSqr = distanceSqr;
            nearestEnemy = enemyTransform;
        }

        Targeted = nearestEnemy;
        if (nearestEnemy == null)
            IsAimed = false;

        if (Targeted != null)
        {
            Boid boid = GetComponentInParent<Boid>();
            if (boid != null)
            {
                boid.EnterCombat();
            }
        }
    }

    public void RotateBaseToFaceTarget(Vector3 targetPosition)
    {
        Vector3 turretUp = transform.up;

        Vector3 vecToTarget = targetPosition - TurretBase.position;
        Vector3 flattenedVecForBase = Vector3.ProjectOnPlane(vecToTarget, turretUp);

        if (HasLimitedTraverse)
        {
            Vector3 turretForward = transform.forward;
            float targetTraverse = Vector3.SignedAngle(turretForward, flattenedVecForBase, turretUp);

            targetTraverse = Mathf.Clamp(targetTraverse, -LeftLimit, RightLimit);
            _limitedTraverseAngle = Mathf.MoveTowards(
                _limitedTraverseAngle,
                targetTraverse,
                TraverseSpeed * Time.deltaTime);

            if (Mathf.Abs(_limitedTraverseAngle) > Mathf.Epsilon)
                TurretBase.localEulerAngles = Vector3.up * _limitedTraverseAngle;
        }
        else
        {
            TurretBase.rotation = Quaternion.RotateTowards(
                Quaternion.LookRotation(TurretBase.forward, turretUp),
                Quaternion.LookRotation(flattenedVecForBase, turretUp),
                TraverseSpeed * Time.deltaTime);
        }
    }

    public void RotateBarrelsToFaceTarget(Vector3 targetPosition)
    {
        Vector3 localTargetPos = TurretBase.InverseTransformDirection(targetPosition - Barrels.position);
        Vector3 flattenedVecForBarrels = Vector3.ProjectOnPlane(localTargetPos, Vector3.up);

        float targetElevation = Vector3.Angle(flattenedVecForBarrels, localTargetPos);
        targetElevation *= Mathf.Sign(localTargetPos.y);

        targetElevation = Mathf.Clamp(targetElevation, -MaxDepression, MaxElevation);
        _elevation = Mathf.MoveTowards(_elevation, targetElevation, ElevationSpeed * Time.deltaTime);

        if (Mathf.Abs(_elevation) > Mathf.Epsilon)
            Barrels.localEulerAngles = Vector3.right * -_elevation;
    }

    // Calculate the relative angles needed to aim at the target.
    public Vector2 CalcuateRelativeAngles(Transform target)
    {
        // Azimuth calculation
        Vector3 vecToTarget = target.position - TurretBase.position;
        Vector3 flattenedVecForBase = Vector3.ProjectOnPlane(vecToTarget, transform.up);
        float azimuth = Vector3.SignedAngle(transform.forward, flattenedVecForBase, transform.up);

        // Elevation calculation
        float elevation = 0f;
        if (_hasBarrels && Barrels != null)
        {
            Vector3 localTargetPos = TurretBase.InverseTransformDirection(target.position - Barrels.position);
            Vector3 flattenedVecForBarrels = Vector3.ProjectOnPlane(localTargetPos, Vector3.up);

            elevation = Vector3.Angle(flattenedVecForBarrels, localTargetPos);
            elevation *= Mathf.Sign(localTargetPos.y);
        }

        return new Vector2(azimuth, elevation);
    }

    public void RotateTurretToIdle()
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

    public float GetTurretAngleToTarget(Vector3 targetPosition)
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

    private void OnDrawGizmos()
    {
        if (TurretBase != null && ShowGizmos)
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
            if (FireTarget.HasFlag(GlobalHelper.Faction.Foe))
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.greenYellow;
            Gizmos.DrawLine(transform.position, Targeted.position);
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