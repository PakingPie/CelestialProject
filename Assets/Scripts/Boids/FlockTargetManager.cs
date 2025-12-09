using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class FlockTargetManager : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5000f;
    [SerializeField] private float _detectionInterval = 0.2f;
    [SerializeField] private LayerMask _targetLayers;
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

    [Header("Detection")]
    [SerializeField] private int _maxOverlapResults = 256;

    [Header("Enemy Avoidance")]
    [SerializeField] private float _minEngagementDistance = 80f;
    [SerializeField] private float _preferredEngagementDistance = 150f;

    public bool _debugMode = false;

    public string FlockId => _flockId;
    public GlobalHelper.Team Team => _team;

    private Dictionary<Transform, TargetInfo> _knownTargets = new Dictionary<Transform, TargetInfo>();
    private Dictionary<Boid, TargetInfo> _boidAssignments = new Dictionary<Boid, TargetInfo>();
    private List<Boid> _managedBoids = new List<Boid>();

    private float _lastDetectionTime;
    private float _lastAssignmentTime;
    private Transform _flockCenter;

    private Collider[] _overlapResults;

    public IReadOnlyDictionary<Boid, TargetInfo> BoidAssignments => _boidAssignments;

    public void Initialize(string flockId,
    GlobalHelper.Team team,
    float detectionRadius,
    LayerMask targetLayers,
    int maxOverlapResults,
    float minEngagementDistance,
    float preferredEngagementDistance,
    List<string> targetTags,
    List<string> ignoreTags)
    {
        _flockId = flockId;
        _team = team;
        _detectionRadius = detectionRadius;
        _targetLayers = targetLayers;
        _maxOverlapResults = maxOverlapResults;
        _minEngagementDistance = minEngagementDistance;
        _preferredEngagementDistance = preferredEngagementDistance;
        _targetTags = targetTags ?? new List<string>();
        _ignoreTags = ignoreTags ?? new List<string>();
    }

    private bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        // Never target ignored tags (friendlies)
        foreach (var ignoreTag in _ignoreTags)
        {
            if (target.CompareTag(ignoreTag)) return false;
        }

        // Never target boids from our own flock
        Boid boid = target.GetComponent<Boid>();
        if (boid != null && _managedBoids.Contains(boid)) return false;

        // Check if it matches target tags
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
        // Check target and all parents for ignore tags
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
        _overlapResults = new Collider[_maxOverlapResults];
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

    public TargetInfo GetTargetInfo(Boid boid)
    {
        _boidAssignments.TryGetValue(boid, out var info);
        return info;
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
                var boid = _managedBoids[i];
                if (_boidAssignments.TryGetValue(boid, out var info) && info != null)
                {
                    info.AssignedBoidCount--;
                }
                _boidAssignments.Remove(boid);
                _managedBoids.RemoveAt(i);
            }
        }
    }

    private void DetectTargets()
    {
        // Update flock center
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

        // if (_debugMode)
        // {
        //     Debug.Log($"[{_flockId}] Scanning at center {center}, radius {_detectionRadius}");
        //     Debug.Log($"[{_flockId}] Target Layers: {_targetLayers.value}");
        //     Debug.Log($"[{_flockId}] Target Tags: {string.Join(", ", _targetTags)}");

        //     // Test without layer mask
        //     Collider[] testResults = Physics.OverlapSphere(center, _detectionRadius);
        //     Debug.Log($"[{_flockId}] Without layer filter: {testResults.Length} colliders found");
        // }
        // Detect potential targets
        int hitCount = Physics.OverlapSphereNonAlloc(center, _detectionRadius, _overlapResults, _targetLayers);

        // if (_debugMode)
        // {
        //     Debug.Log($"[{_flockId}] With layer filter: {hitCount} colliders found");
        // }

        HashSet<Transform> currentTargets = new HashSet<Transform>();

        for (int i = 0; i < hitCount; i++)
        {
            var collider = _overlapResults[i];
            if (collider == null) continue;

            // Get the root object with VehicleBase, not the collider's transform
            Transform target = GetVehicleRoot(collider.transform);
            if (target == null) continue;

            // Check if valid target
            if (!IsValidTarget(target)) continue;

            currentTargets.Add(target);

            if (!_knownTargets.ContainsKey(target))
            {
                _knownTargets[target] = new TargetInfo
                {
                    Target = target,
                    AssignedBoidCount = 0
                };
            }

            // Update target info
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

        // Remove stale targets
        var staleTargets = _knownTargets.Keys
            .Where(t => t == null || !currentTargets.Contains(t) && Time.time - _knownTargets[t].LastSeenTime > 5f)
            .ToList();

        foreach (var stale in staleTargets)
        {
            _knownTargets.Remove(stale);
        }
    }

    private float CalculateThreatLevel(TargetInfo info, Vector3 flockCenter)
    {
        float threat = 0f;

        // Distance factor (closer = more threatening)
        float normalizedDistance = Mathf.Clamp01(info.Distance / _detectionRadius);
        threat += (1f - normalizedDistance) * _distanceWeight;

        // Angle factor (targets in front are more important)
        if (_managedBoids.Count > 0 && _managedBoids[0] != null)
        {
            Vector3 toTarget = (info.LastKnownPosition - flockCenter).normalized;
            Vector3 flockForward = _managedBoids[0].forward;
            float dot = Vector3.Dot(flockForward, toTarget);
            threat += (dot + 1f) * 0.5f * _angleWeight;
        }

        // Check if target is targeting us
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

        // Health factor (low health = easier kill = higher priority)
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
        // Sort targets by threat level
        var sortedTargets = _knownTargets.Values
            .Where(t => t.IsValid)
            .OrderByDescending(t => t.ThreatLevel)
            .Take(_maxTargets)
            .ToList();

        if (sortedTargets.Count == 0)
        {
            // Clear all assignments
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

        // Reset assignment counts
        foreach (var target in sortedTargets)
        {
            target.AssignedBoidCount = 0;
        }

        // Get boids that need assignment, sorted by distance to nearest target
        var boidsNeedingAssignment = _managedBoids
            .Where(b => b != null)
            .Select(b => new
            {
                Boid = b,
                NearestTarget = sortedTargets.OrderBy(t => Vector3.Distance(b.position, t.LastKnownPosition)).FirstOrDefault()
            })
            .OrderBy(x => x.NearestTarget != null ? Vector3.Distance(x.Boid.position, x.NearestTarget.LastKnownPosition) : float.MaxValue)
            .ToList();

        // Assign targets
        foreach (var item in boidsNeedingAssignment)
        {
            Boid boid = item.Boid;
            TargetInfo bestTarget = null;
            float bestScore = float.MinValue;

            foreach (var target in sortedTargets)
            {
                if (target.AssignedBoidCount >= _maxBoidsPerTarget) continue;

                float distance = Vector3.Distance(boid.position, target.LastKnownPosition);
                float score = target.ThreatLevel - (distance / _detectionRadius) * 0.5f;

                // Prefer keeping current target
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

            // Clear old assignment
            if (_boidAssignments.TryGetValue(boid, out var oldInfo) && oldInfo != null)
            {
                oldInfo.AssignedBoidCount--;
            }

            // Assign new target
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
                // Update position tracking
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

    // Get predicted intercept point for a boid
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

        // Calculate intercept point
        Vector3 toTarget = info.LastKnownPosition - boid.position;
        float distance = toTarget.magnitude;
        float timeToTarget = distance / projectileSpeed;

        return info.LastKnownPosition + info.EstimatedVelocity * timeToTarget;
    }

    public Vector3 GetEngagementOffset(Boid boid, Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - boid.position;
        float distance = toTarget.magnitude;

        if (distance < _minEngagementDistance)
        {
            // Too close - back off
            float backoffStrength = 1f - (distance / _minEngagementDistance);
            return -toTarget.normalized * backoffStrength * _minEngagementDistance;
        }

        return Vector3.zero;
    }

    private Transform GetVehicleRoot(Transform colliderTransform)
    {
        // First try to get VehicleBase on this object or parents
        VehicleBase vehicle = colliderTransform.GetComponentInParent<VehicleBase>();
        if (vehicle != null)
        {
            return vehicle.transform;
        }

        // Fallback: walk up hierarchy looking for tagged object
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);

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


    // [SerializeField] private bool _debugMode = true;
    // void OnGUI()
    // {
    //     if (!_debugMode) return;

    //     // Offset based on flock ID hash to separate each manager's GUI
    //     int yOffset = Mathf.Abs(_flockId.GetHashCode()) % 4 * 80;

    //     GUILayout.BeginArea(new Rect(10, 10 + yOffset, 300, 75));
    //     GUILayout.Box($"[{_flockId}]");
    //     GUILayout.Label($"Known Targets: {_knownTargets.Count}");
    //     GUILayout.Label($"Managed Boids: {_managedBoids.Count}");

    //     int assigned = 0;
    //     foreach (var kvp in _boidAssignments)
    //     {
    //         if (kvp.Value != null) assigned++;
    //     }
    //     GUILayout.Label($"Assigned: {assigned}");
    //     GUILayout.EndArea();
    // }
}