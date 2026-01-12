using System.Collections.Generic;
using UnityEngine;
using static GlobalHelper;
public class Boid : MonoBehaviour
{
    private BoidSettings _settings;
    private Transform _cachedTransform;
    [SerializeField] private Transform _target;
    private Material _material;
    private BoidFlockTargetManager _targetManager;
    private Vector3 _velocity;
    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 forward;
    [HideInInspector] public Vector3 avgFlockHeading;
    [HideInInspector] public Vector3 avgAvoidanceHeading;
    [HideInInspector] public Vector3 flockmatesCenter;
    [HideInInspector] public int numPerceivedFlockmates;

    public Vector2 HeightRange = new Vector2(-100.0f, 100.0f);


    private Transform _fallbackTarget;
    // Smoothing state
    private Vector3 _smoothedFlockHeading;
    private Vector3 _smoothedFlockCenter;
    private Vector3 _smoothedFormationTarget;
    private Vector3 _previousAcceleration;
    private float _smoothedSpeed;
    private Vector3 _smoothedTargetPosition;


    [Header("Debug")]
    [SerializeField] private bool _EnableDebugLogs = false;
    [SerializeField] private bool _EnableDebugGizmos = false;

    // Formation state
    [HideInInspector] public bool IsInCombat = false;
    [HideInInspector] public int FormationIndex = 0;
    [HideInInspector] public Boid FormationLeader = null;
    private float _combatTimer = 0f;

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

    private Vector3 _combatFacingDirection;
    private const float CombatRotationSpeed = 4f;

    // Wander state
    private Vector3 _wanderTarget;
    private float _wanderTimer;
    private const float WanderRadius = 2500f;
    private const float WanderInterval = 60f;

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

    void Start()
    {
        // Debug.Log($"Boid '{name}')");
        // Debug.Log($"ObstacleMask: {_settings.obstacleMask}");
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
        if (fallbackTarget == null)
            Debug.LogWarning($"Boid '{name}': fallbackTarget is null!");
        _settings = settings;
        _fallbackTarget = fallbackTarget;  // Store the fallback separately
        _target = fallbackTarget;

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
        _combatFacingDirection = forward;
        _wanderTarget = position + Random.insideUnitSphere * WanderRadius;
        _wanderTarget.y = Mathf.Clamp(_wanderTarget.y, HeightRange.x, HeightRange.y);
        _wanderTimer = Random.Range(0f, WanderInterval);
    }

    private void UpdateRegistryPosition()
    {
        if (BoidRegistry.Instance != null)
            BoidRegistry.Instance.UpdateBoidPosition(this);
    }

    public void SetTargetManager(BoidFlockTargetManager manager)
    {
        _targetManager = manager;
    }

    public void UpdateTarget()
    {
        if (_targetManager != null)
        {
            Transform assignedTarget = _targetManager.GetAssignedTarget(this);
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

        // Debug this
        // Debug.Log($"Combat timer: {_combatTimer} / {_settings.returnToFormationDelay}");

        if (_combatTimer >= _settings.returnToFormationDelay)
        {
            IsInCombat = false;
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
        return CalculateFormationOffset(FormationIndex, _settings.formationType, _settings.formationSpacing);
    }

    private Vector3 CalculateFormationOffset(int index, FormationType type, float spacing)
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

    private Vector3 GetVFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        // Add vertical stagger - alternating up/down
        float yOffset = (row % 2 == 0) ? spacing * 0.3f : -spacing * 0.3f;

        return new Vector3(side * row * spacing, yOffset, -row * spacing);
    }

    private Vector3 GetLineFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int pos = (index + 1) / 2;

        // Stagger vertically
        float yOffset = (index % 2 == 0) ? spacing * 0.2f : -spacing * 0.2f;

        return new Vector3(side * pos * spacing, yOffset, 0f);
    }

    private Vector3 GetWedgeFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        float yOffset = (row % 2 == 0) ? spacing * 0.25f : -spacing * 0.25f;

        return new Vector3(side * row * spacing * 0.5f, yOffset, -row * spacing);
    }

    private Vector3 GetBoxFormationOffset(int index, float spacing)
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

    private Vector3 GetCircleFormationOffset(int index, float spacing)
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

    private Vector3 GetEchelonFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Diagonal climb/descent
        float yOffset = index * spacing * 0.3f;

        return new Vector3(index * spacing * 0.7f, yOffset, -index * spacing);
    }

    private Vector3 GetSphereFormationOffset(int index, float spacing)
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

    private Vector3 GetHelixFormationOffset(int index, float spacing)
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

    private Vector3 GetWallFormationOffset(int index, float spacing)
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
        UpdateTarget();
        UpdateCombatState();

        if (!IsInCombat && _postCombatTimer < PostCombatSteadyTime)
        {
            _postCombatTimer += Time.deltaTime;
        }

        _smoothedFlockHeading = Vector3.Lerp(_smoothedFlockHeading, avgFlockHeading, Time.deltaTime * FlockDataSmoothSpeed);
        _smoothedFlockCenter = Vector3.Lerp(_smoothedFlockCenter, flockmatesCenter, Time.deltaTime * FlockDataSmoothSpeed);

        Vector3 acceleration = Vector3.zero;

        // Formation behavior when not in combat
        if (_settings.useFormation && !IsInCombat && FormationIndex == 0)
        {
            // Leader: move toward assigned target, or hold heading after combat, or wander
            if (_target != null)
            {
                Vector3 targetPos = _target.position;
                _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, targetPos, Time.deltaTime * TargetSmoothSpeed);
                Vector3 offsetToTarget = _smoothedTargetPosition - position;

                if (offsetToTarget.sqrMagnitude > 25f)
                {
                    acceleration += SteerTowards(offsetToTarget) * _settings.targetWeight;
                }
            }
            else if (_postCombatTimer < PostCombatSteadyTime)
            {
                // Just exited combat - maintain last heading to let formation reform
                acceleration += SteerTowards(_lastCombatHeading) * _settings.targetWeight * 0.5f;
            }
            else
            {
                // No target and formation has had time to reform - wander randomly
                acceleration += GetWanderForce() * _settings.targetWeight;
            }
        }
        else if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
        {
            acceleration += CalculateFormationAcceleration();
        }
        else if (IsInCombat && AttackBehavior != null && AttackBehavior.Profile != null && _target != null)
        {
            Vector3 movementDir = AttackBehavior.GetDesiredMovementDirection(_target.position, _target.forward);
            acceleration += SteerTowards(movementDir) * _settings.targetWeight * 1.5f;

            // Apply speed multiplier from attack behavior
            float speedMult = AttackBehavior.SpeedMultiplier;
            _velocity *= Mathf.Lerp(1f, speedMult, Time.deltaTime * 3f);
        }
        else
        {
            // Default: move toward target or wander
            if (_target != null)
            {
                Vector3 targetPos = _target.position;
                _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, targetPos, Time.deltaTime * TargetSmoothSpeed);
                Vector3 offsetToTarget = _smoothedTargetPosition - position;
                acceleration += SteerTowards(offsetToTarget) * _settings.targetWeight;
            }
            else
            {
                acceleration += GetWanderForce() * _settings.targetWeight;
            }
        }

        // Flocking behavior
        if (numPerceivedFlockmates > 0)
        {
            Vector3 centerOfMass = _smoothedFlockCenter / numPerceivedFlockmates;
            Vector3 offsetToCenter = centerOfMass - position;

            float alignMult = IsInCombat ? _settings.combatAlignmentMultiplier : 1f;
            float cohesionMult = IsInCombat ? _settings.combatCohesionMultiplier : 1f;
            float separateMult = IsInCombat ? _settings.combatSeparationMultiplier : 1f;

            // Formation mode: drastically reduce flocking
            if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
            {
                alignMult *= 0.1f;
                cohesionMult *= 0.05f;
                separateMult *= 0.3f;  // Keep some separation to avoid collisions
            }

            // Leader should also reduce cohesion to not get pulled back
            if (_settings.useFormation && !IsInCombat && FormationIndex == 0)
            {
                cohesionMult *= 0.1f;
            }

            acceleration += SteerTowards(_smoothedFlockHeading) * _settings.alignWeight * alignMult;
            acceleration += SteerTowards(offsetToCenter) * _settings.cohesionWeight * cohesionMult;
        }

        Vector3 boidSeparation = CalculateBoidSeparation();
        if (boidSeparation.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(boidSeparation) * _settings.separateWeight;
        }

        // Collision avoidance
        Vector3 obstacleAvoidance = CalculateObstacleAvoidance();
        if (obstacleAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(obstacleAvoidance) * _settings.obstacleAvoidanceWeight;
        }

        // Smooth acceleration
        acceleration = Vector3.Lerp(_previousAcceleration, acceleration, Time.deltaTime * AccelerationSmoothSpeed);
        _previousAcceleration = acceleration;

        // Apply velocity
        _velocity += acceleration * Time.deltaTime;

        float speed = _velocity.magnitude;
        if (speed > 0.001f)
        {
            Vector3 dir = _velocity / speed;

            float targetSpeed = Mathf.Clamp(speed, _settings.minSpeed, _settings.maxSpeed);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, targetSpeed, Time.deltaTime * SpeedSmoothSpeed);
            _velocity = dir * _smoothedSpeed;

            Vector3 newPos = _cachedTransform.position + _velocity * Time.deltaTime;
            newPos.y = Mathf.Clamp(newPos.y, HeightRange.x, HeightRange.y);

            // Rotation: use custom facing for broadside ships
            Quaternion targetRotation;

            if (IsInCombat && AttackBehavior != null && AttackBehavior.RequiresCustomFacing() && _target != null)
            {
                Vector3 desiredFacing = AttackBehavior.GetDesiredFacingDirection(_target.position);
                _combatFacingDirection = Vector3.Slerp(_combatFacingDirection, desiredFacing, Time.deltaTime * CombatRotationSpeed);

                if (_combatFacingDirection.sqrMagnitude > 0.01f)
                {
                    targetRotation = Quaternion.LookRotation(_combatFacingDirection);
                }
                else
                {
                    targetRotation = Quaternion.LookRotation(dir);
                }
            }
            else
            {
                targetRotation = Quaternion.LookRotation(dir);
                _combatFacingDirection = dir;
            }

            Quaternion smoothedRotation = Quaternion.Slerp(_cachedTransform.rotation, targetRotation, Time.deltaTime * RotationSmoothSpeed);

            _cachedTransform.SetPositionAndRotation(newPos, smoothedRotation);

            position = newPos;
            forward = smoothedRotation * Vector3.forward;
        }

        UpdateRegistryPosition();
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
        // Reset smoothed target to force immediate recalculation
        if (FormationLeader != null)
        {
            Vector3 formationOffset = GetFormationOffset();
            _smoothedFormationTarget = FormationLeader.position +
                FormationLeader._cachedTransform.TransformDirection(formationOffset);
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

            Vector3 toSelf = position - obstacle.Position;
            float distance = toSelf.magnitude;
            float safeDistance = obstacle.Radius + _settings.boundsRadius;

            if (distance < safeDistance + _settings.obstacleDetectionRange)
            {
                float penetration = (safeDistance + _settings.obstacleDetectionRange) - distance;
                float urgency = Mathf.Clamp01(penetration / _settings.obstacleDetectionRange);
                urgency = urgency * urgency;

                avoidance += toSelf.normalized * urgency;
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

        // Debug.Log("IsTargetInfo Valid: " + (_targetManager != null && _targetManager.GetTargetInfo(this) != null && _targetManager.GetTargetInfo(this).IsValid));

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