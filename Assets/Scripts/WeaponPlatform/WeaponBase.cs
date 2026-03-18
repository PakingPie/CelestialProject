using UnityEngine;
using static GlobalHelper;
using System.Collections.Generic;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor.EditorTools;
#endif
public class WeaponBase : MonoBehaviour
{
    private static readonly ProfilerMarker ManagedUpdateTargetMarker = new ProfilerMarker("WeaponBase.ManagedUpdateTarget");
    private static readonly ProfilerMarker SelectTargetByPriorityMarker = new ProfilerMarker("WeaponBase.SelectTargetByPriority");
    private static readonly ProfilerMarker SelectNearestTargetMarker = new ProfilerMarker("WeaponBase.SelectNearestTarget");
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

    [Header("Target Distribution")]
    [Tooltip("Enable target distribution to prevent multiple weapons targeting the same enemy")]
    public bool UseTargetDistribution = true;
    [Tooltip("If true, this weapon will avoid targets that have reached max weapon count")]
    public bool AvoidOverTargeting = true;

    [Header("Priority Targeting")]
    [Tooltip("Optional: Configure target priority by vehicle type")]
    public TargetPriorityConfig PriorityConfig;

    [Header("Targeting Performance")]
    [Tooltip("How many frames to wait between target searches (>= 1).")]
    [SerializeField] private int _targetUpdateInterval = 3;
    [Tooltip("Random frame jitter to spread work across frames.")]
    [SerializeField] private int _targetUpdateJitter = 1;
    [Tooltip("Maximum frames to wait between searches when no targets are found.")]
    [SerializeField] private int _maxTargetUpdateInterval = 12;
    [Tooltip("How many frames to add per consecutive no-target search.")]
    [SerializeField] private int _noTargetBackoffStep = 1;
    [Tooltip("Reuse current target for this many frames before re-querying.")]
    [SerializeField] private int _targetReuseFrames = 2;
    [Tooltip("Reduced search range when no targets are found (clamped to ActiveRange.y).")]
    [SerializeField] private float _noTargetSearchRange = 400f;
    [Tooltip("Every N no-target searches, do a full-range scan to avoid missing distant enemies.")]
    [SerializeField] private int _fullRangeCheckInterval = 10;

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
    private int _nextTargetUpdateFrame = 0;
    private int _nextTargetRequeryFrame = 0;
    private int _noTargetStreak = 0;
    private int _currentTargetInterval = 0;


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

        int interval = Mathf.Max(1, _targetUpdateInterval);
        _currentTargetInterval = interval;
        _noTargetStreak = 0;
        int jitter = _targetUpdateJitter > 0 ? Random.Range(0, _targetUpdateJitter + 1) : 0;
        _nextTargetUpdateFrame = Time.frameCount + interval + jitter;
        _nextTargetRequeryFrame = Time.frameCount + Mathf.Max(1, _targetReuseFrames);
    }

    protected virtual void OnDisable()
    {
        CombatManager.Instance?.UnregisterTurret(this);
        TargetDistributor.Instance?.UnregisterWeapon(this);
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
        using (ManagedUpdateTargetMarker.Auto())
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

            if (Targeted != null && Time.frameCount < _nextTargetRequeryFrame)
            {
                Vector3 toTarget = Targeted.position - _cachedTransform.position;
                float distSqr = toTarget.sqrMagnitude;
                if (distSqr <= _maxRangeSqr && distSqr >= _minRangeSqr)
                    return;
            }

            int baseInterval = Mathf.Max(1, _targetUpdateInterval);
            int maxInterval = Mathf.Max(baseInterval, _maxTargetUpdateInterval);
            int interval = _currentTargetInterval > 0 ? _currentTargetInterval : baseInterval;
            if (interval > maxInterval) interval = maxInterval;
            if (Time.frameCount < _nextTargetUpdateFrame)
            {
                return;
            }

            int jitter = _targetUpdateJitter > 0 ? Random.Range(0, _targetUpdateJitter + 1) : 0;
            _nextTargetUpdateFrame = Time.frameCount + interval + jitter;

            Vector3 myPosition = _cachedTransform.position;

            float searchRange = ActiveRange.y;
            if (_noTargetStreak > 0)
            {
                bool doFullRange = _fullRangeCheckInterval > 0 && (_noTargetStreak % _fullRangeCheckInterval == 0);
                if (!doFullRange)
                    searchRange = Mathf.Min(searchRange, _noTargetSearchRange);
            }

            // Populate nearby enemies list
            CombatRegistry.GetNearbyEnemies(myPosition, searchRange, FireTarget, _nearbyEnemies, CanTargetMissiles);

            if (_nearbyEnemies.Count == 0)
            {
                Targeted = null;
                IsAimed = false;
                _noTargetStreak++;
                _currentTargetInterval = Mathf.Min(maxInterval, baseInterval + _noTargetStreak * _noTargetBackoffStep);
                return;
            }

            _noTargetStreak = 0;
            _currentTargetInterval = baseInterval;

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
                _nextTargetRequeryFrame = Time.frameCount + Mathf.Max(1, _targetReuseFrames);
                Boid boid = GetComponentInParent<Boid>();
                if (boid != null)
                {
                    boid.EnterCombat();
                }
            }
        }
    }

    protected virtual void SelectTargetByPriority(Vector3 myPosition)
    {
        using (SelectTargetByPriorityMarker.Auto())
        {
        if (PriorityConfig == null)
        {
            // Fall back to distance-based selection
            SelectNearestTarget(myPosition);
            return;
        }

        int bestAvailableCount = int.MaxValue;
        float bestAvailableScore = float.MinValue;
        VehicleBase bestAvailableTarget = null;
        int bestOverflowCount = int.MaxValue;
        float bestOverflowScore = float.MinValue;
        VehicleBase bestOverflowTarget = null;

        TargetDistributor distributor = UseTargetDistribution ? TargetDistributor.Instance : null;
        bool enforcePerTargetCap = distributor != null && AvoidOverTargeting;

        // Get current target for stickiness bonus
        VehicleBase currentTargetVehicle = null;
        if (Targeted != null)
        {
            currentTargetVehicle = Targeted.GetComponent<VehicleBase>();
            if (currentTargetVehicle == null)
                currentTargetVehicle = Targeted.GetComponentInParent<VehicleBase>();
        }

        // Cache turret orientation for cheap angle pre-check (computed once, not per-enemy)
        Vector3 turretFwd = _cachedTransform.forward;
        Vector3 turretUp = _cachedTransform.up;
        float sinMaxElevSqr = Mathf.Sin((MaxElevation + 5f) * Mathf.Deg2Rad);
        sinMaxElevSqr *= sinMaxElevSqr;
        float sinMaxDeprSqr = Mathf.Sin((MaxDepression + 5f) * Mathf.Deg2Rad);
        sinMaxDeprSqr *= sinMaxDeprSqr;
        float cosMaxTraverse = 0f, cosMaxTraverseSqr = 0f;
        if (HasLimitedTraverse)
        {
            cosMaxTraverse = Mathf.Cos((Mathf.Max(LeftLimit, RightLimit) + 5f) * Mathf.Deg2Rad);
            cosMaxTraverseSqr = cosMaxTraverse * cosMaxTraverse;
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
            Vector3 toEnemy = enemyPos - myPosition;
            float distanceSqr = toEnemy.sqrMagnitude;

            // Check min range
            if (distanceSqr < _minRangeSqr)
                continue;

            float distance = Mathf.Sqrt(distanceSqr);

            // Check priority config range limits
            var priorityEntry = PriorityConfig.GetPriorityEntry(enemyType);
            if (priorityEntry != null)
            {
                if (priorityEntry.MaxEngagementRange > 0 && distance > priorityEntry.MaxEngagementRange)
                    continue;
                if (distance < priorityEntry.MinEngagementRange)
                    continue;
            }

            // Cheap angle pre-check using dot products (skips expensive CalcuateRelativeAngles)
            float vertDot = Vector3.Dot(toEnemy, turretUp);
            float vertDotSqr = vertDot * vertDot;
            if (vertDot > 0f && vertDotSqr > sinMaxElevSqr * distanceSqr)
                continue;
            if (vertDot < 0f && vertDotSqr > sinMaxDeprSqr * distanceSqr)
                continue;
            if (HasLimitedTraverse)
            {
                float fwdDot = Vector3.Dot(toEnemy, turretFwd);
                float horizDistSqr = distanceSqr - vertDotSqr;
                if (horizDistSqr > 0.001f)
                {
                    if (cosMaxTraverse >= 0f)
                    {
                        if (fwdDot < 0f || fwdDot * fwdDot < cosMaxTraverseSqr * horizDistSqr)
                            continue;
                    }
                    else if (fwdDot < 0f && fwdDot * fwdDot > cosMaxTraverseSqr * horizDistSqr)
                    {
                        continue;
                    }
                }
            }

            // Full angle check for accurate final decision
            Vector2 angles = CalcuateRelativeAngles(enemyTransform);
            if (angles.y > MaxElevation || angles.y < -MaxDepression)
                continue;
            if (HasLimitedTraverse && (angles.x > RightLimit || angles.x < -LeftLimit))
                continue;

            // Calculate score
            float score = CalculateTargetScore(enemy, distance, currentTargetVehicle);
            int projectedCount = 1;
            bool canTargetWithoutOverflow = true;

            if (distributor != null)
            {
                projectedCount = distributor.GetProjectedWeaponCountOnTarget(this, enemyTransform);
                if (enforcePerTargetCap)
                    canTargetWithoutOverflow = projectedCount <= distributor.MaxWeaponsPerTarget;
            }

            if (canTargetWithoutOverflow)
            {
                if (projectedCount < bestAvailableCount || (projectedCount == bestAvailableCount && score > bestAvailableScore))
                {
                    bestAvailableCount = projectedCount;
                    bestAvailableScore = score;
                    bestAvailableTarget = enemy;
                }
            }
            else if (projectedCount < bestOverflowCount || (projectedCount == bestOverflowCount && score > bestOverflowScore))
            {
                bestOverflowCount = projectedCount;
                bestOverflowScore = score;
                bestOverflowTarget = enemy;
            }
        }

        VehicleBase bestTarget = bestAvailableTarget != null ? bestAvailableTarget : bestOverflowTarget;
        float bestScore = bestAvailableTarget != null ? bestAvailableScore : bestOverflowScore;

        Transform newTarget = bestTarget != null ? bestTarget.transform : null;
        
        // Update target distributor
        if (distributor != null)
        {
            distributor.UpdateWeaponTarget(this, newTarget);
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
        using (SelectNearestTargetMarker.Auto())
        {
        int bestAvailableCount = int.MaxValue;
        float bestAvailableDistanceSqr = Mathf.Infinity;
        Transform bestAvailableTarget = null;
        int bestOverflowCount = int.MaxValue;
        float bestOverflowDistanceSqr = Mathf.Infinity;
        Transform bestOverflowTarget = null;

        TargetDistributor distributor = UseTargetDistribution ? TargetDistributor.Instance : null;
        bool enforcePerTargetCap = distributor != null && AvoidOverTargeting;

        // Cache turret orientation for cheap angle pre-check (computed once, not per-enemy)
        Vector3 turretFwd = _cachedTransform.forward;
        Vector3 turretUp = _cachedTransform.up;
        float sinMaxElevSqr = Mathf.Sin((MaxElevation + 5f) * Mathf.Deg2Rad);
        sinMaxElevSqr *= sinMaxElevSqr;
        float sinMaxDeprSqr = Mathf.Sin((MaxDepression + 5f) * Mathf.Deg2Rad);
        sinMaxDeprSqr *= sinMaxDeprSqr;
        float cosMaxTraverse = 0f, cosMaxTraverseSqr = 0f;
        if (HasLimitedTraverse)
        {
            cosMaxTraverse = Mathf.Cos((Mathf.Max(LeftLimit, RightLimit) + 5f) * Mathf.Deg2Rad);
            cosMaxTraverseSqr = cosMaxTraverse * cosMaxTraverse;
        }

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];

            if (enemy == _owner) continue;

            Transform enemyTransform = enemy.transform;
            Vector3 enemyPos = enemyTransform.position;
            Vector3 toEnemy = enemyPos - myPosition;
            float distanceSqr = toEnemy.sqrMagnitude;

            if (distanceSqr < _minRangeSqr)
                continue;

            // Cheap angle pre-check using dot products (skips expensive CalcuateRelativeAngles)
            float vertDot = Vector3.Dot(toEnemy, turretUp);
            float vertDotSqr = vertDot * vertDot;
            if (vertDot > 0f && vertDotSqr > sinMaxElevSqr * distanceSqr)
                continue;
            if (vertDot < 0f && vertDotSqr > sinMaxDeprSqr * distanceSqr)
                continue;
            if (HasLimitedTraverse)
            {
                float fwdDot = Vector3.Dot(toEnemy, turretFwd);
                float horizDistSqr = distanceSqr - vertDotSqr;
                if (horizDistSqr > 0.001f)
                {
                    if (cosMaxTraverse >= 0f)
                    {
                        if (fwdDot < 0f || fwdDot * fwdDot < cosMaxTraverseSqr * horizDistSqr)
                            continue;
                    }
                    else if (fwdDot < 0f && fwdDot * fwdDot > cosMaxTraverseSqr * horizDistSqr)
                    {
                        continue;
                    }
                }
            }

            // Full angle check for accurate final decision
            Vector2 angles = CalcuateRelativeAngles(enemyTransform);
            if (angles.y > MaxElevation || angles.y < -MaxDepression)
                continue;
            if (HasLimitedTraverse && (angles.x > RightLimit || angles.x < -LeftLimit))
                continue;

            int projectedCount = 1;
            bool canTargetWithoutOverflow = true;

            if (distributor != null)
            {
                projectedCount = distributor.GetProjectedWeaponCountOnTarget(this, enemyTransform);
                if (enforcePerTargetCap)
                    canTargetWithoutOverflow = projectedCount <= distributor.MaxWeaponsPerTarget;
            }

            if (canTargetWithoutOverflow)
            {
                if (projectedCount > bestAvailableCount)
                    continue;

                if (projectedCount == bestAvailableCount && distanceSqr >= bestAvailableDistanceSqr)
                    continue;

                bestAvailableCount = projectedCount;
                bestAvailableDistanceSqr = distanceSqr;
                bestAvailableTarget = enemyTransform;
            }
            else
            {
                if (projectedCount > bestOverflowCount)
                    continue;

                if (projectedCount == bestOverflowCount && distanceSqr >= bestOverflowDistanceSqr)
                    continue;

                bestOverflowCount = projectedCount;
                bestOverflowDistanceSqr = distanceSqr;
                bestOverflowTarget = enemyTransform;
            }
        }

        Transform nearestEnemy = bestAvailableTarget != null ? bestAvailableTarget : bestOverflowTarget;

        // Update target distributor
        if (distributor != null)
        {
            distributor.UpdateWeaponTarget(this, nearestEnemy);
        }

        Targeted = nearestEnemy;
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
    public void OnDrawGizmos()
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