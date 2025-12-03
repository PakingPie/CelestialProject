using UnityEngine;

public class Boid : MonoBehaviour
{
    private BoidSettings _settings;
    private Transform _cachedTransform;
    private Transform _target;
    private Material _material;
    private FlockTargetManager _targetManager;

    private Vector3 _velocity;

    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 forward;
    [HideInInspector] public Vector3 avgFlockHeading;
    [HideInInspector] public Vector3 avgAvoidanceHeading;
    [HideInInspector] public Vector3 flockmatesCenter;
    [HideInInspector] public int numPerceivedFlockmates;

    public Vector2 HeightRange = new Vector2(-100.0f, 100.0f);

    // Collision state
    private bool _isHeadingForCollision;
    private Vector3 _avoidDirection;
    private int _avoidDirectionIndex;

    // Smoothing state
    private Vector3 _smoothedAvoidDirection;
    private float _collisionUrgency = 0f;
    private Vector3 _smoothedFlockHeading;
    private Vector3 _smoothedFlockCenter;
    private Vector3 _smoothedFormationTarget;
    private Vector3 _previousAcceleration;
    private float _smoothedSpeed;
    private Vector3 _smoothedTargetPosition;

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
    private const float FormationDeadZone = 1.5f;
    private const float FormationUrgencyRange = 10f;
    private const float TargetSmoothSpeed = 10f;

    // Add field
    private Collider[] _nearbyEnemies = new Collider[20];

    public BoidSettings Settings => _settings;
    public Vector3 Velocity => _velocity;
    public Transform CurrentTarget => _target;

    void Awake()
    {
        _cachedTransform = transform;

        var meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
            _material = meshRenderer.material;
    }

    void OnEnable()
    {
        if (BoidObstacleSystem.Instance != null)
            BoidObstacleSystem.Instance.RegisterBoid(this);
    }

    void OnDisable()
    {
        if (BoidObstacleSystem.Instance != null)
            BoidObstacleSystem.Instance.UnregisterBoid(this);
    }

    public void Initialize(BoidSettings settings, Transform fallbackTarget)
    {
        _settings = settings;
        _target = fallbackTarget;

        position = _cachedTransform.position;
        forward = _cachedTransform.forward;

        float startSpeed = (_settings.minSpeed + _settings.maxSpeed) * 0.5f;
        _velocity = _cachedTransform.forward * startSpeed;
        _smoothedSpeed = startSpeed;

        _avoidDirectionIndex = Random.Range(0, BoidDirections.viewDirections.Length);

        _smoothedAvoidDirection = forward;
        _smoothedFlockHeading = forward;
        _smoothedFlockCenter = position;
        _smoothedFormationTarget = position;
        _smoothedTargetPosition = position + forward * 50f;
        _previousAcceleration = Vector3.zero;
        _collisionUrgency = 0f;
    }

    public void SetTargetManager(FlockTargetManager manager)
    {
        _targetManager = manager;
    }

    public void SetCollisionState(bool isColliding)
    {
        _isHeadingForCollision = isColliding;

        float targetUrgency = isColliding ? 1f : 0f;
        _collisionUrgency = Mathf.Lerp(_collisionUrgency, targetUrgency, Time.deltaTime * CollisionSmoothSpeed);

        if (isColliding)
        {
            Vector3 newDir = FindUnobstructedDirectionIncremental();
            _smoothedAvoidDirection = Vector3.Slerp(_smoothedAvoidDirection, newDir, Time.deltaTime * AvoidDirectionSmoothSpeed);
        }
    }

    public float GetCollisionDistance() => _settings.collisionAvoidDistance;
    public LayerMask GetObstacleMask() => _settings.obstacleMask;

    public void UpdateTarget()
    {
        if (_targetManager != null)
        {
            Transform assignedTarget = _targetManager.GetAssignedTarget(this);
            if (assignedTarget != null)
            {
                _target = assignedTarget;
                return;
            }
        }
    }

    public Vector3 GetTargetPosition()
    {
        if (_targetManager != null)
        {
            TargetInfo info = _targetManager.GetTargetInfo(this);
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
        IsInCombat = true;
        _combatTimer = 0f;
    }

    public void UpdateCombatState()
    {
        if (_targetManager != null)
        {
            TargetInfo info = _targetManager.GetTargetInfo(this);
            if (info != null && info.IsValid)
            {
                IsInCombat = true;
                _combatTimer = 0f;
                return;
            }
        }

        if (!IsInCombat) return;

        _combatTimer += Time.deltaTime;
        if (_combatTimer >= _settings.returnToFormationDelay)
        {
            IsInCombat = false;
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
            default:
                return Vector3.zero;
        }
    }

    private Vector3 GetVFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        return new Vector3(side * row * spacing, 0f, -row * spacing);
    }

    private Vector3 GetLineFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int pos = (index + 1) / 2;

        return new Vector3(side * pos * spacing, 0f, 0f);
    }

    private Vector3 GetWedgeFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int side = (index % 2 == 0) ? 1 : -1;
        int row = (index + 1) / 2;

        return new Vector3(side * row * spacing * 0.5f, 0f, -row * spacing);
    }

    private Vector3 GetBoxFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(index + 1));
        int x = index % gridSize;
        int z = index / gridSize;

        float offsetX = (x - gridSize / 2f) * spacing;
        float offsetZ = -z * spacing;

        return new Vector3(offsetX, 0f, offsetZ);
    }

    private Vector3 GetCircleFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        int ring = 1;
        int ringStart = 1;
        int boidsPerRing = 6;

        while (index >= ringStart + boidsPerRing * ring)
        {
            ringStart += boidsPerRing * ring;
            ring++;
        }

        int indexInRing = index - ringStart;
        int totalInRing = boidsPerRing * ring;
        float angle = (indexInRing / (float)totalInRing) * Mathf.PI * 2f;
        float radius = spacing * ring;

        return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
    }

    private Vector3 GetEchelonFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        return new Vector3(index * spacing * 0.7f, 0f, -index * spacing);
    }

    public void UpdateBoid()
    {
        UpdateTarget();
        UpdateCombatState();

        _smoothedFlockHeading = Vector3.Lerp(_smoothedFlockHeading, avgFlockHeading, Time.deltaTime * FlockDataSmoothSpeed);
        _smoothedFlockCenter = Vector3.Lerp(_smoothedFlockCenter, flockmatesCenter, Time.deltaTime * FlockDataSmoothSpeed);

        Vector3 acceleration = Vector3.zero;

        // Formation behavior when not in combat
        if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
        {
            acceleration += CalculateFormationAcceleration();
        }
        else
        {
            Vector3 targetPos = GetTargetPosition();

            // Add engagement distance offset
            if (_targetManager != null)
            {
                targetPos += _targetManager.GetEngagementOffset(this, targetPos);
            }

            _smoothedTargetPosition = Vector3.Lerp(_smoothedTargetPosition, targetPos, Time.deltaTime * TargetSmoothSpeed);

            Vector3 offsetToTarget = _smoothedTargetPosition - position;
            acceleration = SteerTowards(offsetToTarget) * _settings.targetWeight;
        }

        // Flocking behavior
        if (numPerceivedFlockmates > 0)
        {
            Vector3 centerOfMass = _smoothedFlockCenter / numPerceivedFlockmates;
            Vector3 offsetToCenter = centerOfMass - position;

            float alignMult = IsInCombat ? _settings.combatAlignmentMultiplier : 1f;
            float cohesionMult = IsInCombat ? _settings.combatCohesionMultiplier : 1f;
            float separateMult = IsInCombat ? _settings.combatSeparationMultiplier : 1f;

            if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
            {
                alignMult *= 0.3f;
                cohesionMult *= 0.1f;
            }

            acceleration += SteerTowards(_smoothedFlockHeading) * _settings.alignWeight * alignMult;
            acceleration += SteerTowards(offsetToCenter) * _settings.cohesionWeight * cohesionMult;
            acceleration += SteerTowards(avgAvoidanceHeading) * _settings.separateWeight * separateMult;
        }

        // Enemy avoidance
        Vector3 enemyAvoidance = CalculateEnemyAvoidance();
        if (enemyAvoidance.sqrMagnitude > 0.01f)
        {
            acceleration += SteerTowards(enemyAvoidance) * _settings.enemyAvoidanceWeight;
        }

        // Collision avoidance
        if (_collisionUrgency > 0.01f)
        {
            acceleration += SteerTowards(_smoothedAvoidDirection) * _settings.avoidCollisionWeight * _collisionUrgency;
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

            Quaternion targetRotation = Quaternion.LookRotation(dir);
            Quaternion smoothedRotation = Quaternion.Slerp(_cachedTransform.rotation, targetRotation, Time.deltaTime * RotationSmoothSpeed);

            _cachedTransform.SetPositionAndRotation(newPos, smoothedRotation);

            position = newPos;
            forward = smoothedRotation * Vector3.forward;
        }
    }

    private Vector3 CalculateFormationAcceleration()
    {
        if (FormationLeader == null) return Vector3.zero;

        Vector3 acceleration = Vector3.zero;

        Vector3 formationOffset = GetFormationOffset();
        Vector3 rawTarget = FormationLeader.position +
            FormationLeader._cachedTransform.TransformDirection(formationOffset);

        _smoothedFormationTarget = Vector3.Lerp(_smoothedFormationTarget, rawTarget, Time.deltaTime * FormationTargetSmoothSpeed);

        Vector3 toFormation = _smoothedFormationTarget - position;
        float distanceToFormation = toFormation.magnitude;

        if (distanceToFormation > FormationDeadZone)
        {
            float urgency = Mathf.Clamp01((distanceToFormation - FormationDeadZone) / FormationUrgencyRange);
            Vector3 formationForce = SteerTowards(toFormation) * _settings.formationTightness * urgency;
            acceleration += formationForce;
        }

        Vector3 leaderVelocity = FormationLeader.Velocity;
        if (leaderVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 matchForce = SteerTowards(leaderVelocity - _velocity) * _settings.formationMatchSpeed;
            acceleration += matchForce;
        }

        return acceleration;
    }

    private Vector3 FindUnobstructedDirectionIncremental()
    {
        Vector3[] directions = BoidDirections.viewDirections;
        int checksPerFrame = 8;

        for (int i = 0; i < checksPerFrame; i++)
        {
            int idx = (_avoidDirectionIndex + i) % directions.Length;
            Vector3 dir = _cachedTransform.TransformDirection(directions[idx]);

            if (!Physics.SphereCast(position, _settings.boundsRadius, dir, out _, _settings.collisionAvoidDistance, _settings.obstacleMask))
            {
                _avoidDirectionIndex = idx;
                return dir;
            }
        }

        _avoidDirectionIndex = (_avoidDirectionIndex + checksPerFrame) % directions.Length;
        return forward;
    }

    private Vector3 SteerTowards(Vector3 direction)
    {
        Vector3 v = direction.normalized * _settings.maxSpeed - _velocity;
        return Vector3.ClampMagnitude(v, _settings.maxSteerForce);
    }

    // Add method
    private Vector3 CalculateEnemyAvoidance()
    {
        if (_targetManager == null) return Vector3.zero;

        Vector3 avoidance = Vector3.zero;
        int enemyCount = 0;

        // Get nearby colliders
        int count = Physics.OverlapSphereNonAlloc(
            position,
            _settings.enemyAvoidanceRadius,
            _nearbyEnemies
        );

        for (int i = 0; i < count; i++)
        {
            if (_nearbyEnemies[i] == null) continue;

            Transform other = _nearbyEnemies[i].transform;

            // Skip self
            if (other == _cachedTransform) continue;

            // Check if it's an enemy (has Boid component but not in our flock)
            Boid otherBoid = other.GetComponent<Boid>();
            if (otherBoid == null) continue;

            // Skip if same flock
            if (otherBoid._targetManager == _targetManager) continue;

            Vector3 toSelf = position - other.position;
            float distance = toSelf.magnitude;

            if (distance < _settings.enemyAvoidanceRadius && distance > 0.01f)
            {
                // Stronger repulsion when closer
                float strength = 1f - (distance / _settings.enemyAvoidanceRadius);
                avoidance += toSelf.normalized * strength;
                enemyCount++;
            }
        }

        if (enemyCount > 0)
        {
            avoidance /= enemyCount;
        }

        return avoidance;
    }

    void OnDrawGizmos()
    {
        if (_target != null)
        {
            if (_target.tag != gameObject.tag)
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }
}