using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


public class MissileSalvo : WeaponBase
{
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

                    // Track missiles fired at this target
                    if (DistributeTargetsPerMissile)
                    {
                        if (!_missilesFiredAtTarget.ContainsKey(missileTarget))
                            _missilesFiredAtTarget[missileTarget] = 0;
                        _missilesFiredAtTarget[missileTarget]++;
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

        // Find a target that hasn't reached max missiles
        for (int i = 0; i < _availableTargets.Count; i++)
        {
            int index = (_currentTargetIndex + i) % _availableTargets.Count;
            Transform candidate = _availableTargets[index];

            if (candidate == null) continue;

            int missileCount = 0;
            _missilesFiredAtTarget.TryGetValue(candidate, out missileCount);

            if (missileCount < MaxMissilesPerTarget)
            {
                _currentTargetIndex = (index + 1) % _availableTargets.Count;
                return candidate;
            }
        }

        // All targets have max missiles, cycle back to first
        _currentTargetIndex = (_currentTargetIndex + 1) % _availableTargets.Count;
        return _availableTargets[_currentTargetIndex];
    }

    /// <summary>
    /// Refresh the list of valid targets within range and seeker cone
    /// </summary>
    private void RefreshAvailableTargets()
    {
        _availableTargets.Clear();

        // Get nearby enemies
        CombatRegistry.GetNearbyEnemies(transform.position, ActiveRange.y, FireTarget, _nearbyEnemies, CanTargetMissiles);

        float seekerCone = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone;

        foreach (var enemy in _nearbyEnemies)
        {
            if (enemy == null) continue;

            Transform enemyTransform = enemy.transform;

            // Check seeker cone
            Vector2 angles = CalcuateRelativeAngles(enemyTransform);
            if (Mathf.Abs(angles.x) > seekerCone / 2f || Mathf.Abs(angles.y) > seekerCone / 2f)
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
        base.OnDrawGizmos();
        if (Targeted != null && EnableDebugGizmos)
        {
            // Draw line to test target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, Targeted.position);
            // Calculate relative angles using the new firing direction method
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);

            // Get seeker cone if launcher is available
            float seekerCone = TestSeekerCone;
            if (_launcher != null && _launcher.missilePrefabToLaunch != null)
            {
                var missile = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>();
                if (missile != null)
                    seekerCone = missile.seekerCone;
            }

            // Check if target is within seeker cone
            bool withinCone = Mathf.Abs(relativeAngles.x) <= seekerCone / 2f &&
                              Mathf.Abs(relativeAngles.y) <= seekerCone / 2f;

            // Draw sphere at target - green if in cone, red if outside
            Gizmos.color = withinCone ? Color.green : Color.red;
            Gizmos.DrawWireSphere(Targeted.position, 1f);

            // Draw firing direction (green)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 20f);

            // Draw transform.forward (blue) for comparison
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 15f);

            UnityEditor.Handles.Label(Targeted.position + Vector3.up * 2f,
                $"Azimuth: {relativeAngles.x:F1}°  Elev: {relativeAngles.y:F1}°\n" +
                $"Seeker Cone: {seekerCone}°  In Cone: {withinCone}\n");
        }
    }
#endif
}

