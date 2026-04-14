using System.Collections.Generic;
using UnityEngine;
using static GlobalHelper;

public enum BoidMode
{
    Active,
    Docking,
    Docked,
    Parking,
    Parked,
    Launching
}

public class Boid : MonoBehaviour
{

    [Header("Command State")]
    private float _targetSpeed = -1f; // -1 means use default
    private Vector3? _moveTarget = null;
    private bool _holdingPosition = false;
    private Transform _priorityTarget = null;
    private Vector3 _spawnPosition;
    private bool _isDespawning = false;
    private System.Action _onDespawnArrived;

    // Moorage state
    private BoidMode _mode = BoidMode.Active;
    private Vector3 _moorageTarget;
    private System.Action _onMoorageArrived;
    private Vector3 _parkedDriftVelocity;
    private Quaternion _parkedTargetRotation;

    [Header("Debug")]
    [SerializeField] private Transform _target;

    private BoidSettings _settings;
    private Transform _cachedTransform;
    private Material _material;
    private BoidFlockTargetManager _targetManager;
    private Vector3 _velocity;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 forward;
    [HideInInspector] public Vector3 avgFlockHeading;
    [HideInInspector] public Vector3 avgAvoidanceHeading;
    [HideInInspector] public Vector3 flockmatesCenter;
    [HideInInspector] public int numPerceivedFlockmates;

    [Header("Movement Constraints")]
    [Tooltip("Height range for boid movement (Y axis)")]
    public Vector2 HeightRange = new Vector2(-100.0f, 100.0f);

    [Header("Leash Boundary")]
    [HideInInspector] public Vector3 LeashCenter;
    [HideInInspector] public bool UseLeash;


    private Transform _fallbackTarget;
    // Smoothing state
    private Vector3 _smoothedFlockHeading;
    private Vector3 _smoothedFlockCenter;
    private Vector3 _smoothedFormationTarget;
    private Vector3 _previousAcceleration;
    private float _smoothedSpeed;
    private Vector3 _smoothedTargetPosition;

    // Newtonian physics state
    private Vector3 _angularVelocity;
    private Quaternion _previousRotation;

    // Per-ship-class physics multipliers (cached from BoidSettings at init)
    private float _torqueMultiplier = 1f;
    private float _dragMultiplier = 1f;


    [Header("Debug")]
    [SerializeField] private bool _EnableDebugLogs = false;
    [SerializeField] private bool _EnableDebugGizmos = false;

    // Formation state
    [HideInInspector] public bool IsInCombat = false;
    [HideInInspector] public int FormationIndex = 0;
    [HideInInspector] public Boid FormationLeader = null;
    [HideInInspector] public CombatMorale CurrentMorale = CombatMorale.Confident;
    [HideInInspector] public bool IsParentFormationTier = false; // true = sub-flock leader following parent formation
    [HideInInspector] public bool IsSubFlockLeader = false;
    [HideInInspector] public Vector3 SubFlockCenter;
    private float _combatTimer = 0f;
    private bool _combatLeashEngaged = false;

    // Smoothing parameters
    private const float CollisionSmoothSpeed = 8f;
    private const float AvoidDirectionSmoothSpeed = 12f;
    private const float RotationSmoothSpeed = 6f;
    private const float SpeedSmoothSpeed = 5f;
    private const float FlockDataSmoothSpeed = 15f;
    private const float FormationTargetSmoothSpeed = 4f;
    private const float AccelerationSmoothSpeed = 8f;
    private const float FormationDeadZone = 10f;
    private const float FormationUrgencyRange = 100f;
    private const float TargetSmoothSpeed = 10f;
    private const float MinHeightRecoveryBuffer = 100f;

    private Vector3 _combatFacingDirection;
    private const float CombatRotationSpeed = 4f;

    // Wander state
    private Vector3 _wanderTarget;
    private float _wanderTimer;
    private const float WanderRadius = 2500f;
    private const float WanderInterval = 60f;

    // Auto-calculated collision bounds radius (surface clearance margin)
    private float _boundsRadius;

    // Ship type identity (cached from VehicleBase at init)
    private GlobalHelper.VehicleType _shipClass = GlobalHelper.VehicleType.Fighter;
    public GlobalHelper.VehicleType ShipClass => _shipClass;
    public GlobalHelper.ShipSizeTier SizeTier => GlobalHelper.GetSizeTier(_shipClass);
    public GlobalHelper.FormationZone FormationZone => GlobalHelper.GetFormationZone(_shipClass);

    public BoidSettings Settings => _settings;
    public Vector3 Velocity => _velocity;
    public Transform CurrentTarget => _target;

    private Vector3 _lastCombatHeading;
    private float _postCombatTimer = 0f;
    private const float PostCombatSteadyTime = 5f; // Hold heading for 5 seconds after combat
    private BoidAttackBehavior _attackBehavior;
    private BoidAttackBehavior AttackBehavior
    {
        get
        {
            if (_attackBehavior == null)
                _attackBehavior = GetComponent<BoidAttackBehavior>();
            return _attackBehavior;
        }
    }

    void Awake()
    {
        _cachedTransform = transform;
        _attackBehavior = GetComponent<BoidAttackBehavior>();
        var meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
            _material = meshRenderer.material;
    }

    void OnEnable()
    {
        if (BoidRegistry.Instance != null)
            BoidRegistry.Instance.RegisterBoid(this);
    }

    void OnDisable()
    {
        if (BoidRegistry.Instance != null)
            BoidRegistry.Instance.UnregisterBoid(this);
    }

    public void Initialize(BoidSettings settings, Transform fallbackTarget)
    {
        _settings = settings;
        _fallbackTarget = fallbackTarget;  // Store the fallback separately
        _target = fallbackTarget;

        // Cache ship type from VehicleBase (one-time lookup)
        var vehicle = GetComponent<VehicleBase>();
        if (vehicle != null)
            _shipClass = vehicle.VehicleType;

        position = _cachedTransform.position;
        forward = _cachedTransform.forward;

        float startSpeed = (_settings.minSpeed + _settings.maxSpeed) * 0.5f;
        _velocity = _cachedTransform.forward * startSpeed;
        _smoothedSpeed = startSpeed;

        _smoothedFlockHeading = forward;
        _smoothedFlockCenter = position;
        _smoothedFormationTarget = position;
        _smoothedTargetPosition = position + forward * 50f;
        _previousAcceleration = Vector3.zero;
        _angularVelocity = Vector3.zero;
        _previousRotation = _cachedTransform.rotation;
        InitializePhysicsMultipliers();
        _boundsRadius = CalculateBoundsRadius();
        _combatFacingDirection = forward;
        _wanderTarget = position + Random.insideUnitSphere * WanderRadius;
        _wanderTarget.y = Mathf.Clamp(_wanderTarget.y, HeightRange.x, HeightRange.y);
        _wanderTimer = Random.Range(0f, WanderInterval);
    }

    public void SetSpawnPosition(Vector3 pos)
    {
        _spawnPosition = pos;
    }

    public Vector3 SpawnPosition => _spawnPosition;
    public bool IsDespawning => _isDespawning;
    public BoidMode Mode => _mode;
    public bool IsDocked => _mode == BoidMode.Docked;
    public bool IsParked => _mode == BoidMode.Parked;
    public bool IsMoored => _mode == BoidMode.Docked || _mode == BoidMode.Parked;
    public bool IsTransitioning => _mode == BoidMode.Docking || _mode == BoidMode.Parking || _mode == BoidMode.Launching;

    public void BeginDespawn(System.Action onArrived = null)
    {
        _isDespawning = true;
        _onDespawnArrived = onArrived;
        IsInCombat = false;
        ClearPriorityTarget();
    }

    public void CancelDespawn()
    {
        _isDespawning = false;
        _onDespawnArrived = null;
    }

    #region Moorage

    /// <summary>
    /// Begin moorage approach toward target point.
    /// For carrier docking: callback fires for deactivation.
    /// For station parking: steers to target, then enters idle parked state.
    /// </summary>
    public void BeginMoorage(BoidMode mode, Vector3 targetPoint, System.Action onArrived = null)
    {
        _mode = mode;
        _moorageTarget = targetPoint;
        _onMoorageArrived = onArrived;
        IsInCombat = false;
        ClearPriorityTarget();
    }

    /// <summary>
    /// Immediately set boid into docked state (used for startDocked spawns).
    /// </summary>
    public void SetDocked()
    {
        _mode = BoidMode.Docked;
        IsInCombat = false;
        _velocity = Vector3.zero;
    }

    /// <summary>
    /// Immediately set boid into parked state (used for startDocked spawns).
    /// </summary>
    public void SetParked(float driftSpeed, Quaternion targetRotation)
    {
        _mode = BoidMode.Parked;
        IsInCombat = false;
        _velocity = Vector3.zero;
        _parkedDriftVelocity = Random.insideUnitSphere * driftSpeed;
        _parkedTargetRotation = targetRotation;
    }

    /// <summary>
    /// Launch from docked/parked state. Enters Launching mode briefly, then Active.
    /// </summary>
    public void Launch(Vector3 launchVelocity)
    {
        _mode = BoidMode.Launching;
        gameObject.SetActive(true);
        _velocity = launchVelocity;
        _smoothedSpeed = launchVelocity.magnitude;
        if (launchVelocity.sqrMagnitude > 0.01f)
        {
            forward = launchVelocity.normalized;
            _cachedTransform.rotation = Quaternion.LookRotation(forward);
        }
    }

    /// <summary>
    /// Abort docking/parking approach (e.g., attacked during approach) and return to active.
    /// </summary>
    public void AbortMoorage()
    {
        if (_mode == BoidMode.Docking || _mode == BoidMode.Parking)
        {
            _mode = BoidMode.Active;
            _onMoorageArrived = null;
        }
    }

    #endregion

    private void UpdateRegistryPosition()
    {
        if (BoidRegistry.Instance != null)
            BoidRegistry.Instance.UpdateBoidPosition(this);
    }

    public void SetTargetManager(BoidFlockTargetManager manager)
    {
        _targetManager = manager;
    }

    public void SetHeightRange(Vector2 heightRange)
    {
        HeightRange = heightRange;

        if (_cachedTransform != null)
        {
            Vector3 clampedPosition = _cachedTransform.position;
            clampedPosition.y = Mathf.Clamp(clampedPosition.y, HeightRange.x, HeightRange.y);
            _cachedTransform.position = clampedPosition;
            position = clampedPosition;
        }

        _wanderTarget.y = Mathf.Clamp(_wanderTarget.y, HeightRange.x, HeightRange.y);
        _smoothedTargetPosition.y = Mathf.Clamp(_smoothedTargetPosition.y, HeightRange.x, HeightRange.y);
        _smoothedFormationTarget.y = Mathf.Clamp(_smoothedFormationTarget.y, HeightRange.x, HeightRange.y);
    }

    public void UpdateTarget()
    {
        // Check boid-level priority target first (from direct command)
        if (_priorityTarget != null)
        {
            if (_target != _priorityTarget && AttackBehavior != null)
            {
                AttackBehavior.ResetSideCommitment();
            }
            _target = _priorityTarget;
            return;
        }

        if (_targetManager != null)
        {
            // Use the new method that respects priority targets and defense mode
            Transform assignedTarget = _targetManager.GetTargetForBoid(this);

            if (assignedTarget != null)
            {
                if (_target != assignedTarget && AttackBehavior != null)
                {
                    AttackBehavior.ResetSideCommitment();
                }
                _target = assignedTarget;
                return;
            }
        }

        // No combat target assigned - restore fallback target
        _target = _fallbackTarget;
    }

    public Vector3 GetTargetPosition()
    {
        if (_targetManager != null)
        {
            BoidTargetInfo info = _targetManager.GetTargetInfo(this);
            if (info != null && info.IsValid)
            {
                return info.LastKnownPosition;
            }
        }

        if (_target != null)
        {
            return _target.position;
        }

        return position + forward * 100f;
    }

    public Vector3 GetInterceptPosition(float projectileSpeed)
    {
        if (_targetManager != null)
        {
            return _targetManager.GetInterceptPoint(this, projectileSpeed);
        }
        return GetTargetPosition();
    }

    public void SetColor(Color color)
    {
        if (_material != null)
            _material.color = color;
    }

    public void EnterCombat()
    {
        if (IsInCombat) return;  // Already in combat, don't reset timer

        IsInCombat = true;
        _combatTimer = 0f;
        _combatLeashEngaged = false;
    }

    public void UpdateCombatState()
    {
        bool wasInCombat = IsInCombat;

        bool hasValidTarget = false;
        if (_targetManager != null)
        {
            BoidTargetInfo info = _targetManager.GetTargetInfo(this);
            hasValidTarget = info != null && info.Target && info.IsValid;
        }

        if (hasValidTarget)
        {
            IsInCombat = true;
            _combatTimer = 0f;
            _postCombatTimer = 0f;
            _lastCombatHeading = forward;
            return;
        }

        if (!IsInCombat) return;

        _combatTimer += Time.deltaTime;

        if (_combatTimer >= _settings.returnToFormationDelay)
        {
            IsInCombat = false;
            _combatLeashEngaged = false;
            _postCombatTimer = 0f;
            _lastCombatHeading = forward;

            if (wasInCombat && FormationLeader != null)
            {
                OnFormationChanged();
            }
        }
    }

    public Vector3 GetFormationOffset()
    {
        float spacingMultiplier = GetSizeTierSpacingMultiplier();

        if (_settings.useSubFlocks && !IsParentFormationTier)
        {
            // Sub-flock internal formation: use sub-flock formation type and tighter spacing
            return CalculateFormationOffset(FormationIndex, _settings.subFlockFormationType,
                _settings.subFlockFormationSpacing * spacingMultiplier);
        }
        return CalculateFormationOffset(FormationIndex, _settings.formationType,
            _settings.formationSpacing * spacingMultiplier);
    }

    /// <summary>
    /// Returns the spacing multiplier for this boid based on its ship size tier.
    /// </summary>
    private float GetSizeTierSpacingMultiplier()
    {
        if (_settings == null) return 1f;
        switch (SizeTier)
        {
            case GlobalHelper.ShipSizeTier.Large:  return _settings.capitalSpacingMultiplier;
            case GlobalHelper.ShipSizeTier.Medium: return _settings.escortSpacingMultiplier;
            default:                               return 1f;
        }
    }

    public static Vector3 CalculateFormationOffset(int index, FormationType type, float spacing)
    {
        switch (type)
        {
            case FormationType.V:
                return GetVFormationOffset(index, spacing);
            case FormationType.Line:
                return GetLineFormationOffset(index, spacing);
            case FormationType.Wedge:
                return GetWedgeFormationOffset(index, spacing);
            case FormationType.Box:
                return GetBoxFormationOffset(index, spacing);
            case FormationType.Circle:
                return GetCircleFormationOffset(index, spacing);
            case FormationType.Echelon:
                return GetEchelonFormationOffset(index, spacing);
            case FormationType.Sphere:
                return GetSphereFormationOffset(index, spacing);
            case FormationType.Helix:
                return GetHelixFormationOffset(index, spacing);
            case FormationType.Wall:
                return GetWallFormationOffset(index, spacing);
            default:
                return Vector3.zero;
        }
    }

    private static Vector3 GetVFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        // Add vertical stagger - alternating up/down
        float yOffset = (row % 2 == 0) ? spacing * 0.3f : -spacing * 0.3f;

        return new Vector3(side * row * spacing, yOffset, -row * spacing);
    }

    private static Vector3 GetLineFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int pos = (index + 1) / 2;

        // Stagger vertically
        float yOffset = (index % 2 == 0) ? spacing * 0.2f : -spacing * 0.2f;

        return new Vector3(side * pos * spacing, yOffset, 0f);
    }

    private static Vector3 GetWedgeFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        float yOffset = (row % 2 == 0) ? spacing * 0.25f : -spacing * 0.25f;

        return new Vector3(side * row * spacing * 0.5f, yOffset, -row * spacing);
    }

    private static Vector3 GetBoxFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int adjustedIndex = index - 1;

        // Calculate cube dimensions
        int totalFollowers = index; // Approximate for sizing
        int cubeSize = Mathf.Max(2, Mathf.CeilToInt(Mathf.Pow(totalFollowers, 1f / 3f)));

        int x = adjustedIndex % cubeSize;
        int y = (adjustedIndex / cubeSize) % cubeSize;
        int z = adjustedIndex / (cubeSize * cubeSize);

        // Center horizontally and vertically, place behind leader
        float halfWidth = (cubeSize - 1) / 2f;

        float offsetX = (x - halfWidth) * spacing;
        float offsetY = (y - halfWidth) * spacing;
        float offsetZ = -(z + 1) * spacing; // Always behind leader

        return new Vector3(offsetX, offsetY, offsetZ);
    }

    private static Vector3 GetCircleFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Spherical layers
        int layer = 1;
        int layerStart = 1;
        int boidsPerLayer = 8;

        while (index >= layerStart + boidsPerLayer * layer)
        {
            layerStart += boidsPerLayer * layer;
            layer++;
        }

        int indexInLayer = index - layerStart;
        int totalInLayer = boidsPerLayer * layer;

        float theta = (indexInLayer / (float)totalInLayer) * Mathf.PI * 2f; // Horizontal angle
        float phi = Mathf.PI * 0.5f + (layer % 2 == 0 ? 0.3f : -0.3f);      // Vertical angle
        float radius = spacing * layer;

        return new Vector3(
            Mathf.Sin(theta) * Mathf.Sin(phi) * radius,
            Mathf.Cos(phi) * radius * 0.5f,
            Mathf.Cos(theta) * Mathf.Sin(phi) * radius
        );
    }

    private static Vector3 GetEchelonFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Diagonal climb/descent
        float yOffset = index * spacing * 0.3f;

        return new Vector3(index * spacing * 0.7f, yOffset, -index * spacing);
    }

    private static Vector3 GetSphereFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Fibonacci sphere distribution for even spacing
        float goldenRatio = (1f + Mathf.Sqrt(5f)) / 2f;
        float theta = 2f * Mathf.PI * index / goldenRatio;
        float phi = Mathf.Acos(1f - 2f * (index + 0.5f) / (index + 10));

        float radius = spacing * Mathf.Ceil(index / 8f);

        return new Vector3(
            Mathf.Cos(theta) * Mathf.Sin(phi) * radius,
            Mathf.Cos(phi) * radius,
            Mathf.Sin(theta) * Mathf.Sin(phi) * radius
        );
    }

    private static Vector3 GetHelixFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        float angle = index * 0.5f;
        float radius = spacing;
        float yOffset = index * spacing * 0.4f;

        return new Vector3(
            Mathf.Cos(angle) * radius,
            yOffset,
            Mathf.Sin(angle) * radius - index * spacing * 0.5f
        );
    }

    private static Vector3 GetWallFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Vertical grid behind leader
        int columns = 5;
        int x = index % columns;
        int y = index / columns;

        float offsetX = (x - columns / 2f) * spacing;
        float offsetY = (y - 1) * spacing;

        return new Vector3(offsetX, offsetY, -spacing);
    }

    public void UpdateBoid()
    {
        // Handle moorage states first
        if (_mode == BoidMode.Parked)
        {
            UpdateParkedDrift();
            return;
        }
        if (_mode == BoidMode.Docked)
            return; // Shouldn't be called, but safety check

        if (_mode == BoidMode.Docking || _mode == BoidMode.Parking)
        {
            // Update combat detection so boids can abort approach if attacked
            UpdateTarget();
            UpdateCombatState();

            if (IsInCombat)
            {
                AbortMoorage();
                // Fall through to normal active behavior below
            }
            else
            {
                if (TryHandleMoorageApproach())
                    return;
            }
        }

        if (_mode == BoidMode.Launching)
        {
            UpdateLaunching();
            return;
        }

        UpdateTarget();
        UpdateCombatState();

        if (TryHandleDespawnMovement())
            return;

        if (TryHandleHoldPosition())
            return;

        UpdatePostCombatTimer();
        UpdateSmoothedFlockData();

        Vector3 acceleration = CalculatePrimaryAcceleration();
        ApplyFlockingAcceleration(ref acceleration);
        ApplyLocalAvoidance(ref acceleration);
        ApplyHeightBoundaryRecovery(ref acceleration);
        ApplyLeashBoundary(ref acceleration);
        ApplyMovement(acceleration);

        UpdateRegistryPosition();
    }

    private bool TryHandleMoorageApproach()
    {
        float dist = Vector3.Distance(position, _moorageTarget);
        if (dist < 50f)
        {
            _onMoorageArrived?.Invoke();
            _onMoorageArrived = null;
            return true;
        }

        Vector3 acceleration = SteerTowards(_moorageTarget - position) * _settings.targetWeight * 2f;
        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(obstacleAvoidance) * _settings.obstacleAvoidanceWeight;
        }

        ApplyHeightBoundaryRecovery(ref acceleration);

        _velocity += acceleration * Time.deltaTime;

        float effectiveDrag = _settings.linearDrag * _dragMultiplier;
        if (effectiveDrag > 0f)
            _velocity *= 1f / (1f + effectiveDrag * Time.deltaTime);

        float speed = _velocity.magnitude;
        if (speed > 0.001f)
        {
            float targetSpeed = Mathf.Clamp(speed, _settings.minSpeed, _settings.maxSpeed);
            if (speed > targetSpeed * 1.5f)
                _velocity = _velocity / speed * targetSpeed * 1.5f;

            _smoothedSpeed = _velocity.magnitude;

            Vector3 direction = _velocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            ApplyRotationalPhysics(targetRotation, Time.deltaTime);
            ApplyVelocityCoupling(Time.deltaTime);

            Vector3 newPos = _cachedTransform.position + _velocity * Time.deltaTime;
            ClampPositionToHeightRange(ref newPos);
            _cachedTransform.position = newPos;
            position = newPos;
            forward = _cachedTransform.forward;
        }

        UpdateRegistryPosition();
        return true;
    }

    private void UpdateParkedDrift()
    {
        // Gentle random drift while parked
        Vector3 newPos = _cachedTransform.position + _parkedDriftVelocity * Time.deltaTime;
        ClampPositionToHeightRange(ref newPos);
        _cachedTransform.position = newPos;
        position = newPos;

        // Smoothly rotate toward parked orientation
        _cachedTransform.rotation = Quaternion.Slerp(_cachedTransform.rotation, _parkedTargetRotation, Time.deltaTime * 0.5f);
        forward = _cachedTransform.forward;
    }

    private void UpdateLaunching()
    {
        // Immediately transition to Active — boids already have velocity from Launch()
        _mode = BoidMode.Active;
    }

    private bool TryHandleDespawnMovement()
    {
        if (!_isDespawning)
            return false;

        if (Vector3.Distance(position, _spawnPosition) < 50f)
        {
            _onDespawnArrived?.Invoke();
            return true;
        }

        Vector3 acceleration = SteerTowards(_spawnPosition - position) * _settings.targetWeight * 2f;
        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(obstacleAvoidance) * _settings.obstacleAvoidanceWeight;
        }

        ApplyHeightBoundaryRecovery(ref acceleration);

        // Apply steering forces to velocity
        _velocity += acceleration * Time.deltaTime;

        // Linear drag
        float effectiveDrag = _settings.linearDrag * _dragMultiplier;
        if (effectiveDrag > 0f)
            _velocity *= 1f / (1f + effectiveDrag * Time.deltaTime);

        float speed = _velocity.magnitude;
        if (speed > 0.001f)
        {
            // Speed limits
            float targetSpeed = Mathf.Clamp(speed, _settings.minSpeed, _settings.maxSpeed);
            if (speed > targetSpeed * 1.5f)
                _velocity = _velocity / speed * targetSpeed * 1.5f;

            _smoothedSpeed = _velocity.magnitude;

            // Rotational physics
            Vector3 direction = _velocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            ApplyRotationalPhysics(targetRotation, Time.deltaTime);

            // Velocity coupling
            ApplyVelocityCoupling(Time.deltaTime);

            // Position update
            Vector3 newPos = _cachedTransform.position + _velocity * Time.deltaTime;
            ClampPositionToHeightRange(ref newPos);
            _cachedTransform.position = newPos;
            position = newPos;
            forward = _cachedTransform.forward;
        }

        UpdateRegistryPosition();
        return true;
    }

    private bool TryHandleHoldPosition()
    {
        if (!_holdingPosition || !_moveTarget.HasValue)
            return false;

        Vector3 toHoldPos = _moveTarget.Value - position;
        float distanceToHold = toHoldPos.magnitude;
        Vector3 acceleration = Vector3.zero;

        if (distanceToHold > 20f)
        {
            acceleration = SteerTowards(toHoldPos) * _settings.targetWeight;
        }
        else if (_velocity.sqrMagnitude > 1f)
        {
            acceleration = -_velocity.normalized * _settings.maxSteerForce * 2f;
        }

        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(obstacleAvoidance) * _settings.obstacleAvoidanceWeight;
        }

        Vector3 boidSeparation = CalculateBoidSeparation();
        if (boidSeparation.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(boidSeparation) * _settings.separateWeight * 0.5f;
        }

        ApplyHeightBoundaryRecovery(ref acceleration);

        // Apply steering forces to velocity
        _velocity += acceleration * Time.deltaTime;

        // Linear drag (stronger for hold position)
        float effectiveDrag = _settings.linearDrag * _dragMultiplier;
        float holdDragBoost = distanceToHold <= 20f ? 3f : 1.5f;
        if (effectiveDrag > 0f)
            _velocity *= 1f / (1f + effectiveDrag * holdDragBoost * Time.deltaTime);

        float targetSpeed = distanceToHold > 20f ? _settings.maxSpeed * 0.5f : _settings.minSpeed * 0.5f;
        float speed = _velocity.magnitude;

        if (speed > 0.001f)
        {
            // Soft speed cap for approach
            if (speed > targetSpeed * 1.5f)
                _velocity = _velocity / speed * targetSpeed * 1.5f;

            _smoothedSpeed = _velocity.magnitude;

            // Rotational physics (gentler for hold)
            Quaternion targetRotation = Quaternion.LookRotation(forward);
            ApplyRotationalPhysics(targetRotation, Time.deltaTime);

            // Velocity coupling
            ApplyVelocityCoupling(Time.deltaTime);

            // Position update
            Vector3 newPos = _cachedTransform.position + _velocity * Time.deltaTime;
            ClampPositionToHeightRange(ref newPos);
            _cachedTransform.position = newPos;
            position = newPos;
        }

        UpdateRegistryPosition();
        return true;
    }

    private void UpdatePostCombatTimer()
    {
        if (!IsInCombat && _postCombatTimer < PostCombatSteadyTime)
        {
            _postCombatTimer += Time.deltaTime;
        }
    }

    private void UpdateSmoothedFlockData()
    {
        _smoothedFlockHeading = Vector3.Lerp(_smoothedFlockHeading, avgFlockHeading, Time.deltaTime * FlockDataSmoothSpeed);
        _smoothedFlockCenter = Vector3.Lerp(_smoothedFlockCenter, flockmatesCenter, Time.deltaTime * FlockDataSmoothSpeed);
    }

    private Vector3 CalculatePrimaryAcceleration()
    {
        if (_settings.useFormation && !IsInCombat)
        {
            if (FormationIndex == 0 && FormationLeader == null)
                return CalculateLeaderAcceleration();

            // Sub-flock leaders: leader-style AI + tether to parent formation slot
            if (IsSubFlockLeader && IsParentFormationTier && FormationLeader != null)
                return CalculateSubFlockLeaderAcceleration();

            if (FormationLeader != null && FormationIndex > 0)
                return CalculateFormationAcceleration();
        }

        // Broken morale: always flee regardless of IsInCombat state
        if (_settings.useAdaptiveMorale && CurrentMorale == CombatMorale.Broken)
        {
            return CalculateFleeAcceleration();
        }

        if (IsInCombat)
        {
            // Confident morale: blend formation into combat
            if (_settings.useAdaptiveMorale && CurrentMorale == CombatMorale.Confident
                && _settings.useFormation && FormationLeader != null && FormationIndex > 0
                && AttackBehavior != null && AttackBehavior.Profile != null && _target != null)
            {
                float fw = _settings.confidentFormationWeight;
                Vector3 formAcc = CalculateFormationAcceleration();
                Vector3 combatAcc = CalculateCombatAcceleration();
                return Vector3.Lerp(combatAcc, formAcc, fw);
            }

            // Cautious morale (or non-adaptive): pure combat
            if (AttackBehavior != null && AttackBehavior.Profile != null && _target != null)
            {
                return CalculateCombatAcceleration();
            }
        }

        return CalculateTravelAcceleration();
    }

    private Vector3 CalculateFleeAcceleration()
    {
        Vector3 acceleration = Vector3.zero;
        Vector3 fleeDir = Vector3.zero;

        // Flee from nearest known enemy (not _target, which may be fallback/leader)
        Vector3? nearestEnemy = _targetManager?.GetNearestEnemyPosition(position);
        if (nearestEnemy.HasValue)
        {
            fleeDir = (position - nearestEnemy.Value).normalized;
        }
        else if (_target != null)
        {
            fleeDir = (position - _target.position).normalized;
        }
        else
        {
            fleeDir = forward;
        }

        acceleration += SteerTowards(fleeDir) * _settings.targetWeight * 2f;

        // Apply speed boost
        float fleeSpeed = _settings.maxSpeed * _settings.fleeSpeedMultiplier;
        float currentSpeed = _velocity.magnitude;
        if (currentSpeed > 0.01f && currentSpeed < fleeSpeed)
        {
            _velocity = _velocity.normalized * Mathf.Lerp(currentSpeed, fleeSpeed, Time.deltaTime * 3f);
        }

        return acceleration;
    }

    private Vector3 CalculateCombatAcceleration()
    {
        float discipline = GetCombatDisciplineMultiplier();
        Vector3 acceleration = Vector3.zero;

        if (_target != null)
        {
            Vector3 movementDir = AttackBehavior.GetDesiredMovementDirection(_target.position, _target.forward);
            float speedMult = AttackBehavior.SpeedMultiplier;
            _velocity *= Mathf.Lerp(1f, speedMult, Time.deltaTime * 3f);

            float pursuitWeight = _settings.combatTargetPursuitWeight * discipline;
            float targetBehindFactor = Mathf.Clamp01(-Vector3.Dot(forward, movementDir));
            if (targetBehindFactor > 0f)
            {
                float turnBoost = FormationIndex == 0
                    ? Mathf.Lerp(1f, 3f, targetBehindFactor)
                    : Mathf.Lerp(1f, 1.75f, targetBehindFactor);
                pursuitWeight *= turnBoost;
            }

            acceleration += SteerTowards(movementDir) * pursuitWeight;
        }
        else
        {
            acceleration += CalculateTravelAcceleration();
        }

        ApplyCombatAnchorAcceleration(ref acceleration, discipline);
        return acceleration;
    }

    private float GetCombatDisciplineMultiplier()
    {
        if (AttackBehavior != null && AttackBehavior.Profile != null)
            return Mathf.Max(0f, AttackBehavior.Profile.squadDisciplineMultiplier);

        return 1f;
    }

    private void ApplyCombatAnchorAcceleration(ref Vector3 acceleration, float discipline)
    {
        if (_targetManager == null)
            return;

        Vector3 anchorPosition;
        bool hasAnchor;

        // When sub-flocks are active, remap anchor modes to sub-flock scope
        if (_settings.useSubFlocks)
        {
            hasAnchor = TryGetSubFlockAnchorPosition(out anchorPosition);
        }
        else
        {
            hasAnchor = _targetManager.TryGetCombatAnchorPosition(this, _settings.combatAnchorMode, out anchorPosition);
        }

        if (!hasAnchor)
            return;

        float slackRadius = Mathf.Max(0f, _settings.combatAnchorSlackRadius);
        float leashRadius = Mathf.Max(slackRadius, _settings.combatLeashRadius);

        // Cautious morale: tighten engagement range
        if (_settings.useAdaptiveMorale && CurrentMorale == CombatMorale.Cautious)
        {
            slackRadius *= _settings.cautiousEngageRangeMult;
            leashRadius *= _settings.cautiousEngageRangeMult;
        }

        float hysteresis = Mathf.Max(0f, _settings.combatRegroupHysteresis);
        float distanceToAnchor = Vector3.Distance(position, anchorPosition);

        if (_combatLeashEngaged)
        {
            float releaseRadius = Mathf.Max(slackRadius, leashRadius - hysteresis);
            if (distanceToAnchor <= releaseRadius)
            {
                _combatLeashEngaged = false;
            }
        }
        else if (distanceToAnchor > leashRadius)
        {
            _combatLeashEngaged = true;
        }

        if (_combatLeashEngaged)
        {
            acceleration += SteerTowards(anchorPosition - position) * _settings.combatLeashWeight * discipline;
            return;
        }

        if (distanceToAnchor > slackRadius)
        {
            float normalizedDistance = leashRadius > slackRadius
                ? Mathf.InverseLerp(slackRadius, leashRadius, distanceToAnchor)
                : 1f;

            float anchorWeight = Mathf.Lerp(0f, _settings.combatAnchorWeight, normalizedDistance) * discipline;
            acceleration += SteerTowards(anchorPosition - position) * anchorWeight;
        }

        ApplyCombatSlotRetention(ref acceleration, discipline);
    }

    private void ApplyCombatSlotRetention(ref Vector3 acceleration, float discipline)
    {
        if (FormationLeader == null || FormationIndex <= 0)
            return;

        float slotRetention = Mathf.Clamp01(_settings.combatSlotRetention) * discipline;
        if (slotRetention <= 0f)
            return;

        Vector3 slotTarget = FormationLeader.position + FormationLeader._cachedTransform.TransformDirection(GetFormationOffset());
        Vector3 toSlot = slotTarget - position;

        if (toSlot.sqrMagnitude <= _settings.formationDeadZone * _settings.formationDeadZone)
            return;

        acceleration += SteerTowards(toSlot) * slotRetention;
    }

    private bool TryGetSubFlockAnchorPosition(out Vector3 anchorPosition)
    {
        switch (_settings.combatAnchorMode)
        {
            case CombatAnchorMode.Leader:
                // Anchor to sub-flock leader instead of flock leader
                if (FormationLeader != null)
                {
                    anchorPosition = FormationLeader.position;
                    return true;
                }
                break;

            case CombatAnchorMode.FlockCenter:
                // Use sub-flock center of mass instead of whole-flock center
                anchorPosition = SubFlockCenter;
                return true;

            case CombatAnchorMode.CommandAnchor:
                // Command anchor stays global — pass through to target manager
                return _targetManager.TryGetCombatAnchorPosition(this, CombatAnchorMode.CommandAnchor, out anchorPosition);
        }

        anchorPosition = Vector3.zero;
        return false;
    }

    private Vector3 CalculateLeaderAcceleration()
    {
        Vector3 acceleration = Vector3.zero;

        if (_target != null)
        {
            Vector3 chaseAcceleration = GetTargetChaseAcceleration();
            if (chaseAcceleration != Vector3.zero)
            {
                acceleration = chaseAcceleration;
                ApplyLeaderSpeedThrottle();
                return acceleration;
            }
        }

        if (_postCombatTimer < PostCombatSteadyTime)
        {
            acceleration = SteerTowards(_lastCombatHeading) * _settings.targetWeight * 0.5f;
            ApplyLeaderSpeedThrottle();
            return acceleration;
        }

        acceleration = GetWanderForce() * _settings.targetWeight;
        ApplyLeaderSpeedThrottle();
        return acceleration;
    }

    private void ApplyLeaderSpeedThrottle()
    {
        if (!_settings.useFormation || numPerceivedFlockmates <= 0)
            return;

        // Use flock center from compute shader to measure how far followers are lagging
        Vector3 flockCenter = _smoothedFlockCenter / numPerceivedFlockmates;
        float distToFlock = Vector3.Distance(position, flockCenter);

        // Start throttling when followers are more than 1 spacing behind
        float throttleStart = _settings.formationSpacing * 1.5f;
        float throttleMax = _settings.formationSpacing * 4f;

        if (distToFlock > throttleStart)
        {
            float throttle = Mathf.Clamp01((distToFlock - throttleStart) / (throttleMax - throttleStart));
            float minSpeedRatio = 0.4f;
            float targetSpeed = Mathf.Lerp(_settings.maxSpeed, _settings.minSpeed * minSpeedRatio, throttle);
            float currentSpeed = _velocity.magnitude;

            if (currentSpeed > targetSpeed)
            {
                _velocity = _velocity.normalized * Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * SpeedSmoothSpeed);
            }
        }
    }

    private Vector3 CalculateSubFlockLeaderAcceleration()
    {
        // Leader-style AI: wander/chase/travel
        Vector3 leaderAccel = CalculateLeaderAcceleration();

        // Tether toward parent formation slot to keep sub-flocks in the general area
        if (FormationLeader != null)
        {
            Vector3 formationOffset = GetFormationOffset();
            Vector3 slotTarget = FormationLeader.position +
                FormationLeader._cachedTransform.TransformDirection(formationOffset);

            Vector3 toSlot = slotTarget - position;
            float distToSlot = toSlot.magnitude;

            // Gentle tether: only pulls when drifting far from the parent slot
            float tetherStart = _settings.formationSpacing * 1.5f;
            float tetherMax = _settings.formationSpacing * 4f;

            if (distToSlot > tetherStart)
            {
                float urgency = Mathf.Clamp01((distToSlot - tetherStart) / (tetherMax - tetherStart));
                // Blend: at max urgency, prioritize tether over leader AI
                Vector3 tetherForce = SteerTowards(toSlot) * _settings.formationTightness * urgency * 0.5f;
                leaderAccel += tetherForce;
            }

            // Match the flock leader's velocity loosely
            Vector3 leaderVel = FormationLeader.Velocity;
            if (leaderVel.sqrMagnitude > 0.01f)
            {
                leaderAccel += SteerTowards(leaderVel) * _settings.formationMatchSpeed * 0.3f;
            }
        }

        return leaderAccel;
    }

    private Vector3 CalculateTravelAcceleration()
    {
        if (_target != null)
        {
            Vector3 targetPos = _target.position;
            _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, targetPos, Time.deltaTime * TargetSmoothSpeed);
            return SteerTowards(_smoothedTargetPosition - position) * _settings.targetWeight;
        }

        return GetWanderForce() * _settings.targetWeight;
    }

    private Vector3 GetTargetChaseAcceleration()
    {
        Vector3 targetPos = _target.position;
        _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, targetPos, Time.deltaTime * TargetSmoothSpeed);
        Vector3 offsetToTarget = _smoothedTargetPosition - position;

        if (offsetToTarget.sqrMagnitude <= 25f)
            return Vector3.zero;

        return SteerTowards(offsetToTarget) * _settings.targetWeight;
    }

    private void ApplyFlockingAcceleration(ref Vector3 acceleration)
    {
        if (numPerceivedFlockmates <= 0)
            return;

        Vector3 centerOfMass = _smoothedFlockCenter / numPerceivedFlockmates;
        Vector3 offsetToCenter = centerOfMass - position;

        float alignMult = IsInCombat ? _settings.combatAlignmentMultiplier : 1f;
        float cohesionMult = IsInCombat ? _settings.combatCohesionMultiplier : 1f;

        // Adaptive morale overrides during combat
        if (IsInCombat && _settings.useAdaptiveMorale)
        {
            switch (CurrentMorale)
            {
                case CombatMorale.Confident:
                    // Near-normal flocking — stay together
                    alignMult = Mathf.Lerp(_settings.combatAlignmentMultiplier, 1f, _settings.confidentFormationWeight);
                    cohesionMult = Mathf.Lerp(_settings.combatCohesionMultiplier, 1f, _settings.confidentFormationWeight);
                    break;
                case CombatMorale.Broken:
                    // Scatter — no cohesion, no alignment
                    alignMult = 0f;
                    cohesionMult = 0f;
                    break;
                case CombatMorale.Cautious:
                    // Loosen up — less cohesion, keep some alignment
                    alignMult = _settings.combatAlignmentMultiplier * 0.5f;
                    cohesionMult = _settings.combatCohesionMultiplier * 0.5f;
                    break;
            }
        }

        if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
        {
            alignMult *= 0.1f;
            cohesionMult *= 0.05f;
        }

        if (_settings.useFormation && !IsInCombat && FormationIndex == 0)
        {
            cohesionMult *= 0.1f;
        }

        acceleration += SteerTowards(_smoothedFlockHeading) * _settings.alignWeight * alignMult;
        acceleration += SteerTowards(offsetToCenter) * _settings.cohesionWeight * cohesionMult;
    }

    private void ApplyLocalAvoidance(ref Vector3 acceleration)
    {
        Vector3 boidSeparation = CalculateBoidSeparation();
        if (boidSeparation.sqrMagnitude > 0.01f)
        {
            float sepWeight = _settings.separateWeight;
            if (IsInCombat)
            {
                sepWeight *= _settings.combatSeparationMultiplier;
                if (_settings.useAdaptiveMorale && CurrentMorale == CombatMorale.Cautious)
                    sepWeight *= _settings.cautiousSeparationMult;
            }
            acceleration += SteerTowards(boidSeparation) * sepWeight;
        }

        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(obstacleAvoidance) * _settings.obstacleAvoidanceWeight;
        }
    }

    private void InitializePhysicsMultipliers()
    {
        switch (SizeTier)
        {
            case GlobalHelper.ShipSizeTier.Large:
                _torqueMultiplier = _settings.capitalTorqueMultiplier;
                _dragMultiplier = _settings.capitalDragMultiplier;
                break;
            case GlobalHelper.ShipSizeTier.Medium:
                _torqueMultiplier = _settings.escortTorqueMultiplier;
                _dragMultiplier = _settings.escortDragMultiplier;
                break;
            default:
                _torqueMultiplier = 1f;
                _dragMultiplier = 1f;
                break;
        }
    }

    private void ApplyRotationalPhysics(Quaternion desiredRotation, float dt)
    {
        float effectiveTorque = _settings.torqueStrength * _torqueMultiplier;
        float maxAngSpeed = _settings.maxAngularSpeed * _torqueMultiplier;
        float damping = _settings.rotationalDrag;

        // Decompose desired facing into yaw/pitch (no roll) by using an up-constrained LookRotation
        Vector3 desiredForward = desiredRotation * Vector3.forward;
        Vector3 currentForward = _cachedTransform.forward;
        Vector3 currentUp = _cachedTransform.up;

        // --- Yaw/Pitch: Critically-damped spring toward desired heading ---
        // Calculate signed pitch and yaw errors in local space
        Vector3 localDesired = _cachedTransform.InverseTransformDirection(desiredForward);
        float yawError = Mathf.Atan2(localDesired.x, localDesired.z) * Mathf.Rad2Deg;
        float pitchError = -Mathf.Asin(Mathf.Clamp(localDesired.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Critically-damped spring: acceleration = stiffness * error - damping * velocity
        // This prevents oscillation by decelerating as we approach the target
        float criticalDamping = 2f * Mathf.Sqrt(effectiveTorque);
        float dampingForce = Mathf.Max(damping, criticalDamping);

        // Yaw torque (local Y axis)
        float yawAccel = yawError * effectiveTorque - _angularVelocity.y * dampingForce;
        _angularVelocity.y += yawAccel * dt;

        // Pitch torque (local X axis)
        float pitchAccel = pitchError * effectiveTorque - _angularVelocity.x * dampingForce;
        _angularVelocity.x += pitchAccel * dt;

        // --- Roll stabilization: auto-level toward world up ---
        float rollAngle = Vector3.SignedAngle(Vector3.up, currentUp, currentForward);
        float rollTorque = -rollAngle * effectiveTorque * 0.5f - _angularVelocity.z * dampingForce * 2f;
        _angularVelocity.z += rollTorque * dt;

        // Clamp angular speed per-axis to prevent spikes
        _angularVelocity.x = Mathf.Clamp(_angularVelocity.x, -maxAngSpeed, maxAngSpeed);
        _angularVelocity.y = Mathf.Clamp(_angularVelocity.y, -maxAngSpeed, maxAngSpeed);
        _angularVelocity.z = Mathf.Clamp(_angularVelocity.z, -maxAngSpeed * 0.5f, maxAngSpeed * 0.5f);

        // Apply angular velocity to rotation (in local space)
        if (_angularVelocity.sqrMagnitude > 0.0001f)
        {
            _cachedTransform.Rotate(_angularVelocity.x * dt, _angularVelocity.y * dt, _angularVelocity.z * dt, Space.Self);
        }
    }

    private void ApplyVelocityCoupling(float dt)
    {
        if (_settings.velocityCoupling <= 0f || _velocity.sqrMagnitude < 0.01f)
        {
            _previousRotation = _cachedTransform.rotation;
            return;
        }

        // Rotate velocity by a portion of the ship's rotation change this frame
        Quaternion rotationDelta = _cachedTransform.rotation * Quaternion.Inverse(_previousRotation);
        Vector3 rotatedVelocity = rotationDelta * _velocity;
        _velocity = Vector3.Lerp(_velocity, rotatedVelocity, _settings.velocityCoupling);

        _previousRotation = _cachedTransform.rotation;
    }

    private void ApplyMovement(Vector3 acceleration)
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        acceleration = Vector3.Lerp(_previousAcceleration, acceleration, dt * AccelerationSmoothSpeed);
        _previousAcceleration = acceleration;

        // Newtonian thrust decomposition — constrain acceleration to ship thruster axes
        acceleration = ApplyThrustAuthority(acceleration);

        // Apply steering forces to velocity
        _velocity += acceleration * dt;

        // Linear drag
        float effectiveDrag = _settings.linearDrag * _dragMultiplier;
        if (effectiveDrag > 0f)
            _velocity *= 1f / (1f + effectiveDrag * dt);

        float speed = _velocity.magnitude;
        if (speed <= 0.001f)
            return;

        // Soft speed management — target speed acts as a brake/thrust target
        float targetSpeed = GetDesiredCruiseSpeed(speed);
        if (speed > targetSpeed * 1.5f)
            _velocity = _velocity / speed * targetSpeed * 1.5f;
        else if (speed < _settings.minSpeed)
            _velocity += _cachedTransform.forward * (_settings.minSpeed - speed) * 2f * dt;

        speed = _velocity.magnitude;
        _smoothedSpeed = speed;

        // Rotational physics — torque-based turning replaces Slerp
        Vector3 desiredDirection = speed > 0.01f ? _velocity.normalized : forward;
        Quaternion desiredRotation = GetMovementRotation(desiredDirection);
        ApplyRotationalPhysics(desiredRotation, dt);

        // Velocity coupling — velocity partially rotates with the ship
        ApplyVelocityCoupling(dt);

        // Position update
        Vector3 newPos = _cachedTransform.position + _velocity * dt;
        ClampPositionToHeightRange(ref newPos);
        _cachedTransform.position = newPos;
        position = newPos;
        forward = _cachedTransform.forward;
    }

    /// <summary>
    /// Decomposes a world-space acceleration vector into ship-local thruster axes and scales
    /// each axis by its available authority: forward (main engine), reverse, and lateral/vertical (RCS).
    /// </summary>
    private Vector3 ApplyThrustAuthority(Vector3 acceleration)
    {
        if (acceleration.sqrMagnitude < 0.0001f) return acceleration;

        Vector3 shipForward = _cachedTransform.forward;
        float forwardDot = Vector3.Dot(acceleration, shipForward);
        Vector3 forwardComponent = shipForward * forwardDot;
        Vector3 lateralComponent = acceleration - forwardComponent;

        float forwardAuthority = forwardDot >= 0f ? 1.0f : _settings.reverseThrustRatio;

        float lateralAuthority = _settings.rcsAuthority;
        if (IsInCombat && AttackBehavior != null)
            lateralAuthority = Mathf.Min(lateralAuthority * _settings.combatRcsBoost, 1f);

        return (forwardComponent * forwardAuthority) + (lateralComponent * lateralAuthority);
    }

    private float GetDesiredCruiseSpeed(float currentSpeed)
    {
        if (_targetSpeed >= 0f)
            return Mathf.Clamp(_targetSpeed, 0f, _settings.maxSpeed);

        return Mathf.Clamp(currentSpeed, _settings.minSpeed, _settings.maxSpeed);
    }

    private Quaternion GetMovementRotation(Vector3 direction)
    {
        if (IsInCombat && AttackBehavior != null && AttackBehavior.RequiresCustomFacing() && _target != null)
        {
            Vector3 desiredFacing = AttackBehavior.GetDesiredFacingDirection(_target.position);
            _combatFacingDirection = Vector3.Slerp(_combatFacingDirection, desiredFacing, Time.deltaTime * CombatRotationSpeed);

            if (_combatFacingDirection.sqrMagnitude > 0.01f)
            {
                return Quaternion.LookRotation(_combatFacingDirection);
            }
        }

        _combatFacingDirection = direction;
        return Quaternion.LookRotation(direction);
    }

    private Vector3 CalculateFormationAcceleration()
    {
        if (FormationLeader == null) return Vector3.zero;

        Vector3 acceleration = Vector3.zero;

        Vector3 formationOffset = GetFormationOffset();
        Vector3 rawTarget = FormationLeader.position +
            FormationLeader._cachedTransform.TransformDirection(formationOffset);

        float distanceToRaw = Vector3.Distance(_smoothedFormationTarget, rawTarget);
        float distanceToFormation = Vector3.Distance(position, rawTarget);

        // Use faster smoothing when far from formation (e.g., after combat)
        float smoothSpeed = FormationTargetSmoothSpeed;
        if (distanceToFormation > _settings.formationSpacing * 2f)
        {
            smoothSpeed *= 5f; // Much faster catchup
        }
        else if (distanceToRaw > _settings.formationSpacing * 0.5f)
        {
            smoothSpeed *= 3f;
        }

        _smoothedFormationTarget = Vector3.Lerp(_smoothedFormationTarget, rawTarget, Time.deltaTime * smoothSpeed);

        Vector3 toFormation = _smoothedFormationTarget - position;
        distanceToFormation = toFormation.magnitude;

        if (distanceToFormation > _settings.formationDeadZone)
        {
            float urgency = Mathf.Clamp01((distanceToFormation - _settings.formationDeadZone) / _settings.formationUrgencyRange);
            urgency = Mathf.Max(urgency, 0.1f);

            // Boost urgency when very far from formation
            if (distanceToFormation > _settings.formationSpacing * 3f)
            {
                urgency = 1f;
            }

            Vector3 formationForce = SteerTowards(toFormation) * _settings.formationTightness * urgency;
            acceleration += formationForce;
        }

        Vector3 leaderVelocity = FormationLeader.Velocity;
        if (leaderVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 matchForce = SteerTowards(leaderVelocity) * _settings.formationMatchSpeed;
            acceleration += matchForce;
        }

        return acceleration;
    }

    public void OnFormationChanged()
    {
        // Smooth transition: blend from current position toward new formation target
        // instead of snapping, to avoid visible jerks during reassignment
        if (FormationLeader != null)
        {
            Vector3 formationOffset = GetFormationOffset();
            Vector3 newTarget = FormationLeader.position +
                FormationLeader._cachedTransform.TransformDirection(formationOffset);

            // If we already have a valid smoothed target, keep it — the normal
            // formation steering will smoothly blend toward the new slot.
            // Only snap if the smoothed target is uninitialized (at origin).
            if (_smoothedFormationTarget.sqrMagnitude < 0.01f)
            {
                _smoothedFormationTarget = newTarget;
            }
            // Otherwise let CalculateFormationAcceleration's existing smoothing
            // handle the transition naturally.
        }
    }

    private void ApplyHeightBoundaryRecovery(ref Vector3 acceleration)
    {
        if (_settings == null)
            return;

        float minHeight = HeightRange.x;
        float maxHeight = HeightRange.y;
        float heightSpan = maxHeight - minHeight;
        if (heightSpan <= 0f)
            return;

        float recoveryBuffer = Mathf.Min(heightSpan * 0.25f, Mathf.Max(MinHeightRecoveryBuffer, _settings.maxSpeed * 10f));
        float verticalBias = 0f;

        if (position.y <= minHeight + recoveryBuffer)
        {
            verticalBias = Mathf.InverseLerp(minHeight + recoveryBuffer, minHeight, position.y);
        }
        else if (position.y >= maxHeight - recoveryBuffer)
        {
            verticalBias = -Mathf.InverseLerp(maxHeight - recoveryBuffer, maxHeight, position.y);
        }

        if (Mathf.Abs(verticalBias) <= 0.001f)
            return;

        acceleration += SteerTowards(new Vector3(0f, verticalBias, 0f)) * _settings.targetWeight * 1.5f;
    }

    private void ApplyLeashBoundary(ref Vector3 acceleration)
    {
        if (_settings == null || !_settings.useLeash || !UseLeash)
            return;

        Vector3 offset = position - LeashCenter;
        float dist = offset.magnitude;
        float radius = _settings.leashRadius;

        if (dist < radius * _settings.leashSoftEdge)
            return;

        // Soft steering: ramps from 0 at softEdge to full strength at radius
        float t = Mathf.InverseLerp(radius * _settings.leashSoftEdge, radius, dist);
        Vector3 pullDir = -offset.normalized;
        acceleration += SteerTowards(pullDir) * _settings.leashStrength * t;
    }

    private void ClampPositionToHeightRange(ref Vector3 newPos)
    {
        float clampedY = Mathf.Clamp(newPos.y, HeightRange.x, HeightRange.y);
        if (!Mathf.Approximately(clampedY, newPos.y))
        {
            newPos.y = clampedY;

            if ((clampedY <= HeightRange.x && _velocity.y < 0f) ||
                (clampedY >= HeightRange.y && _velocity.y > 0f))
            {
                _velocity.y = 0f;
            }
        }

        // Hard clamp to leash radius as safety net
        if (_settings != null && _settings.useLeash && UseLeash)
        {
            Vector3 offset = newPos - LeashCenter;
            float dist = offset.magnitude;
            if (dist > _settings.leashRadius)
            {
                newPos = LeashCenter + offset.normalized * _settings.leashRadius;

                // Kill outward velocity component
                Vector3 outDir = offset.normalized;
                float outVel = Vector3.Dot(_velocity, outDir);
                if (outVel > 0f)
                    _velocity -= outDir * outVel;
            }
        }
    }

    private Vector3 SteerTowards(Vector3 direction)
    {
        Vector3 v = direction.normalized * _settings.maxSpeed - _velocity;
        return Vector3.ClampMagnitude(v, _settings.maxSteerForce);
    }

    /// <summary>
    /// Unified separation from ALL nearby boids (allies get soft push, enemies get hard push only when not in combat).
    /// </summary>
    private Vector3 CalculateBoidSeparation()
    {
        if (BoidRegistry.Instance == null)
            return Vector3.zero;

        Vector3 separation = Vector3.zero;
        Faction myFaction = GetFaction();

        // Query ALL nearby boids regardless of faction
        List<Boid> nearbyBoids = BoidRegistry.Instance.GetNearbyBoids(
            position,
            _settings.separationRadius
        );

        for (int i = 0; i < nearbyBoids.Count; i++)
        {
            Boid other = nearbyBoids[i];
            if (other == null || other == this)
                continue;

            Vector3 toSelf = position - other.position;
            float distance = toSelf.magnitude;

            if (distance < 0.1f || distance >= _settings.separationRadius)
                continue;

            float strength = 1f - (distance / _settings.separationRadius);

            bool isEnemy = other.GetFaction() != myFaction;

            if (isEnemy)
            {
                // During combat, let AttackProfile handle spacing - minimal separation
                // Outside combat, avoid enemies more strongly
                if (IsInCombat)
                    strength *= 0.1f;  // Minimal - AttackProfile controls this
                else
                    strength *= 1.5f;  // Stronger avoidance when not fighting
            }

            separation += toSelf.normalized * strength;
        }

        return separation;
    }

    /// <summary>
    /// Auto-calculate boid collision radius from child renderers.
    /// </summary>
    private float CalculateBoundsRadius()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return 1f; // Fallback for empty prefabs

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        // Half the maximum extent as the spherical clearance radius
        return Mathf.Max(combined.extents.x, combined.extents.y, combined.extents.z);
    }

    private Vector3 CalculateObstacleAvoidance()
    {
        if (ObstacleRegistry.Instance == null)
            return Vector3.zero;

        Vector3 avoidance = Vector3.zero;

        var obstacles = ObstacleRegistry.Instance.GetNearbyObstacles(
            position,
            _settings.obstacleDetectionRange
        );

        for (int i = 0; i < obstacles.Count; i++)
        {
            var obstacle = obstacles[i];

            Vector3 closestPoint = obstacle.WorldBounds.ClosestPoint(position);
            Vector3 toSelf = position - closestPoint;
            float distance = toSelf.magnitude;
            float safeDistance = _boundsRadius; // margin from surface, not center

            if (distance < safeDistance + _settings.obstacleDetectionRange)
            {
                float penetration = (safeDistance + _settings.obstacleDetectionRange) - distance;
                float urgency = Mathf.Clamp01(penetration / _settings.obstacleDetectionRange);
                urgency = urgency * urgency;

                if (distance < 0.01f)
                {
                    // Inside the obstacle — push away from obstacle center
                    avoidance += (position - obstacle.Position).normalized * urgency;
                }
                else
                {
                    avoidance += toSelf.normalized * urgency;
                }
            }
        }

        return avoidance;
    }

    public Faction GetFaction()
    {
        if (_targetManager != null)
            return _targetManager.GetFaction();

        return Faction.Neutral;
    }

    private Vector3 GetWanderForce()
    {
        _wanderTimer -= Time.deltaTime;

        if (_wanderTimer <= 0f || Vector3.Distance(position, _wanderTarget) < 50f)
        {
            // Pick new wander target
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y *= 0.3f; // Reduce vertical wandering

            _wanderTarget = position + randomDir.normalized * WanderRadius;
            _wanderTarget.y = Mathf.Clamp(_wanderTarget.y, HeightRange.x, HeightRange.y);
            _wanderTimer = WanderInterval + Random.Range(-1f, 1f);
        }

        Vector3 toWander = _wanderTarget - position;
        return SteerTowards(toWander);
    }

    /// <summary>
    /// Set initial velocity (used when launched from a spawn point).
    /// </summary>
    public void SetInitialVelocity(Vector3 velocity)
    {
        _velocity = velocity;
        _smoothedSpeed = velocity.magnitude;

        if (velocity.sqrMagnitude > 0.01f)
        {
            forward = velocity.normalized;
            _cachedTransform.forward = forward;
        }
    }

    public void SetFallbackTarget(Transform target)
    {
        _fallbackTarget = target;
    }

    // Boid Command Handling
    /// <summary>
    /// Set a specific speed for this boid to match.
    /// </summary>
    public void SetTargetSpeed(float speed)
    {
        _targetSpeed = speed;
    }

    /// <summary>
    /// Clear target speed and return to normal behavior.
    /// </summary>
    public void ClearTargetSpeed()
    {
        _targetSpeed = -1f;
    }

    /// <summary>
    /// Set a specific position to move toward.
    /// </summary>
    public void SetMoveTarget(Vector3 position)
    {
        _moveTarget = position;
        _holdingPosition = false;
    }

    /// <summary>
    /// Clear move target.
    /// </summary>
    public void ClearMoveTarget()
    {
        _moveTarget = null;
        _holdingPosition = false;
    }

    /// <summary>
    /// Hold at current position.
    /// </summary>
    public void HoldPosition()
    {
        _holdingPosition = true;
        _moveTarget = position;
        IsInCombat = false; // Exit combat when holding
    }

    /// <summary>
    /// Set a priority target for attack.
    /// </summary>
    public void SetPriorityTarget(Transform target)
    {
        _priorityTarget = target;
    }

    /// <summary>
    /// Clear priority target.
    /// </summary>
    public void ClearPriorityTarget()
    {
        _priorityTarget = null;
    }

    void OnDrawGizmos()
    {
        if (!_EnableDebugGizmos) return;
        // Leader indicator - large sphere above
        if (FormationIndex == 0)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 20f, 15f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 20f);

            // Show leader's forward direction
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, forward * 100f);

            // Show post-combat state
            if (_postCombatTimer < PostCombatSteadyTime && !IsInCombat)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 30f, 10f);
                Gizmos.DrawRay(transform.position, _lastCombatHeading * 80f);
            }
        }

        // Follower - show line to formation target
        if (FormationLeader != null && FormationIndex > 0)
        {
            // Line to leader
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, FormationLeader.position);

            // Line to formation target position
            Gizmos.color = IsInCombat ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, _smoothedFormationTarget);
            Gizmos.DrawWireSphere(_smoothedFormationTarget, 5f);

            // Show actual target position (not smoothed)
            Vector3 formationOffset = GetFormationOffset();
            Vector3 rawTarget = FormationLeader.position +
                FormationLeader._cachedTransform.TransformDirection(formationOffset);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(rawTarget, Vector3.one * 8f);
        }

        // Combat state indicator
        if (IsInCombat)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 10f);
        }

        // Target line
        if (_target != null)
        {
            Gizmos.color = _target.CompareTag("Ally") ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, _target.position);

            if (IsInCombat && AttackBehavior != null && AttackBehavior.Profile != null)
            {
                Vector3 moveDir = AttackBehavior.GetDesiredMovementDirection(_target.position, _target.forward);
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, moveDir * 50f);
            }
        }

        // Wander target (for leader without target)
        if (FormationIndex == 0 && _target == null && _postCombatTimer >= PostCombatSteadyTime)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, _wanderTarget);
            Gizmos.DrawWireSphere(_wanderTarget, 20f);
        }
    }
}