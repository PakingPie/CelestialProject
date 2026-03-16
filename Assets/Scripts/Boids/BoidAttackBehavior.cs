using UnityEngine;

public class BoidAttackBehavior : MonoBehaviour
{
    [SerializeField] private BoidAttackProfile _profile;

    private Boid _boid;
    
    // State machine for Hit and Run
    public enum HitAndRunPhase { Approaching, Engaging, Retreating, Regrouping }
    private HitAndRunPhase _hitAndRunPhase = HitAndRunPhase.Approaching;
    private float _phaseTimer = 0f;
    
    // Orbit state
    private float _orbitAngle;
    
    // Side commitment (for broadside consistency)
    private int _committedSide = 0;
    
    // Speed multiplier output
    private float _currentSpeedMultiplier = 1f;
    
    public BoidAttackProfile Profile => _profile;
    public float SpeedMultiplier => _currentSpeedMultiplier;
    public HitAndRunPhase CurrentPhase => _hitAndRunPhase; // For debugging

    private Boid Boid
    {
        get
        {
            if (_boid == null)
                _boid = GetComponent<Boid>();
            return _boid;
        }
    }

    void Awake()
    {
        _orbitAngle = Random.Range(0f, Mathf.PI * 2f);
    }

    public void SetProfile(BoidAttackProfile profile)
    {
        _profile = profile;
        ResetState();
    }

    public void ResetState()
    {
        _committedSide = 0;
        _hitAndRunPhase = HitAndRunPhase.Approaching;
        _phaseTimer = 0f;
    }

    public void ResetSideCommitment()
    {
        _committedSide = 0;
    }

    public bool RequiresCustomFacing()
    {
        if (_profile == null) return false;
        return _profile.facing == AttackFacing.Broadside || 
               _profile.facing == AttackFacing.Rear;
    }

    /// <summary>
    /// Get the desired movement direction based on attack mode and current state.
    /// </summary>
    public Vector3 GetDesiredMovementDirection(Vector3 targetPosition, Vector3 targetForward)
    {
        if (_profile == null || Boid == null)
            return (targetPosition - transform.position).normalized;

        Vector3 toTarget = targetPosition - Boid.position;
        float distance = toTarget.magnitude;
        Vector3 toTargetDir = distance > 0.01f ? toTarget / distance : Boid.forward;

        Vector3 movement;
        
        switch (_profile.attackMode)
        {
            case AttackMode.Charge:
                movement = GetChargeMovement(distance, toTargetDir);
                break;
                
            case AttackMode.MaintainDistance:
                movement = GetMaintainDistanceMovement(distance, toTargetDir);
                break;
                
            case AttackMode.HitAndRun:
                movement = GetHitAndRunMovement(distance, toTargetDir);
                break;
                
            case AttackMode.Orbit:
                movement = GetOrbitMovement(targetPosition, distance, toTargetDir);
                break;
                
            default:
                movement = toTargetDir;
                break;
        }

        // Apply facing-based movement adjustments for broadside
        if (_profile.facing == AttackFacing.Broadside)
        {
            movement = ApplyBroadsideMovement(movement, distance, toTargetDir);
        }

        return movement.normalized;
    }

    #region Attack Mode Implementations

    private Vector3 GetChargeMovement(float distance, Vector3 toTargetDir)
    {
        _currentSpeedMultiplier = _profile.approachSpeedMultiplier;
        float facingDot = Vector3.Dot(Boid.forward, toTargetDir);
        
        // Always close distance, but respect minimum
        if (distance < _profile.minDistance)
        {
            // Once the target has slipped behind us, stop extending away and re-engage.
            if (facingDot < -0.1f)
            {
                _currentSpeedMultiplier = _profile.engageSpeedMultiplier;
                return toTargetDir;
            }

            // Too close - back off slightly
            _currentSpeedMultiplier = _profile.retreatSpeedMultiplier;
            return -toTargetDir;
        }
        
        return toTargetDir;
    }

    private Vector3 GetMaintainDistanceMovement(float distance, Vector3 toTargetDir)
    {
        float distanceError = distance - _profile.engagementDistance;
        
        if (distance < _profile.minDistance)
        {
            // Too close - retreat urgently
            _currentSpeedMultiplier = _profile.retreatSpeedMultiplier;
            return -toTargetDir;
        }
        else if (distance > _profile.maxDistance)
        {
            // Too far - approach
            _currentSpeedMultiplier = _profile.approachSpeedMultiplier;
            return toTargetDir;
        }
        else
        {
            // In range - make small adjustments
            _currentSpeedMultiplier = _profile.engageSpeedMultiplier;
            
            float adjustment = Mathf.Clamp(distanceError / _profile.engagementDistance, -0.5f, 0.5f);
            
            // Mostly tangential movement with slight distance correction
            Vector3 tangent = Vector3.Cross(Vector3.up, toTargetDir).normalized;
            if (_committedSide == 0)
                _committedSide = Random.value > 0.5f ? 1 : -1;
            
            return tangent * _committedSide * 0.7f + toTargetDir * adjustment;
        }
    }

    private Vector3 GetHitAndRunMovement(float distance, Vector3 toTargetDir)
    {
        _phaseTimer += Time.deltaTime;

        switch (_hitAndRunPhase)
        {
            case HitAndRunPhase.Approaching:
                _currentSpeedMultiplier = _profile.approachSpeedMultiplier;
                
                // Transition to Engaging when in range
                if (distance <= _profile.engagementDistance)
                {
                    _hitAndRunPhase = HitAndRunPhase.Engaging;
                    _phaseTimer = 0f;
                }
                return toTargetDir;

            case HitAndRunPhase.Engaging:
                _currentSpeedMultiplier = _profile.engageSpeedMultiplier;
                
                // Stay in engagement range, make attack passes
                // Transition to Retreating after engage time
                if (_phaseTimer >= _profile.engageTime)
                {
                    _hitAndRunPhase = HitAndRunPhase.Retreating;
                    _phaseTimer = 0f;
                }
                
                // Maintain engagement distance while engaging
                float distError = distance - _profile.engagementDistance;
                float correction = Mathf.Clamp(distError / _profile.engagementDistance, -0.5f, 0.5f);
                
                Vector3 tangent = Vector3.Cross(Vector3.up, toTargetDir).normalized;
                if (_committedSide == 0)
                    _committedSide = Random.value > 0.5f ? 1 : -1;
                
                return tangent * _committedSide * 0.6f + toTargetDir * correction;

            case HitAndRunPhase.Retreating:
                _currentSpeedMultiplier = _profile.retreatSpeedMultiplier;
                
                // Transition to Regrouping when far enough
                if (distance >= _profile.retreatDistance)
                {
                    _hitAndRunPhase = HitAndRunPhase.Regrouping;
                    _phaseTimer = 0f;
                }
                return -toTargetDir;

            case HitAndRunPhase.Regrouping:
                _currentSpeedMultiplier = _profile.engageSpeedMultiplier * 0.5f;
                
                // Maintain retreat distance briefly, then re-engage
                if (_phaseTimer >= _profile.regroupTime)
                {
                    _hitAndRunPhase = HitAndRunPhase.Approaching;
                    _phaseTimer = 0f;
                    _committedSide = 0; // Pick new side for next pass
                }
                
                // Drift tangentially while regrouping
                Vector3 regroupTangent = Vector3.Cross(Vector3.up, toTargetDir).normalized;
                float regroupCorrection = Mathf.Clamp((distance - _profile.retreatDistance) / _profile.retreatDistance, -0.3f, 0.3f);
                return regroupTangent * (_committedSide != 0 ? _committedSide : 1) * 0.8f + toTargetDir * regroupCorrection;

            default:
                return toTargetDir;
        }
    }

    private Vector3 GetOrbitMovement(Vector3 targetPosition, float distance, Vector3 toTargetDir)
    {
        _currentSpeedMultiplier = _profile.engageSpeedMultiplier;
        
        float direction = _profile.preferClockwise ? 1f : -1f;
        Vector3 tangent = Vector3.Cross(Vector3.up, toTargetDir).normalized * direction;

        // Update orbit angle
        _orbitAngle += _profile.orbitSpeed * Time.deltaTime * direction;

        // Maintain engagement distance while orbiting
        float distanceError = distance - _profile.engagementDistance;
        float radialStrength;
        
        if (distance < _profile.minDistance)
        {
            radialStrength = -1f;
            _currentSpeedMultiplier = _profile.retreatSpeedMultiplier;
        }
        else if (distance > _profile.maxDistance)
        {
            radialStrength = 1f;
            _currentSpeedMultiplier = _profile.approachSpeedMultiplier;
        }
        else
        {
            radialStrength = Mathf.Clamp(distanceError / _profile.engagementDistance, -0.5f, 0.5f);
        }

        return (tangent + toTargetDir * radialStrength).normalized;
    }

    #endregion

    #region Facing Adjustments

    private Vector3 ApplyBroadsideMovement(Vector3 baseMovement, float distance, Vector3 toTargetDir)
    {
        // Commit to a side
        if (_committedSide == 0)
        {
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toTargetDir).normalized;
            float sideOffset = Vector3.Dot(Boid.position - (Boid.position + toTargetDir * distance), perpendicular);
            float velocitySide = Vector3.Dot(Boid.Velocity.normalized, perpendicular);
            float combinedSide = sideOffset * 0.3f + velocitySide * 0.7f;
            
            _committedSide = (Mathf.Abs(combinedSide) < 0.1f) 
                ? (Random.value > 0.5f ? 1 : -1) 
                : (combinedSide >= 0 ? 1 : -1);
        }

        Vector3 tangent = Vector3.Cross(Vector3.up, toTargetDir).normalized * _committedSide;
        
        // Blend tangential movement based on distance
        float tangentWeight = 0.5f;
        
        if (distance > _profile.minDistance && distance < _profile.maxDistance)
        {
            // In good range - prioritize tangential movement for broadside
            tangentWeight = 0.7f;
        }

        return Vector3.Lerp(baseMovement, tangent, tangentWeight);
    }

    /// <summary>
    /// Get the direction the ship should face (for rotation).
    /// </summary>
    public Vector3 GetDesiredFacingDirection(Vector3 targetPosition)
    {
        if (_profile == null || Boid == null)
            return (targetPosition - transform.position).normalized;

        Vector3 toTarget = targetPosition - Boid.position;
        float distance = toTarget.magnitude;
        Vector3 toTargetDir = distance > 0.01f ? toTarget / distance : Boid.forward;

        switch (_profile.facing)
        {
            case AttackFacing.Forward:
                return toTargetDir;
                
            case AttackFacing.Broadside:
                return GetBroadsideFacing(toTargetDir);
                
            case AttackFacing.Rear:
                return -toTargetDir;
                
            default:
                return toTargetDir;
        }
    }

    private Vector3 GetBroadsideFacing(Vector3 toTarget)
    {
        if (_committedSide == 0)
        {
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toTarget).normalized;
            float dot = Vector3.Dot(Boid.transform.right, toTarget);
            _committedSide = dot >= 0 ? 1 : -1;
        }

        // Face perpendicular to target direction
        return Vector3.Cross(Vector3.up, toTarget).normalized * _committedSide;
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (_profile == null || Boid == null) return;

        // Draw engagement ranges
        Gizmos.color = Color.green;
        DrawCircle(Boid.position, _profile.engagementDistance, 32);
        
        Gizmos.color = Color.red;
        DrawCircle(Boid.position, _profile.minDistance, 24);
        
        Gizmos.color = Color.yellow;
        DrawCircle(Boid.position, _profile.maxDistance, 32);

        if (_profile.attackMode == AttackMode.HitAndRun)
        {
            Gizmos.color = Color.cyan;
            DrawCircle(Boid.position, _profile.retreatDistance, 24);
            
            // Show current phase
            Vector3 labelPos = Boid.position + Vector3.up * 30f;
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(labelPos, $"Phase: {_hitAndRunPhase} ({_phaseTimer:F1}s)");
            #endif
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

    #endregion
}