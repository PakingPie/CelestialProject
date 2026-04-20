using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MissileTurret : WeaponBase
{
    [Header("Missile Fire Settings")]
    [Tooltip("The target to fire at.")]
    public float FireInterval = 2.0f;

    [Header("Multi-Target Settings")]
    [Tooltip("If true, each missile in a salvo can target a different enemy")]
    public bool DistributeTargetsPerMissile = true;
    [Tooltip("Maximum missiles to fire at a single target before switching")]
    public int MaxMissilesPerTarget = 1;

    [Header("Debug Settings")]
    public bool EnableDebugGizmos = false;
    public float TestSeekerCone = 30f;

    // Track targets and missiles fired at each
    private List<Transform> _availableTargets = new List<Transform>(16);
    private Dictionary<Transform, int> _missilesFiredAtTarget = new Dictionary<Transform, int>();
    private int _currentTargetIndex = 0;
    private float _fireTimer = 0f;
    private AALauncher _launcher;
    private Transform _lastLaunchedTarget;

    void Start()
    {
        _launcher = GetComponentInChildren<AALauncher>();
    }

    void Update()
    {
        if (IsAimed && Targeted != null)
        {
            // Check if target is within seeker cone
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);
            float seekerHalfAngle = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone * 0.5f;
            float offBoresight = Mathf.Sqrt(relativeAngles.x * relativeAngles.x + relativeAngles.y * relativeAngles.y);
            if (offBoresight > seekerHalfAngle)
                return;

            // Distance
            float distanceToTarget = Vector3.Distance(transform.position, Targeted.position);
            if (distanceToTarget < ActiveRange.y && _fireTimer <= 0.0f)
            {
                // Determine the actual target for this missile
                Transform missileTarget = GetNextMissileTarget();

                if (missileTarget != null)
                {
                    _launcher.Launch(missileTarget);
                    _lastLaunchedTarget = missileTarget;

                    // Track missiles fired at this target
                    if (DistributeTargetsPerMissile)
                    {
                        TargetDistributor distributor = UseTargetDistribution ? TargetDistributor.Instance : null;
                        if (distributor != null)
                        {
                            distributor.RegisterOrdnanceReservation(missileTarget);
                        }
                        else
                        {
                            if (!_missilesFiredAtTarget.ContainsKey(missileTarget))
                                _missilesFiredAtTarget[missileTarget] = 0;
                            _missilesFiredAtTarget[missileTarget]++;
                        }
                    }
                }

                _fireTimer = FireInterval;
            }

            if (_fireTimer > 0.0f)
                _fireTimer -= Time.deltaTime;
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

    /// <summary>
    /// Get the next target for a missile, distributing across available enemies
    /// </summary>
    private Transform GetNextMissileTarget()
    {
        if (!DistributeTargetsPerMissile)
            return Targeted;

        // Refresh available targets list
        RefreshAvailableTargets();

        if (_availableTargets.Count == 0)
            return Targeted; // Fallback to primary target

        TargetDistributor distributor = UseTargetDistribution ? TargetDistributor.Instance : null;
        Transform bestAvailableTarget = null;
        int bestAvailableIndex = -1;
        int bestAvailableCount = int.MaxValue;
        bool allowOverflow = distributor == null || distributor.AllowOrdnanceOverflow;
        Transform bestOverflowTarget = null;
        int bestOverflowIndex = -1;
        int bestOverflowCount = int.MaxValue;

        for (int i = 0; i < _availableTargets.Count; i++)
        {
            int index = (_currentTargetIndex + i) % _availableTargets.Count;
            Transform candidate = _availableTargets[index];

            if (candidate == null) continue;

            int missileCount = distributor != null ? distributor.GetReservedOrdnanceCount(candidate) : GetLocalMissileCount(candidate);
            bool canAcceptWithoutOverflow = distributor != null ? distributor.CanReserveOrdnance(candidate) : missileCount < MaxMissilesPerTarget;

            if (missileCount < MaxMissilesPerTarget && canAcceptWithoutOverflow)
            {
                if (missileCount < bestAvailableCount)
                {
                    bestAvailableCount = missileCount;
                    bestAvailableIndex = index;
                    bestAvailableTarget = candidate;
                }
            }
            else if (allowOverflow && missileCount < bestOverflowCount)
            {
                bestOverflowCount = missileCount;
                bestOverflowIndex = index;
                bestOverflowTarget = candidate;
            }
        }

        Transform chosenTarget = bestAvailableTarget != null ? bestAvailableTarget : bestOverflowTarget;
        int chosenIndex = bestAvailableTarget != null ? bestAvailableIndex : bestOverflowIndex;
        if (chosenIndex >= 0)
            _currentTargetIndex = (chosenIndex + 1) % _availableTargets.Count;

        return chosenTarget;
    }

    private int GetLocalMissileCount(Transform candidate)
    {
        _missilesFiredAtTarget.TryGetValue(candidate, out int missileCount);
        return missileCount;
    }

    /// <summary>
    /// Refresh the list of valid targets within range and seeker cone
    /// </summary>
    private void RefreshAvailableTargets()
    {
        _availableTargets.Clear();

        // Get nearby enemies
        CombatRegistry.GetNearbyEnemies(transform.position, ActiveRange.y, FireTarget, _nearbyEnemies, CanTargetMissiles);

        float seekerHalfAngle = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone * 0.5f;

        foreach (var enemy in _nearbyEnemies)
        {
            if (enemy == null) continue;

            Transform enemyTransform = enemy.transform;

            // Check seeker cone (circular)
            Vector2 angles = CalcuateRelativeAngles(enemyTransform);
            float offBoresight = Mathf.Sqrt(angles.x * angles.x + angles.y * angles.y);
            if (offBoresight > seekerHalfAngle)
                continue;

            // Check distance
            float distance = Vector3.Distance(transform.position, enemyTransform.position);
            if (distance > ActiveRange.y || distance < ActiveRange.x)
                continue;

            _availableTargets.Add(enemyTransform);
        }
    }

#if UNITY_EDITOR
    public void OnDrawGizmos()
    {
        if (!EnableDebug || !EnableDebugGizmos)
            return;

        if (Targeted != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.75f);
            Gizmos.DrawLine(transform.position, Targeted.position);

            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);

            float seekerHalfAngle = TestSeekerCone * 0.5f;
            if (_launcher != null && _launcher.missilePrefabToLaunch != null)
            {
                var missile = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>();
                if (missile != null)
                    seekerHalfAngle = missile.seekerCone * 0.5f;
            }

            float offBoresight = Mathf.Sqrt(relativeAngles.x * relativeAngles.x + relativeAngles.y * relativeAngles.y);
            bool withinCone = offBoresight <= seekerHalfAngle;

            Gizmos.color = withinCone ? Color.green : Color.red;
            Gizmos.DrawWireSphere(Targeted.position, 1f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 20f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 15f);

            int reservationCount = TargetDistributor.Instance != null ? TargetDistributor.Instance.GetReservedOrdnanceCount(Targeted) : 0;

            UnityEditor.Handles.Label(Targeted.position + Vector3.up * 2f,
                $"Azimuth: {relativeAngles.x:F1}°  Elev: {relativeAngles.y:F1}°  Off-bore: {offBoresight:F1}°\n" +
                $"Seeker Half-Angle: {seekerHalfAngle:F1}°  In Cone: {withinCone}\n" +
                $"Primary Target Reservations: {reservationCount}");
        }

        if (_lastLaunchedTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, _lastLaunchedTarget.position);
            Gizmos.DrawWireSphere(_lastLaunchedTarget.position, 1.25f);

            int reservationCount = TargetDistributor.Instance != null ? TargetDistributor.Instance.GetReservedOrdnanceCount(_lastLaunchedTarget) : 0;
            UnityEditor.Handles.Label(_lastLaunchedTarget.position + Vector3.up * 4f,
                $"Last Missile Target\nReservations: {reservationCount}");
        }
    }
#endif
}
