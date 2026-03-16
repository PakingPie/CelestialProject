using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Unity.Profiling;


public class MissileSalvo : WeaponBase
{
    private static readonly ProfilerMarker RefreshAvailableTargetsMarker = new ProfilerMarker("MissileSalvo.RefreshAvailableTargets");
    [Header("Missile Fire Settings")]
    [Tooltip("The target to fire at.")]
    public float FireInterval = 10.0f;

    [Header("Multi-Target Settings")]
    [Tooltip("If true, each missile in a salvo can target a different enemy")]
    public bool DistributeTargetsPerMissile = true;
    [Tooltip("Maximum missiles to fire at a single target before switching")]
    public int MaxMissilesPerTarget = 1;


    [Header("Debug Settings")]
    public bool EnableDebugGizmos = false;
    public float TestSeekerCone = 30f;

    private float _fireTimer = 0f;
    private AALauncher _launcher;

    // Track targets and missiles fired at each
    private List<Transform> _availableTargets = new List<Transform>(16);
    private Dictionary<Transform, int> _missilesFiredAtTarget = new Dictionary<Transform, int>();
    private int _currentTargetIndex = 0;
    private Transform _lastLaunchedTarget;

    private void Start()
    {
        _launcher = GetComponentInChildren<AALauncher>();
    }

    private void Update()
    {
        if (Targeted != null)
        {
            // Get Relative angles to target 
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);
            // Get seeker cone angle from launcher's prebab
            float seekerCone = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone;
            if (Mathf.Abs(relativeAngles.x) > seekerCone / 2f || Mathf.Abs(relativeAngles.y) > seekerCone / 2f)
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

                // Check if using salvo mode (FireOnFullyReload)
                AAHardpoint hardpoint = _launcher as AAHardpoint;
                if (hardpoint != null && hardpoint.FireOnFullyReload)
                {
                    // In salvo mode: use SalvoInterval between shots
                    _fireTimer = hardpoint.SalvoInterval;

                    // Reset tracking when salvo completes (all missiles fired)
                    if (hardpoint.MagazineCount <= 0)
                    {
                        _missilesFiredAtTarget.Clear();
                        _currentTargetIndex = 0;
                    }
                }
                else
                {
                    // Normal mode: use FireInterval between shots
                    _fireTimer = FireInterval;
                    // Reset tracking after each shot in normal mode
                    _missilesFiredAtTarget.Clear();
                }
            }

            if (_fireTimer > 0.0f)
                _fireTimer -= Time.deltaTime;
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
        using (RefreshAvailableTargetsMarker.Auto())
        {
            _availableTargets.Clear();

            // Get nearby enemies
            CombatRegistry.GetNearbyEnemies(transform.position, ActiveRange.y, FireTarget, _nearbyEnemies, CanTargetMissiles);

            float seekerCone = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone;
            float maxRangeSqr = ActiveRange.y * ActiveRange.y;
            float minRangeSqr = ActiveRange.x * ActiveRange.x;
            Vector3 myPosition = transform.position;

            foreach (var enemy in _nearbyEnemies)
            {
                if (enemy == null) continue;

                Transform enemyTransform = enemy.transform;

                // Check distance first to avoid expensive angle math
                float distanceSqr = (enemyTransform.position - myPosition).sqrMagnitude;
                if (distanceSqr > maxRangeSqr || distanceSqr < minRangeSqr)
                    continue;

                // Check seeker cone
                Vector2 angles = CalcuateRelativeAngles(enemyTransform);
                if (Mathf.Abs(angles.x) > seekerCone / 2f || Mathf.Abs(angles.y) > seekerCone / 2f)
                    continue;

                _availableTargets.Add(enemyTransform);
            }
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

            float seekerCone = TestSeekerCone;
            if (_launcher != null && _launcher.missilePrefabToLaunch != null)
            {
                var missile = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>();
                if (missile != null)
                    seekerCone = missile.seekerCone;
            }

            bool withinCone = Mathf.Abs(relativeAngles.x) <= seekerCone / 2f &&
                              Mathf.Abs(relativeAngles.y) <= seekerCone / 2f;

            Gizmos.color = withinCone ? Color.green : Color.red;
            Gizmos.DrawWireSphere(Targeted.position, 1f);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 20f);

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 15f);

            int reservationCount = TargetDistributor.Instance != null ? TargetDistributor.Instance.GetReservedOrdnanceCount(Targeted) : 0;

            UnityEditor.Handles.Label(Targeted.position + Vector3.up * 2f,
                $"Azimuth: {relativeAngles.x:F1}°  Elev: {relativeAngles.y:F1}°\n" +
                $"Seeker Cone: {seekerCone}°  In Cone: {withinCone}\n" +
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

