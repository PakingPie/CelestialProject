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
    float velocityInheritance = 1f,
    float maxPredictionTime = 5f)
    {
        Vector3 relativePos = targetPos - shooterPos;
        Vector3 effectiveShooterVel = shooterVelocity * velocityInheritance;
        Vector3 relativeVel = targetVelocity - effectiveShooterVel;

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
                // FIX: Subtract effectiveShooterVel to compensate for velocity inheritance
                return targetPos + targetVelocity * lt - effectiveShooterVel * lt;
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

        // FIX: Return the correct aim point that compensates for velocity inheritance
        return targetPos + targetVelocity * t - effectiveShooterVel * t;
    }

    /// <summary>
    /// Calculates the intercept point accounting for constant target acceleration using iterative refinement.
    /// More accurate than CalculateInterceptPoint for thrusting targets at longer ranges.
    /// Falls back gracefully if no valid solution converges.
    /// </summary>
    public static Vector3 CalculateInterceptPointWithAcceleration(
    Vector3 shooterPos,
    Vector3 shooterVelocity,
    float projectileSpeed,
    Vector3 targetPos,
    Vector3 targetVelocity,
    Vector3 targetAcceleration,
    float velocityInheritance = 1f,
    float maxPredictionTime = 5f,
    int iterations = 4)
    {
        Vector3 effectiveShooterVel = shooterVelocity * velocityInheritance;

        // Seed with a simple distance-based time estimate
        float t = Mathf.Min(Vector3.Distance(targetPos, shooterPos) / projectileSpeed, maxPredictionTime);

        // Iterative refinement: each pass updates t using the target's accelerated predicted position
        for (int i = 0; i < iterations; i++)
        {
            Vector3 predictedTargetPos = targetPos + targetVelocity * t + 0.5f * targetAcceleration * (t * t);
            Vector3 relativeVec = predictedTargetPos - shooterPos - effectiveShooterVel * t;
            float newT = relativeVec.magnitude / projectileSpeed;
            newT = Mathf.Clamp(newT, 0f, maxPredictionTime);
            if (Mathf.Abs(newT - t) < 0.001f) break;
            t = newT;
        }

        if (t <= 0f) return Vector3.zero;

        Vector3 finalPredictedPos = targetPos + targetVelocity * t + 0.5f * targetAcceleration * (t * t);
        return finalPredictedPos - effectiveShooterVel * t;
    }

    /// <summary>
    /// Simple linear prediction fallback when intercept calculation fails.
    /// </summary>
    public static Vector3 CalculateSimpleLead(
    Vector3 shooterPos,
    Vector3 targetPos,
    Vector3 targetVelocity,
    float projectileSpeed,
    Vector3 shooterVelocity,        // Add this parameter
    float velocityInheritance = 1f, // Add this parameter
    float maxPredictionTime = 5f)
    {
        float distance = Vector3.Distance(shooterPos, targetPos);
        float timeToTarget = Mathf.Min(distance / projectileSpeed, maxPredictionTime);
        Vector3 effectiveShooterVel = shooterVelocity * velocityInheritance;
        return targetPos + targetVelocity * timeToTarget - effectiveShooterVel * timeToTarget;
    }

    /// <summary>
    /// Calculates an aim point that compensates for bullet deflection caused by all active
    /// BlackHoleGravity sources in the scene.
    /// Falls back to standard linear intercept when no gravity sources are active.
    /// </summary>
    public static Vector3 CalculateGravityCompensatedIntercept(
    Vector3 shooterPos,
    Vector3 shooterVelocity,
    float projectileSpeed,
    Vector3 targetPos,
    Vector3 targetVelocity,
    float velocityInheritance = 1f,
    float maxPredictionTime = 5f,
    int iterations = 6,
    float simTimeStep = 0.05f)
    {
        var gravitySources = BlackHoleGravity.ActiveGravitySources;
        if (gravitySources == null || gravitySources.Count == 0)
            return CalculateInterceptPoint(shooterPos, shooterVelocity, projectileSpeed,
                targetPos, targetVelocity, velocityInheritance, maxPredictionTime);

        Vector3 effectiveShooterVel = shooterVelocity * velocityInheritance;

        // Seed with standard linear intercept
        Vector3 aimPoint = CalculateInterceptPoint(shooterPos, shooterVelocity, projectileSpeed,
            targetPos, targetVelocity, velocityInheritance, maxPredictionTime);
        if (aimPoint == Vector3.zero)
            aimPoint = CalculateSimpleLead(shooterPos, targetPos, targetVelocity, projectileSpeed,
                shooterVelocity, velocityInheritance, maxPredictionTime);
        if (aimPoint == Vector3.zero)
            return Vector3.zero;

        for (int iter = 0; iter < iterations; iter++)
        {
            float dist = Vector3.Distance(shooterPos, aimPoint);
            float flightTime = Mathf.Min(dist / Mathf.Max(projectileSpeed, 0.01f), maxPredictionTime);
            if (flightTime <= 0f) break;

            Vector3 fireDir = (aimPoint - shooterPos).normalized;
            Vector3 bulletVel = fireDir * projectileSpeed + effectiveShooterVel;

            Vector3 simPos = shooterPos;
            Vector3 simVel = bulletVel;
            float elapsed = 0f;
            while (elapsed < flightTime)
            {
                float dt = Mathf.Min(simTimeStep, flightTime - elapsed);
                Vector3 totalDeltaV = Vector3.zero;

                for (int i = 0; i < gravitySources.Count; i++)
                {
                    BlackHoleGravity gravitySource = gravitySources[i];
                    if (gravitySource == null)
                        continue;

                    Vector3 toGravitySource = gravitySource.transform.position - simPos;
                    float distSqr = toGravitySource.sqrMagnitude;
                    float influenceSqr = gravitySource.InfluenceRadius * gravitySource.InfluenceRadius;
                    if (distSqr >= influenceSqr || distSqr <= Mathf.Epsilon)
                        continue;

                    float accel = Mathf.Max(gravitySource.GravitationalStrength / distSqr, gravitySource.MinAcceleration);
                    totalDeltaV += toGravitySource.normalized * (accel * dt);
                }

                simVel += totalDeltaV;
                simPos += simVel * dt;
                elapsed += dt;
            }

            Vector3 targetPredicted = targetPos + targetVelocity * flightTime;
            Vector3 deflection = simPos - aimPoint;
            aimPoint = targetPredicted - deflection;
        }

        return aimPoint;
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

        // Try AAMissile
        var missile = target.GetComponentInParent<AAMissile>();
        if (missile != null)
            return missile.Velocity;

        return Vector3.zero;
    }
}