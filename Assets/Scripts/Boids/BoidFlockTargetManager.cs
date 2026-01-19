using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static GlobalHelper;

public class BoidFlockTargetManager : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5000f;
    [SerializeField] private float _detectionInterval = 0.2f;
    [SerializeField] private List<string> _targetTags = new List<string>();
    [SerializeField] private List<string> _ignoreTags = new List<string>();

    [Header("Target Assignment")]
    [SerializeField] private int _maxTargets = 10;
    [SerializeField] private int _maxBoidsPerTarget = 3;
    [SerializeField] private float _reassignInterval = 1f;

    [Header("Threat Evaluation")]
    [SerializeField] private float _distanceWeight = 1f;
    [SerializeField] private float _angleWeight = 0.5f;
    [SerializeField] private float _healthWeight = 0.3f;
    [SerializeField] private float _targetingUsWeight = 2f;

    [Header("Flock Identity")]
    [SerializeField] private string _flockId = "";
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Neutral;

    [Header("Command State")]
    [SerializeField] private Transform _priorityTarget;
    [SerializeField] private Transform _defendTarget;
    [SerializeField] private float _defendRadius;
    [SerializeField] private bool _isDefenseMode;

    public bool _debugMode = false;

    public string FlockId => _flockId;
    public GlobalHelper.Team Team => _team;
    public Transform PriorityTarget => _priorityTarget;
    public bool IsDefenseMode => _isDefenseMode;

    private Dictionary<Transform, BoidTargetInfo> _knownTargets = new Dictionary<Transform, BoidTargetInfo>();
    private Dictionary<Boid, BoidTargetInfo> _boidAssignments = new Dictionary<Boid, BoidTargetInfo>();
    private List<Boid> _managedBoids = new List<Boid>();

    private float _lastDetectionTime;
    private float _lastAssignmentTime;
    private Transform _flockCenter;

    private List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);

    public IReadOnlyDictionary<Boid, BoidTargetInfo> BoidAssignments => _boidAssignments;

    public void Initialize(
        string flockId,
        GlobalHelper.Team team,
        float detectionRadius,
        List<string> targetTags,
        List<string> ignoreTags)
    {
        _flockId = flockId;
        _team = team;
        _detectionRadius = detectionRadius;
        _targetTags = targetTags ?? new List<string>();
        _ignoreTags = ignoreTags ?? new List<string>();
    }

    public Faction GetFaction()
    {
        switch (_team)
        {
            case GlobalHelper.Team.Player:
                return GlobalHelper.Faction.Player;
            case GlobalHelper.Team.Ally:
                return GlobalHelper.Faction.Ally;
            case GlobalHelper.Team.Foe:
                return GlobalHelper.Faction.Foe;
            default:
                return GlobalHelper.Faction.Neutral;
        }
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        foreach (var ignoreTag in _ignoreTags)
        {
            if (target.CompareTag(ignoreTag)) return false;
        }

        Boid boid = target.GetComponent<Boid>();
        if (boid != null && _managedBoids.Contains(boid)) return false;

        foreach (var tag in _targetTags)
        {
            if (target.CompareTag(tag)) return true;
        }

        var health = target.GetComponent<VehicleBase>();
        if (health != null) return true;

        return false;
    }

    private bool IsIgnored(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            foreach (var ignoreTag in _ignoreTags)
            {
                if (current.CompareTag(ignoreTag)) return true;
            }
            current = current.parent;
        }
        return false;
    }

    void Awake()
    {
        _flockCenter = transform;
    }

    public void RegisterBoid(Boid boid)
    {
        if (!_managedBoids.Contains(boid))
        {
            _managedBoids.Add(boid);
            _boidAssignments[boid] = null;
        }
    }

    public void UnregisterBoid(Boid boid)
    {
        _managedBoids.Remove(boid);

        if (_boidAssignments.TryGetValue(boid, out var targetInfo) && targetInfo != null)
        {
            targetInfo.AssignedBoidCount--;
        }
        _boidAssignments.Remove(boid);
    }

    public Transform GetAssignedTarget(Boid boid)
    {
        if (_boidAssignments.TryGetValue(boid, out var info) && info != null && info.IsValid)
        {
            return info.Target;
        }
        return null;
    }

    public BoidTargetInfo GetTargetInfo(Boid boid)
    {
        if (_boidAssignments.TryGetValue(boid, out var info))
        {
            if (info != null && info.Target == null)
            {
                _boidAssignments[boid] = null;
                return null;
            }
            return info;
        }
        return null;
    }

    public IReadOnlyList<Boid> GetManagedBoids()
    {
        return _managedBoids;
    }

    public IReadOnlyDictionary<Transform, BoidTargetInfo> GetKnownTargets()
    {
        return _knownTargets;
    }

    void Update()
    {
        CleanupNullBoids();

        if (Time.time - _lastDetectionTime >= _detectionInterval)
        {
            DetectTargets();
            _lastDetectionTime = Time.time;
        }

        if (Time.time - _lastAssignmentTime >= _reassignInterval)
        {
            AssignTargets();
            _lastAssignmentTime = Time.time;
        }

        UpdateTargetTracking();
    }

    private void CleanupNullBoids()
    {
        for (int i = _managedBoids.Count - 1; i >= 0; i--)
        {
            if (_managedBoids[i] == null)
            {
                _managedBoids.RemoveAt(i);
            }
        }

        var keysToRemove = new List<Boid>();
        foreach (var kvp in _boidAssignments)
        {
            if (kvp.Key == null)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
        {
            _boidAssignments.Remove(key);
        }
    }

    private void DetectTargets()
    {
        Vector3 center = Vector3.zero;
        int validCount = 0;
        foreach (var boid in _managedBoids)
        {
            if (boid != null)
            {
                center += boid.position;
                validCount++;
            }
        }
        if (validCount > 0)
        {
            center /= validCount;
        }
        else
        {
            center = transform.position;
        }

        GlobalHelper.Faction targetFactions = GetTargetFactions();

        CombatRegistry.GetNearbyEnemies(center, _detectionRadius, targetFactions, _nearbyEnemies);

        HashSet<Transform> currentTargets = new HashSet<Transform>();

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase vehicle = _nearbyEnemies[i];
            if (vehicle == null) continue;

            Transform target = vehicle.transform;

            if (IsIgnored(target)) continue;

            Boid boid = target.GetComponent<Boid>();
            if (boid != null && _managedBoids.Contains(boid)) continue;

            currentTargets.Add(target);

            if (!_knownTargets.ContainsKey(target))
            {
                _knownTargets[target] = new BoidTargetInfo
                {
                    Target = target,
                    AssignedBoidCount = 0
                };
            }

            var info = _knownTargets[target];
            info.LastSeenTime = Time.time;

            Vector3 newPos = target.position;
            if (info.LastKnownPosition != Vector3.zero)
            {
                info.EstimatedVelocity = (newPos - info.LastKnownPosition) / _detectionInterval;
            }
            info.LastKnownPosition = newPos;
            info.Distance = Vector3.Distance(center, newPos);
            info.ThreatLevel = CalculateThreatLevel(info, center);
        }

        var staleTargets = _knownTargets.Keys
            .Where(t => t == null || !currentTargets.Contains(t) && Time.time - _knownTargets[t].LastSeenTime > 5f)
            .ToList();

        foreach (var stale in staleTargets)
        {
            _knownTargets.Remove(stale);
        }
    }

    private float CalculateThreatLevel(BoidTargetInfo info, Vector3 flockCenter)
    {
        float threat = 0f;

        float normalizedDistance = Mathf.Clamp01(info.Distance / _detectionRadius);
        threat += (1f - normalizedDistance) * _distanceWeight;

        if (_managedBoids.Count > 0 && _managedBoids[0] != null)
        {
            Vector3 toTarget = (info.LastKnownPosition - flockCenter).normalized;
            Vector3 flockForward = _managedBoids[0].forward;
            float dot = Vector3.Dot(flockForward, toTarget);
            threat += (dot + 1f) * 0.5f * _angleWeight;
        }

        var targetWeapons = info.Target.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in targetWeapons)
        {
            if (weapon.Targeted != null)
            {
                foreach (var boid in _managedBoids)
                {
                    if (boid != null && weapon.Targeted == boid.transform)
                    {
                        threat += _targetingUsWeight;
                        break;
                    }
                }
            }
        }

        var vehicleInfo = info.Target.GetComponent<VehicleBase>();
        if (vehicleInfo != null)
        {
            float healthPercent = vehicleInfo.HitPoints / (float)vehicleInfo.MaxHitPoints;
            threat += (1f - healthPercent) * _healthWeight;
        }

        return threat;
    }

    private void AssignTargets()
    {
        var sortedTargets = _knownTargets.Values
            .Where(t => t.IsValid)
            .OrderByDescending(t => t.ThreatLevel)
            .Take(_maxTargets)
            .ToList();

        if (sortedTargets.Count == 0)
        {
            foreach (var boid in _managedBoids)
            {
                if (_boidAssignments.TryGetValue(boid, out var oldInfo) && oldInfo != null)
                {
                    oldInfo.AssignedBoidCount--;
                }
                _boidAssignments[boid] = null;
            }
            return;
        }

        foreach (var target in sortedTargets)
        {
            target.AssignedBoidCount = 0;
        }

        var boidsNeedingAssignment = _managedBoids
            .Where(b => b != null)
            .Select(b => new
            {
                Boid = b,
                NearestTarget = sortedTargets.OrderBy(t => Vector3.Distance(b.position, t.LastKnownPosition)).FirstOrDefault()
            })
            .OrderBy(x => x.NearestTarget != null ? Vector3.Distance(x.Boid.position, x.NearestTarget.LastKnownPosition) : float.MaxValue)
            .ToList();

        foreach (var item in boidsNeedingAssignment)
        {
            Boid boid = item.Boid;
            BoidTargetInfo bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var target in sortedTargets)
            {
                if (target.AssignedBoidCount >= _maxBoidsPerTarget) continue;

                float distance = Vector3.Distance(boid.position, target.LastKnownPosition);
                float score = target.ThreatLevel - (distance / _detectionRadius) * 0.5f;

                if (_boidAssignments.TryGetValue(boid, out var currentTarget) && currentTarget == target)
                {
                    score += 0.3f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = target;
                }
            }

            if (_boidAssignments.TryGetValue(boid, out var oldInfo) && oldInfo != null)
            {
                oldInfo.AssignedBoidCount--;
            }

            _boidAssignments[boid] = bestTarget;
            if (bestTarget != null)
            {
                bestTarget.AssignedBoidCount++;
            }
        }
    }

    private void UpdateTargetTracking()
    {
        foreach (var kvp in _knownTargets)
        {
            if (kvp.Value.IsValid && kvp.Value.Target != null)
            {
                Vector3 currentPos = kvp.Value.Target.position;
                kvp.Value.EstimatedVelocity = Vector3.Lerp(
                    kvp.Value.EstimatedVelocity,
                    (currentPos - kvp.Value.LastKnownPosition) / Time.deltaTime,
                    Time.deltaTime * 5f
                );
                kvp.Value.LastKnownPosition = currentPos;
            }
        }
    }

    public Vector3 GetInterceptPoint(Boid boid, float projectileSpeed = 0f)
    {
        if (!_boidAssignments.TryGetValue(boid, out var info) || info == null || !info.IsValid)
        {
            return boid.position + boid.forward * 100f;
        }

        if (projectileSpeed <= 0f)
        {
            return info.LastKnownPosition;
        }

        Vector3 toTarget = info.LastKnownPosition - boid.position;
        float distance = toTarget.magnitude;
        float timeToTarget = distance / projectileSpeed;

        return info.LastKnownPosition + info.EstimatedVelocity * timeToTarget;
    }

    private Transform GetVehicleRoot(Transform colliderTransform)
    {
        VehicleBase vehicle = colliderTransform.GetComponentInParent<VehicleBase>();
        if (vehicle != null)
        {
            return vehicle.transform;
        }

        Transform current = colliderTransform;
        while (current != null)
        {
            foreach (var tag in _targetTags)
            {
                if (current.CompareTag(tag))
                    return current;
            }
            current = current.parent;
        }

        return null;
    }

    private GlobalHelper.Faction GetTargetFactions()
    {
        switch (_team)
        {
            case GlobalHelper.Team.Player:
            case GlobalHelper.Team.Ally:
                return GlobalHelper.Faction.Foe;
            case GlobalHelper.Team.Foe:
                return GlobalHelper.Faction.Player | GlobalHelper.Faction.Ally;
            default:
                return GlobalHelper.Faction.Foe;
        }
    }

    #region Command Support Methods

    /// <summary>
    /// Set a priority target that overrides normal target assignment.
    /// </summary>
    public void SetPriorityTarget(Transform target)
    {
        _priorityTarget = target;
        
        if (_debugMode)
            Debug.Log($"[{_flockId}] Priority target set: {(target != null ? target.name : "null")}");
    }

    /// <summary>
    /// Clear the priority target.
    /// </summary>
    public void ClearPriorityTarget()
    {
        _priorityTarget = null;
        
        if (_debugMode)
            Debug.Log($"[{_flockId}] Priority target cleared");
    }

    /// <summary>
    /// Enable defense mode - only engage enemies within radius of defended target.
    /// </summary>
    public void SetDefenseMode(Transform defendTarget, float radius)
    {
        _defendTarget = defendTarget;
        _defendRadius = radius;
        _isDefenseMode = true;
        
        if (_debugMode)
            Debug.Log($"[{_flockId}] Defense mode enabled: {defendTarget?.name}, radius: {radius}");
    }

    /// <summary>
    /// Disable defense mode.
    /// </summary>
    public void ClearDefenseMode()
    {
        _defendTarget = null;
        _defendRadius = 0f;
        _isDefenseMode = false;
        
        if (_debugMode)
            Debug.Log($"[{_flockId}] Defense mode disabled");
    }

    /// <summary>
    /// Get the closest enemy within a radius of a position (used for defense mode).
    /// </summary>
    public Transform GetClosestEnemyInRadius(Vector3 center, float radius)
    {
        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var kvp in _knownTargets)
        {
            if (!kvp.Value.IsValid || kvp.Key == null) 
                continue;

            float dist = Vector3.Distance(center, kvp.Value.LastKnownPosition);
            if (dist <= radius && dist < closestDist)
            {
                closestDist = dist;
                closest = kvp.Key;
            }
        }

        return closest;
    }

    /// <summary>
    /// Get the default target for a boid based on normal assignment logic.
    /// </summary>
    public Transform GetDefaultTarget(Boid boid)
    {
        if (boid == null) return null;

        if (_boidAssignments.TryGetValue(boid, out var info) && info != null && info.IsValid)
        {
            return info.Target;
        }

        return null;
    }

    /// <summary>
    /// Get target for a boid, considering priority target, defense mode, and normal assignments.
    /// This is the main method boids should use to get their current target.
    /// </summary>
    public Transform GetTargetForBoid(Boid boid)
    {
        // Priority target takes precedence (e.g., from Attack command)
        if (_priorityTarget != null)
        {
            // Validate priority target still exists
            if (_priorityTarget.gameObject.activeInHierarchy)
                return _priorityTarget;
            else
                _priorityTarget = null; // Clear invalid priority target
        }

        // In defense mode, only target enemies within defense radius
        if (_isDefenseMode && _defendTarget != null)
        {
            Transform defenseTarget = GetClosestEnemyInRadius(_defendTarget.position, _defendRadius);
            if (defenseTarget != null)
                return defenseTarget;

            // No enemies in defense radius - no combat target
            return null;
        }

        // Default targeting logic - use assigned target
        return GetDefaultTarget(boid);
    }

    /// <summary>
    /// Get target info for a specific target transform.
    /// </summary>
    public BoidTargetInfo GetTargetInfoForTransform(Transform target)
    {
        if (target == null) return null;
        
        if (_knownTargets.TryGetValue(target, out var info))
        {
            return info;
        }
        
        return null;
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

        // Defense radius
        if (_isDefenseMode && _defendTarget != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(_defendTarget.position, _defendRadius);
        }

        // Priority target
        if (_priorityTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_priorityTarget.position, 30f);
            Gizmos.DrawLine(transform.position, _priorityTarget.position);
        }

        // Known targets
        if (_knownTargets != null)
        {
            foreach (var kvp in _knownTargets)
            {
                if (kvp.Value.IsValid)
                {
                    Gizmos.color = Color.Lerp(Color.yellow, Color.red, kvp.Value.ThreatLevel / 5f);
                    Gizmos.DrawWireSphere(kvp.Value.LastKnownPosition, 2f);
                    Gizmos.DrawLine(transform.position, kvp.Value.LastKnownPosition);
                }
            }
        }
    }
}