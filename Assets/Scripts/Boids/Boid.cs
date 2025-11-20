using UnityEngine;

public class Boid : MonoBehaviour
{
    BoidSettings settings;

    [HideInInspector]
    public Vector3 position;
    [HideInInspector]
    public Vector3 forward;

    Vector3 velocity;
    [HideInInspector]
    public Vector3 avgFlockHeading;
    [HideInInspector]
    public Vector3 avgAvoidanceHeading;
    [HideInInspector]
    public Vector3 flockmatesCenter;
    [HideInInspector]
    public int numPerceivedFlockmates;

    Material material;
    Transform cachedTransform;
    Transform target;

    public Vector2 HeightRange = new Vector2(-100.0f, 100.0f);

    void Awake()
    {
        material = transform.GetComponentInChildren<MeshRenderer>().material;
        cachedTransform = transform;
    }

    public void Initialize(BoidSettings settings, Transform target)
    {
        this.settings = settings;
        this.target = target;

        position = cachedTransform.position;
        forward = cachedTransform.forward;

        float startSpeed = (settings.minSpeed + settings.maxSpeed) / 2.0f;
        velocity = transform.forward * startSpeed;
    }

    public void UpdateTarget()
    {
        target = GetComponentInChildren<Gun>().Targeted;
    }

    public void SetColor(Color color)
    {
        if (material != null)
        {
            material.color = color;
        }
    }

    public void UpdateBoid()
    {
        Vector3 acceleration = Vector3.zero;

        if (target != null)
        {
            Vector3 offsetToTarget = target.position - position;
            acceleration = SteerTowards(offsetToTarget) * settings.targetWeight;
        }
        else
        {
            UpdateTarget();
        }

        if (numPerceivedFlockmates != 0)
        {
            flockmatesCenter /= numPerceivedFlockmates;

            Vector3 offsetToFlockmatesCenter = flockmatesCenter - position;

            var alignmentForce = SteerTowards(avgFlockHeading) * settings.alignWeight;
            var cohesionForce = SteerTowards(offsetToFlockmatesCenter) * settings.cohesionWeight;
            var separationForce = SteerTowards(avgAvoidanceHeading) * settings.separateWeight;

            acceleration += alignmentForce + cohesionForce + separationForce;
        }

        if (IsHeadingForCollision())
        {
            Vector3 collisionAvoidDir = ObstacleRays();
            Vector3 collisionAvoidForce = SteerTowards(collisionAvoidDir) * settings.avoidCollisionWeight;
            acceleration += collisionAvoidForce;
        }

        velocity += acceleration * Time.deltaTime;
        float speed = velocity.magnitude;
        Vector3 dir = velocity / speed;

        speed = Mathf.Clamp(speed, settings.minSpeed, settings.maxSpeed);
        velocity = dir * speed;

        cachedTransform.position += velocity * Time.deltaTime;
        cachedTransform.forward = dir;
        cachedTransform.position = new Vector3(cachedTransform.position.x, Mathf.Clamp(cachedTransform.position.y, HeightRange.x, HeightRange.y), cachedTransform.position.z);
        position = cachedTransform.position;
        forward = dir;
    }

    bool IsHeadingForCollision()
    {
        RaycastHit hit;
        if (Physics.SphereCast(position, settings.boundsRadius, forward, out hit, settings.collisionAvoidDistance, settings.obstacleMask))
        {
            return true;
        }
        return false;
    }

    Vector3 ObstacleRays()
    {
        Vector3[] rayDirections = BoidDirections.viewDirections;
        for (int i = 0; i < rayDirections.Length; i++)
        {
            Vector3 dir = cachedTransform.TransformDirection(rayDirections[i]);
            Ray ray = new Ray(position, dir);
            if (!Physics.SphereCast(ray, settings.boundsRadius, settings.collisionAvoidDistance, settings.obstacleMask))
            {
                return dir;
            }
        }
        return forward;
    }

    Vector3 SteerTowards(Vector3 direction)
    {
        Vector3 v = direction.normalized * settings.maxSpeed - velocity;
        return Vector3.ClampMagnitude(v, settings.maxSteerForce);
    }
}

