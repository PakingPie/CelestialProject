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
            // Find avoid direction incrementally (check a few rays per frame)
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

    public void UpdateBoid()
    {
        Vector3 acceleration = Vector3.zero;

        // Target following
        if (_target != null)
        {
            Vector3 offsetToTarget = _target.position - position;
            acceleration = SteerTowards(offsetToTarget) * _settings.targetWeight;
        }
        else
        {
            UpdateTarget();
        }

        // Flocking behavior
        if (numPerceivedFlockmates > 0)
        {
            Vector3 centerOfMass = flockmatesCenter / numPerceivedFlockmates;
            Vector3 offsetToCenter = centerOfMass - position;

            acceleration += SteerTowards(avgFlockHeading) * _settings.alignWeight;
            acceleration += SteerTowards(offsetToCenter) * _settings.cohesionWeight;
            acceleration += SteerTowards(avgAvoidanceHeading) * _settings.separateWeight;
        }

        // Collision avoidance (state set by BoidObstacleSystem)
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

    // Check only a few directions per call instead of all 300
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