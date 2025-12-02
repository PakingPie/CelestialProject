using UnityEngine;

public class Boid : MonoBehaviour
{
    private BoidSettings _settings;
    private Transform _cachedTransform;
    private Transform _target;
    private Material _material;
    private Gun _gun;

    private Vector3 _velocity;

    [HideInInspector] public Vector3 position;
    [HideInInspector] public Vector3 forward;
    [HideInInspector] public Vector3 avgFlockHeading;
    [HideInInspector] public Vector3 avgAvoidanceHeading;
    [HideInInspector] public Vector3 flockmatesCenter;
    [HideInInspector] public int numPerceivedFlockmates;

    public Vector2 HeightRange = new Vector2(-100.0f, 100.0f);

    // Collision state set by BoidObstacleSystem
    private bool _isHeadingForCollision;
    private Vector3 _avoidDirection;
    private int _avoidDirectionIndex;

    // Formation state
    [HideInInspector] public bool IsInCombat = false;
    [HideInInspector] public int FormationIndex = 0;
    [HideInInspector] public Boid FormationLeader = null;
    private float _combatTimer = 0f;

    public BoidSettings Settings => _settings;
    public Vector3 Velocity => _velocity;

    void Awake()
    {
        _cachedTransform = transform;

        var meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
            _material = meshRenderer.material;

        _gun = GetComponentInChildren<Gun>();
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

    public void Initialize(BoidSettings settings, Transform target)
    {
        _settings = settings;
        _target = target;

        position = _cachedTransform.position;
        forward = _cachedTransform.forward;

        float startSpeed = (_settings.minSpeed + _settings.maxSpeed) * 0.5f;
        _velocity = _cachedTransform.forward * startSpeed;

        _avoidDirectionIndex = Random.Range(0, BoidDirections.viewDirections.Length);
    }

    // Called by BoidObstacleSystem
    public void SetCollisionState(bool isColliding)
    {
        _isHeadingForCollision = isColliding;

        if (isColliding)
        {
            _avoidDirection = FindUnobstructedDirectionIncremental();
        }
    }

    public float GetCollisionDistance() => _settings.collisionAvoidDistance;
    public LayerMask GetObstacleMask() => _settings.obstacleMask;

    public void UpdateTarget()
    {
        if (_gun != null)
            _target = _gun.Targeted;
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
        if (!IsInCombat) return;

        bool hasTarget = false;
        var weapons = GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            if (weapon.Targeted != null)
            {
                hasTarget = true;
                _combatTimer = 0f;
                break;
            }
        }

        if (!hasTarget)
        {
            _combatTimer += Time.deltaTime;
            if (_combatTimer >= _settings.returnToFormationDelay)
            {
                IsInCombat = false;
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
        int position = (index + 1) / 2;

        return new Vector3(side * position * spacing, 0f, 0f);
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
        UpdateCombatState();

        Vector3 acceleration = Vector3.zero;

        // Formation behavior when not in combat
        if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
        {
            acceleration += CalculateFormationAcceleration();
        }
        else if (_target != null)
        {
            // Normal target following
            Vector3 offsetToTarget = _target.position - position;
            acceleration = SteerTowards(offsetToTarget) * _settings.targetWeight;
        }
        else
        {
            UpdateTarget();
        }

        // Flocking behavior with combat modifiers
        if (numPerceivedFlockmates > 0)
        {
            Vector3 centerOfMass = flockmatesCenter / numPerceivedFlockmates;
            Vector3 offsetToCenter = centerOfMass - position;

            float alignMult = IsInCombat ? _settings.combatAlignmentMultiplier : 1f;
            float cohesionMult = IsInCombat ? _settings.combatCohesionMultiplier : 1f;
            float separateMult = IsInCombat ? _settings.combatSeparationMultiplier : 1f;

            // In formation mode (not combat), reduce normal flocking
            if (_settings.useFormation && !IsInCombat && FormationLeader != null && FormationIndex > 0)
            {
                alignMult *= 0.3f;
                cohesionMult *= 0.1f;
            }

            acceleration += SteerTowards(avgFlockHeading) * _settings.alignWeight * alignMult;
            acceleration += SteerTowards(offsetToCenter) * _settings.cohesionWeight * cohesionMult;
            acceleration += SteerTowards(avgAvoidanceHeading) * _settings.separateWeight * separateMult;
        }

        // Collision avoidance (always active)
        if (_isHeadingForCollision)
        {
            acceleration += SteerTowards(_avoidDirection) * _settings.avoidCollisionWeight;
        }

        // Apply velocity
        _velocity += acceleration * Time.deltaTime;

        float speed = _velocity.magnitude;
        if (speed > 0.001f)
        {
            Vector3 dir = _velocity / speed;
            speed = Mathf.Clamp(speed, _settings.minSpeed, _settings.maxSpeed);
            _velocity = dir * speed;

            Vector3 newPos = _cachedTransform.position + _velocity * Time.deltaTime;
            newPos.y = Mathf.Clamp(newPos.y, HeightRange.x, HeightRange.y);

            _cachedTransform.SetPositionAndRotation(newPos, Quaternion.LookRotation(dir));

            position = newPos;
            forward = dir;
        }
    }

    private Vector3 CalculateFormationAcceleration()
    {
        Vector3 acceleration = Vector3.zero;

        if (FormationLeader == null) return acceleration;

        // Calculate target formation position in world space
        Vector3 formationOffset = GetFormationOffset();
        Vector3 formationTarget = FormationLeader.position +
            FormationLeader._cachedTransform.TransformDirection(formationOffset);

        Vector3 toFormation = formationTarget - position;
        float distanceToFormation = toFormation.magnitude;

        if (distanceToFormation > 0.5f)
        {
            // Steer towards formation position
            Vector3 formationForce = SteerTowards(toFormation) * _settings.formationTightness;
            acceleration += formationForce;
        }

        // Match leader's velocity for smooth following
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
}