using UnityEngine;

public static class LeadCalculator
{
    /// <summary>
    /// Calculates the intercept point for a projectile to hit a moving target.
    /// Returns Vector3.zero if no valid intercept exists.
    /// </summary>
    public static Vector3 CalculateInterceptPoint(
        Vector3 shooterPos,
        Vector3 shooterVelocity,
        float projectileSpeed,
        Vector3 targetPos,
        Vector3 targetVelocity,
        float maxPredictionTime = 5f)
    {
        Vector3 relativePos = targetPos - shooterPos;
        Vector3 relativeVel = targetVelocity - shooterVelocity;

        float a = Vector3.Dot(relativeVel, relativeVel) - (projectileSpeed * projectileSpeed);
        float b = 2f * Vector3.Dot(relativePos, relativeVel);
        float c = Vector3.Dot(relativePos, relativePos);

        // Handle case where a is nearly zero
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f)
                return Vector3.zero;

            float lt = -c / b;
            if (lt > 0f && lt <= maxPredictionTime)
                return targetPos + targetVelocity * lt;
            return Vector3.zero;
        }

        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0f)
            return Vector3.zero;

        float sqrtDiscriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b + sqrtDiscriminant) / (2f * a);
        float t2 = (-b - sqrtDiscriminant) / (2f * a);

        float t;
        if (t1 > 0f && t2 > 0f)
            t = Mathf.Min(t1, t2);
        else if (t1 > 0f)
            t = t1;
        else if (t2 > 0f)
            t = t2;
        else
            return Vector3.zero;

        t = Mathf.Min(t, maxPredictionTime);

        return targetPos + targetVelocity * t;
    }

    /// <summary>
    /// Simple linear prediction fallback when intercept calculation fails.
    /// </summary>
    public static Vector3 CalculateSimpleLead(
        Vector3 shooterPos,
        Vector3 targetPos,
        Vector3 targetVelocity,
        float projectileSpeed,
        float maxPredictionTime = 5f)
    {
        float distance = Vector3.Distance(shooterPos, targetPos);
        float timeToTarget = Mathf.Min(distance / projectileSpeed, maxPredictionTime);
        return targetPos + targetVelocity * timeToTarget;
    }

    /// <summary>
    /// Gets velocity from a target, trying multiple common components.
    /// </summary>
    public static Vector3 GetTargetVelocity(Transform target)
    {
        if (target == null) return Vector3.zero;

        // Try EnemyVehicle
        var enemyVehicle = target.GetComponentInParent<EnemyVehicle>();
        if (enemyVehicle != null)
            return enemyVehicle.Velocity;

        // Try Boid
        var boid = target.GetComponentInParent<Boid>();
        if (boid != null)
            return boid.Velocity;

        // Try Rigidbody
        var rb = target.GetComponentInParent<Rigidbody>();
        if (rb != null)
            return rb.linearVelocity;

        // Try PlayerShipMovement
        var playerShip = target.GetComponentInParent<PlayerShipMovement>();
        if (playerShip != null)
            return playerShip.Velocity;

        return Vector3.zero;
    }
}