using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;
using static GlobalHelper;

public class BoidFlockTargetManager : MonoBehaviour
{
    private struct BoidAssignmentCandidate
    {
        public Boid Boid;
        public float DistanceToNearestTarget;

        public BoidAssignmentCandidate(Boid boid, float distanceToNearestTarget)
        {
            Boid = boid;
            DistanceToNearestTarget = distanceToNearestTarget;
        }
    }

    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5000f;
    [SerializeField] private float _detectionInterval = 0.2f;
    [SerializeField] private List<string> _targetTags = new List<string>();
    [SerializeField] private List<string> _ignoreTags = new List<string>();

    [Header("Target Distribution")]
    [FormerlySerializedAs("_maxTargets")]
    [SerializeField] private int _maxConcurrentCombatTargets = 2;
    [SerializeField] private int _maxBoidsPerTarget = 3;
    [SerializeField] private float _reassignInterval = 1f;
    [SerializeField] private float _targetAssignmentStickiness = 0.35f;
    [SerializeField] private float _focusFireBias = 0.75f;
    [SerializeField] private float _distancePenaltyWeight = 0.5f;
    [SerializeField] private bool _preserveAssignmentsUntilInvalid = true;

    [Header("Threat Evaluation")]
    [SerializeField] private float _distanceWeight = 1f;
    [SerializeField] private float _angleWeight = 0.5f;
    [SerializeField] private float _healthWeight = 0.3f;
    [SerializeField] private float _targetingUsWeight = 2f;

    [Header("Type-Aware Targeting")]
    [Tooltip("Optional priority matrix for per-type target preference. If null, all types score equally.")]
    [SerializeField] private TargetPriorityMatrix _targetPriorityMatrix;
    [Tooltip("Scale _maxBoidsPerTarget by target size tier (Large=2x, Medium=1.5x, Small=1x).")]
    [SerializeField] private bool _scaleCapByTargetSize = true;

    [Header("Flock Identity")]
    [SerializeField] private string _flockId = "";
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Neutral;

    [Header("Command State")]
    [SerializeField] private Transform _priorityTarget;
    [SerializeField] private Transform _commandAnchor;
    [SerializeField] private Transform _defendTarget;
    [SerializeField] private float _defendRadius;
    [SerializeField] private bool _isDefenseMode;

    public bool _debugMode = false;

    public string FlockId => _flockId;
    public GlobalHelper.Team Team => _team;
    public Transform PriorityTarget => _priorityTarget;
    public Transform CommandAnchor => _commandAnchor;
    public bool IsDefenseMode => _isDefenseMode;

    private Dictionary<Transform, BoidTargetInfo> _knownTargets = new Dictionary<Transform, BoidTargetInfo>();
    private Dictionary<Boid, BoidTargetInfo> _boidAssignments = new Dictionary<Boid, BoidTargetInfo>();
    private List<Boid> _managedBoids = new List<Boid>();
    private HashSet<Boid> _managedBoidSet = new HashSet<Boid>();

    private float _lastDetectionTime;
    private float _lastAssignmentTime;

    private List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    private HashSet<Transform> _currentTargets = new HashSet<Transform>();
    private List<Transform> _staleTargets = new List<Transform>(32);
    private List<Boid> _keysToRemove = new List<Boid>(16);
    private List<Boid> _unassignedBoids = new List<Boid>(64);
    private List<BoidTargetInfo> _candidateTargets = new List<BoidTargetInfo>(16);
    private List<BoidAssignmentCandidate> _assignmentCandidates = new List<BoidAssignmentCandidate>(64);

    // Fleet-level shared target pool (set by FleetController)
    private List<BoidTargetInfo> _fleetTargetPool;

    public IReadOnlyDictionary<Boid, BoidTargetInfo> BoidAssignments => _boidAssignments;

    /// <summary>
    /// Called by FleetController to push shared targets from other flocks.
    /// </summary>
    public void SetFleetTargetPool(List<BoidTargetInfo> pool)
    {
        _fleetTargetPool = pool;
    }

    public void SetTargetPriorityMatrix(TargetPriorityMatrix matrix)
    {
        _targetPriorityMatrix = matrix;
    }

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
        _commandAnchor = null;
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
        if (IsManagedBoid(boid)) return false;

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
    }

    private bool IsManagedBoid(Boid boid)
    {
        return boid != null && _managedBoidSet.Contains(boid);
    }

    private bool IsFormationLeader(Boid boid)
    {
        return boid != null && boid.FormationIndex == 0 && boid.FormationLeader == null;
    }

    public void RegisterBoid(Boid boid)
    {
        if (!_managedBoidSet.Contains(boid))
        {
            _managedBoids.Add(boid);
            _managedBoidSet.Add(boid);
            _boidAssignments[boid] = null;
        }
    }

    public void UnregisterBoid(Boid boid)
    {
        _managedBoids.Remove(boid);
        _managedBoidSet.Remove(boid);

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
                _managedBoidSet.Remove(_managedBoids[i]);
                _managedBoids.RemoveAt(i);
            }
        }

        _keysToRemove.Clear();
        foreach (var kvp in _boidAssignments)
        {
            if (kvp.Key == null)
                _keysToRemove.Add(kvp.Key);
        }
        for (int i = 0; i < _keysToRemove.Count; i++)
        {
            Boid key = _keysToRemove[i];
            _managedBoidSet.Remove(key);
            _boidAssignments.Remove(key);
        }
    }

    private Vector3 CalculateFlockCenter()
    {
        Vector3 center = Vector3.zero;
        int validCount = 0;

        for (int i = 0; i < _managedBoids.Count; i++)
        {
            Boid boid = _managedBoids[i];
            if (boid == null)
                continue;

            center += boid.position;
            validCount++;
        }

        if (validCount > 0)
            return center / validCount;

        return transform.position;
    }

    public Vector3 GetFlockCenterPosition()
    {
        return CalculateFlockCenter();
    }

    private void DetectTargets()
    {
        Vector3 center = CalculateFlockCenter();
        GlobalHelper.Faction targetFactions = GetTargetFactions();

        CombatRegistry.GetNearbyEnemies(center, _detectionRadius, targetFactions, _nearbyEnemies);

        _currentTargets.Clear();

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase vehicle = _nearbyEnemies[i];
            if (vehicle == null) continue;

            Transform target = vehicle.transform;

            if (IsIgnored(target)) continue;

            Boid boid = target.GetComponent<Boid>();
            if (IsManagedBoid(boid)) continue;

            _currentTargets.Add(target);
            UpdateKnownTarget(target, center);
        }

        RemoveStaleTargets();
    }

    private void UpdateKnownTarget(Transform target, Vector3 flockCenter)
    {
        if (!_knownTargets.TryGetValue(target, out var info))
        {
            info = new BoidTargetInfo
            {
                Target = target,
                AssignedBoidCount = 0
            };
            info.CachedWeapons = target.GetComponentsInChildren<WeaponBase>();
            info.CachedVehicle = target.GetComponent<VehicleBase>();

            // Cache VehicleType for type-aware targeting
            if (info.CachedVehicle != null)
            {
                info.TargetShipClass = info.CachedVehicle.VehicleType;
                info.TargetSizeTier = GlobalHelper.GetSizeTier(info.TargetShipClass);
            }
            else
            {
                info.TargetShipClass = GlobalHelper.VehicleType.Fighter;
                info.TargetSizeTier = GlobalHelper.ShipSizeTier.Small;
            }

            _knownTargets[target] = info;
        }

        info.LastSeenTime = Time.time;

        Vector3 newPos = target.position;
        if (info.LastKnownPosition != Vector3.zero)
        {
            info.EstimatedVelocity = (newPos - info.LastKnownPosition) / _detectionInterval;
        }

        info.LastKnownPosition = newPos;
        info.Distance = Vector3.Distance(flockCenter, newPos);
        info.ThreatLevel = CalculateThreatLevel(info, flockCenter);
    }

    private void RemoveStaleTargets()
    {
        _staleTargets.Clear();

        foreach (var kvp in _knownTargets)
        {
            Transform target = kvp.Key;
            if (target == null || (!_currentTargets.Contains(target) && Time.time - kvp.Value.LastSeenTime > 5f))
            {
                _staleTargets.Add(target);
            }
        }

        for (int i = 0; i < _staleTargets.Count; i++)
        {
            _knownTargets.Remove(_staleTargets[i]);
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

        var targetWeapons = info.CachedWeapons;
        if(targetWeapons != null)
        {
            foreach (var weapon in targetWeapons)
            {
                if (weapon.Targeted != null && weapon.Targeted != null)
                {
                    for (int i = 0; i < _managedBoids.Count; i++)
                    {
                        Boid boid = _managedBoids[i];
                        if (boid != null && weapon.Targeted == boid.transform)
                        {
                            threat += _targetingUsWeight;
                            break;
                        }
                    }
                }
            }
        }

        var vehicleInfo = info.CachedVehicle;
        if (vehicleInfo != null)
        {
            float healthPercent = vehicleInfo.HitPoints / (float)vehicleInfo.MaxHitPoints;
            threat += (1f - healthPercent) * _healthWeight;
        }

        return threat;
    }

    private void AssignTargets()
    {
        BuildCandidateTargets();

        if (_candidateTargets.Count == 0)
        {
            ClearAssignments();
            return;
        }

        for (int i = 0; i < _candidateTargets.Count; i++)
        {
            _candidateTargets[i].AssignedBoidCount = 0;
        }

        BuildAssignmentCandidates();
        _unassignedBoids.Clear();

        for (int i = 0; i < _assignmentCandidates.Count; i++)
        {
            Boid boid = _assignmentCandidates[i].Boid;
            if (boid == null)
                continue;

            if (_preserveAssignmentsUntilInvalid && TryKeepExistingAssignment(boid))
                continue;

            if (!TryAssignBestTarget(boid, true))
            {
                _unassignedBoids.Add(boid);
            }
        }

        for (int i = 0; i < _unassignedBoids.Count; i++)
        {
            TryAssignBestTarget(_unassignedBoids[i], false);
        }
    }

    private void BuildCandidateTargets()
    {
        _candidateTargets.Clear();

        foreach (var target in _knownTargets.Values)
        {
            if (target != null && target.IsValid)
            {
                _candidateTargets.Add(target);
            }
        }

        // Include fleet-shared targets that this flock hasn't detected itself
        if (_fleetTargetPool != null)
        {
            for (int i = 0; i < _fleetTargetPool.Count; i++)
            {
                var ft = _fleetTargetPool[i];
                if (ft != null && ft.IsValid && ft.Target != null && !_knownTargets.ContainsKey(ft.Target))
                {
                    _candidateTargets.Add(ft);
                }
            }
        }

        _candidateTargets.Sort((left, right) => right.ThreatLevel.CompareTo(left.ThreatLevel));

        if (_candidateTargets.Count > _maxConcurrentCombatTargets)
        {
            _candidateTargets.RemoveRange(_maxConcurrentCombatTargets, _candidateTargets.Count - _maxConcurrentCombatTargets);
        }
    }

    private void BuildAssignmentCandidates()
    {
        _assignmentCandidates.Clear();

        for (int i = 0; i < _managedBoids.Count; i++)
        {
            Boid boid = _managedBoids[i];
            if (boid == null)
                continue;

            _assignmentCandidates.Add(new BoidAssignmentCandidate(boid, GetNearestTargetDistance(boid)));
        }

        _assignmentCandidates.Sort((left, right) =>
        {
            bool leftIsLeader = IsFormationLeader(left.Boid);
            bool rightIsLeader = IsFormationLeader(right.Boid);

            if (leftIsLeader != rightIsLeader)
                return leftIsLeader ? -1 : 1;

            return left.DistanceToNearestTarget.CompareTo(right.DistanceToNearestTarget);
        });
    }

    private bool TryAssignBestTarget(Boid boid, bool respectPreferredCapacity)
    {
        if (boid == null)
            return false;

        BoidTargetInfo bestTarget = GetBestTargetForBoid(boid, respectPreferredCapacity);
        _boidAssignments[boid] = bestTarget;

        if (bestTarget == null)
            return false;

        bestTarget.AssignedBoidCount++;
        return true;
    }

    private float GetNearestTargetDistance(Boid boid)
    {
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < _candidateTargets.Count; i++)
        {
            float distance = Vector3.Distance(boid.position, _candidateTargets[i].LastKnownPosition);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
            }
        }

        return nearestDistance;
    }

    private float ScoreTarget(Boid boid, BoidTargetInfo target)
    {
        float distance = Vector3.Distance(boid.position, target.LastKnownPosition);
        float score = target.ThreatLevel - (distance / _detectionRadius) * _distancePenaltyWeight;

        if (target.AssignedBoidCount > 0)
        {
            score += target.AssignedBoidCount * _focusFireBias;
        }

        if (_boidAssignments.TryGetValue(boid, out var currentTarget) && currentTarget == target)
        {
            score += _targetAssignmentStickiness;
        }

        // Apply type-aware priority multiplier
        if (_targetPriorityMatrix != null)
        {
            score *= _targetPriorityMatrix.GetPriority(boid.ShipClass, target.TargetShipClass);
        }

        return score;
    }

    /// <summary>
    /// Returns the effective boid-per-target cap, scaled by target size tier if enabled.
    /// </summary>
    private int GetEffectiveMaxBoidsForTarget(BoidTargetInfo target)
    {
        if (!_scaleCapByTargetSize)
            return _maxBoidsPerTarget;

        float multiplier = GlobalHelper.GetSizeTierMultiplier(target.TargetSizeTier);
        return Mathf.Max(1, Mathf.RoundToInt(_maxBoidsPerTarget * multiplier));
    }

    private BoidTargetInfo GetBestTargetForBoid(Boid boid, bool respectPreferredCapacity)
    {
        BoidTargetInfo bestTarget = null;
        float bestScore = float.MinValue;

        for (int targetIndex = 0; targetIndex < _candidateTargets.Count; targetIndex++)
        {
            BoidTargetInfo target = _candidateTargets[targetIndex];
            int effectiveCap = GetEffectiveMaxBoidsForTarget(target);

            if (respectPreferredCapacity && target.AssignedBoidCount >= effectiveCap)
                continue;

            float score = ScoreTarget(boid, target);

            if (!respectPreferredCapacity && target.AssignedBoidCount >= effectiveCap)
            {
                int overflowCount = target.AssignedBoidCount - effectiveCap;
                score -= (overflowCount + 1) * Mathf.Max(0.5f, _focusFireBias + 0.5f);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private BoidTargetInfo GetLeaderFallbackTargetInfo(Boid boid)
    {
        if (!IsFormationLeader(boid))
            return null;

        BoidTargetInfo bestTarget = null;
        float bestScore = float.MinValue;

        foreach (var knownTarget in _knownTargets.Values)
        {
            if (knownTarget == null || !knownTarget.IsValid)
                continue;

            float score = ScoreTarget(boid, knownTarget);
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = knownTarget;
            }
        }

        return bestTarget;
    }

    private bool TryKeepExistingAssignment(Boid boid)
    {
        if (!_boidAssignments.TryGetValue(boid, out var currentTarget) || currentTarget == null || !currentTarget.IsValid)
            return false;

        if (!_candidateTargets.Contains(currentTarget))
            return false;

        if (currentTarget.AssignedBoidCount >= GetEffectiveMaxBoidsForTarget(currentTarget))
            return false;

        currentTarget.AssignedBoidCount++;
        _boidAssignments[boid] = currentTarget;
        return true;
    }

    private void ClearAssignments()
    {
        for (int i = 0; i < _managedBoids.Count; i++)
        {
            Boid boid = _managedBoids[i];
            if (boid == null)
                continue;

            if (_boidAssignments.TryGetValue(boid, out var oldInfo) && oldInfo != null)
            {
                oldInfo.AssignedBoidCount--;
            }

            _boidAssignments[boid] = null;
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

    public void SetCommandAnchor(Transform anchor)
    {
        _commandAnchor = anchor;
    }

    public bool TryGetCombatAnchorPosition(Boid boid, CombatAnchorMode anchorMode, out Vector3 anchorPosition)
    {
        switch (anchorMode)
        {
            case CombatAnchorMode.Leader:
                if (boid != null && boid.FormationLeader != null)
                {
                    anchorPosition = boid.FormationLeader.position;
                    return true;
                }
                break;

            case CombatAnchorMode.FlockCenter:
                anchorPosition = GetFlockCenterPosition();
                return true;

            case CombatAnchorMode.CommandAnchor:
                Transform anchor = _isDefenseMode && _defendTarget != null ? _defendTarget : _commandAnchor;
                if (anchor != null)
                {
                    anchorPosition = anchor.position;
                    return true;
                }
                break;
        }

        anchorPosition = Vector3.zero;
        return false;
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

        BoidTargetInfo leaderFallback = GetLeaderFallbackTargetInfo(boid);
        if (leaderFallback != null)
        {
            return leaderFallback.Target;
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

        // // Known targets
        // if (_knownTargets != null)
        // {
        //     foreach (var kvp in _knownTargets)
        //     {
        //         if (kvp.Value.IsValid)
        //         {
        //             Gizmos.color = Color.Lerp(Color.yellow, Color.red, kvp.Value.ThreatLevel / 5f);
        //             Gizmos.DrawWireSphere(kvp.Value.LastKnownPosition, 2f);
        //             Gizmos.DrawLine(transform.position, kvp.Value.LastKnownPosition);
        //         }
        //     }
        // }
    }
}