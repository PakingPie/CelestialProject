using UnityEngine;
using static GlobalHelper;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.EditorTools;
#endif
public class WeaponBase : MonoBehaviour
{
    [Header("Turret")]
    [Tooltip("Transform of the turret's azimuthal rotations.")]
    public Transform TurretBase = null;
    [Tooltip("Transform of the turret's elevation rotations. ")]
    public Transform Barrels = null;
    [Tooltip("Speed at which the turret's guns elevate up and down.")]
    [SerializeField] private float _elevationSpeed = 30f;
    [Tooltip("Highest upwards elevation the turret's barrels can aim.")]
    public float MaxElevation = 60f;
    [Tooltip("Lowest downwards elevation the turret's barrels can aim.")]
    public float MaxDepression = 5f;
    [Tooltip("Speed at which the turret can rotate left/right.")]
    [SerializeField] private float _traverseSpeed = 60f;
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

    public float ElevationSpeed { get { return _elevationSpeed * Effectiveness; } set { _elevationSpeed = value; } }
    public float TraverseSpeed { get { return _traverseSpeed * Effectiveness; } set { _traverseSpeed = value; } }

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

    [Header("Priority Targeting")]
    [Tooltip("Optional: Configure target priority by vehicle type")]
    public TargetPriorityConfig PriorityConfig;

    // Add these protected fields near the other cached values:
    protected float _currentTargetScore = 0f;

    [Header("Anti-Missile")]
    [Tooltip("Can this gun target missiles?")]
    public bool CanTargetMissiles = false;
    [Tooltip("Prioritize missiles over vehicles")]
    public bool PrioritizeMissiles = false;
    [Tooltip("Range to detect missiles")]
    public float MissileDetectionRange = 200f;


    [Header("Status")]
    public float Effectiveness = 1f;
    public bool IsFunctional = true;

    [Header("Manual Targeting")]
    [Tooltip("When true, automatic targeting is disabled and only manual targets are used")]
    public bool IsManualTargeting = false;
    [Tooltip("How long to keep manual target before reverting to auto (0 = forever)")]
    public float ManualTargetDuration = 0f;


    [Header("Debug")]
    [Tooltip("Enable Debug Gizmos")]
    public bool EnableDebug = true;
    public bool ShowGunAngles = false;

    private float _angleToTarget = 0f;
    private float _limitedTraverseAngle = 0f;
    private float _elevation = 0f;
    private bool _hasBarrels = true;
    private bool _isAimed = false;
    private bool _isBaseAtRest = false;
    private bool _isBarrelAtRest = false;
    private float _manualTargetTime = 0f;


    public float AngleToTarget { get { return IsIdle ? 999f : _angleToTarget; } set { _angleToTarget = value; } }
    public float LimitedTraverseAngle { get { return _limitedTraverseAngle; } set { _limitedTraverseAngle = value; } }
    public float Elevation { get { return _elevation; } set { _elevation = value; } }
    public bool HasBarrels { get { return _hasBarrels; } }
    public bool IsAimed { get { return _isAimed; } set { _isAimed = value; } }
    public bool IsBaseAtRest { get { return _isBaseAtRest; } set { _isBaseAtRest = value; } }
    public bool IsBarrelAtRest { get { return _isBarrelAtRest; } set { _isBarrelAtRest = value; } }
    public bool IsTurretAtRest { get { return _isBarrelAtRest && _isBaseAtRest; } }

    // Cached values
    protected VehicleBase _owner;
    protected float _maxRangeSqr;
    protected float _minRangeSqr;

    // Reusable list for nearby enemies - no allocations
    protected List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    [HideInInspector] public bool UseManagedUpdates = true;

    // Replace the field and Awake initialization with this:
    private Transform _cachedTransform;
    protected Transform CachedTransform
    {
        get
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            return _cachedTransform;
        }
    }

    protected virtual void Awake()
    {
        _cachedTransform = transform; // Still cache in Awake for performance
        _owner = GetComponentInParent<VehicleBase>();
        _hasBarrels = Barrels != null;
        CacheRangeValues();
    }

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
        // Handle manual targeting mode
        if (IsManualTargeting)
        {
            // Check if manual target is still valid
            if (Targeted == null)
            {
                // Target was destroyed, revert to auto
                ClearManualTarget();
            }
            else if (ManualTargetDuration > 0f && Time.time - _manualTargetTime > ManualTargetDuration)
            {
                // Manual target duration expired
                ClearManualTarget();
            }
            else
            {
                // Keep manual target, skip automatic selection
                return;
            }
        }

        Vector3 myPosition = _cachedTransform.position;

        // Populate nearby enemies list
        CombatRegistry.GetNearbyEnemies(myPosition, ActiveRange.y, FireTarget, _nearbyEnemies, CanTargetMissiles);

        if (_nearbyEnemies.Count == 0)
        {
            Targeted = null;
            IsAimed = false;
            return;
        }

        // Use priority-based or distance-based selection
        if (PriorityConfig != null)
        {
            SelectTargetByPriority(myPosition);
        }
        else
        {
            SelectNearestTarget(myPosition);
        }

        if (Targeted == null)
        {
            IsAimed = false;
        }
        else
        {
            Boid boid = GetComponentInParent<Boid>();
            if (boid != null)
            {
                boid.EnterCombat();
            }
        }
    }

    protected virtual void SelectTargetByPriority(Vector3 myPosition)
    {
        if (PriorityConfig == null)
        {
            // Fall back to distance-based selection
            SelectNearestTarget(myPosition);
            return;
        }

        float bestScore = float.MinValue;
        VehicleBase bestTarget = null;

        // Get current target for stickiness bonus
        VehicleBase currentTargetVehicle = null;
        if (Targeted != null)
        {
            currentTargetVehicle = Targeted.GetComponent<VehicleBase>();
            if (currentTargetVehicle == null)
                currentTargetVehicle = Targeted.GetComponentInParent<VehicleBase>();
        }

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];

            if (enemy == null || enemy == _owner)
                continue;

            VehicleType enemyType = enemy.VehicleType;

            // Check if this type should be ignored
            if (PriorityConfig.ShouldIgnore(enemyType))
                continue;

            Transform enemyTransform = enemy.transform;
            Vector3 enemyPos = enemyTransform.position;
            float distanceSqr = (enemyPos - myPosition).sqrMagnitude;
            float distance = Mathf.Sqrt(distanceSqr);

            // Check min range
            if (distanceSqr < _minRangeSqr)
                continue;

            // Check priority config range limits
            var priorityEntry = PriorityConfig.GetPriorityEntry(enemyType);
            if (priorityEntry != null)
            {
                if (priorityEntry.MaxEngagementRange > 0 && distance > priorityEntry.MaxEngagementRange)
                    continue;
                if (distance < priorityEntry.MinEngagementRange)
                    continue;
            }

            // Check angle constraints
            Vector2 angles = CalcuateRelativeAngles(enemyTransform);
            if (angles.y > MaxElevation || angles.y < -MaxDepression)
                continue;
            if (HasLimitedTraverse && (angles.x > RightLimit || angles.x < -LeftLimit))
                continue;

            // Calculate score
            float score = CalculateTargetScore(enemy, distance, currentTargetVehicle);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }

        if (bestTarget != null)
        {
            Targeted = bestTarget.transform;
            _currentTargetScore = bestScore;
        }
        else
        {
            Targeted = null;
            _currentTargetScore = 0f;
        }
    }

    protected float CalculateTargetScore(VehicleBase enemy, float distance, VehicleBase currentTarget)
    {
        VehicleType enemyType = enemy.VehicleType;

        // Base priority from config
        float basePriority = PriorityConfig.GetPriority(enemyType);

        // Distance factor (0-100 scale, closer = higher)
        float maxRange = ActiveRange.y;
        float distanceFactor = (1f - Mathf.Clamp01(distance / maxRange)) * 100f;

        // Damage factor (favor damaged targets)
        float damageFactor = 0f;
        if (enemy.MaxHitPoints > 0)
        {
            float healthPercent = (float)enemy.HitPoints / enemy.MaxHitPoints;
            damageFactor = (1f - healthPercent) * 100f;
        }

        // Target stickiness (bonus for current target)
        float stickinessBonus = 0f;
        if (currentTarget != null && enemy == currentTarget)
        {
            stickinessBonus = PriorityConfig.TargetStickinessBonus;
        }

        // Calculate final score
        float score = basePriority
            + (distanceFactor * PriorityConfig.DistanceWeight)
            + (damageFactor * PriorityConfig.DamageWeight)
            + stickinessBonus;

        return score;
    }

    protected void SelectNearestTarget(Vector3 myPosition)
    {
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

    // Get faction from parent vehicle
    public Faction GetOwnerFaction()
    {
        VehicleBase vehicle = GetComponentInParent<VehicleBase>();
        if (vehicle != null)
            return vehicle.FactionType;
        return Faction.Player;
    }

    /// <summary>
    /// Manually set a target. Optionally locks targeting to this target.
    /// </summary>
    /// <param name="newTarget">The target transform</param>
    /// <param name="lockTarget">If true, disables automatic targeting until ClearManualTarget is called</param>
    public bool SetTarget(Transform newTarget, bool lockTarget = false)
    {
        if (newTarget == null)
        {
            ClearManualTarget();
            return false;
        }

        // Optional: Validate target
        VehicleBase targetVehicle = newTarget.GetComponent<VehicleBase>()
                                    ?? newTarget.GetComponentInParent<VehicleBase>();

        if (targetVehicle != null)
        {
            // Check faction
            if ((targetVehicle.FactionType & FireTarget) == 0)
            {
                return false;
            }

            // Ensure cached transform is valid
            if (_cachedTransform == null)
            {
                _cachedTransform = transform;
            }

            float distance = Vector3.Distance(_cachedTransform.position, newTarget.position);
            if (distance > ActiveRange.y || distance < ActiveRange.x)
            {
                return false;
            }
        }

        Targeted = newTarget;

        if (lockTarget)
        {
            IsManualTargeting = true;
            _manualTargetTime = Time.time;
        }

        return true;
    }

    /// <summary>
    /// Clear manual target and return to automatic targeting
    /// </summary>
    public void ClearManualTarget()
    {
        IsManualTargeting = false;
        _manualTargetTime = 0f;
        // Targeted will be updated on next ManagedUpdateTarget call
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!EnableDebug) return;
        // Draw line between turret and aim position
        if (Targeted != null)
        {
            // If Gun's target is Foe, draw in green, else if is Ally or Player, draw in red
            Gizmos.color = (FireTarget & Faction.Foe) == Faction.Foe ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, Targeted.position);
        }

        if (TurretBase != null && ShowGunAngles)
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
    }
#endif
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