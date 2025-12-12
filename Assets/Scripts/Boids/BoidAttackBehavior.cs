using UnityEngine;

public class BoidAttackBehavior : MonoBehaviour
{
    [SerializeField] private BoidAttackProfile _attackProfile;
    
    private Boid _boid;
    private float _strafeTimer;
    private int _strafeDirection = 1;
    private float _orbitAngle;
    private int _committedSide = 0;
    private bool _hasReachedEngagementRange = false;
    
    public BoidAttackProfile Profile => _attackProfile;
    
    // Lazy initialization for _boid
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
        _orbitAngle = Random.Range(0f, 360f);
        // Remove the GetComponent call from here
    }
    
    public void SetProfile(BoidAttackProfile profile)
    {
        _attackProfile = profile;
    }
    
    public void ResetSideCommitment()
    {
        _committedSide = 0;
        _hasReachedEngagementRange = false;
    }
    
    public bool RequiresCustomFacing()
    {
        if (_attackProfile == null) return false;
        return _attackProfile.preferredAngle == AttackAngle.Side || 
               _attackProfile.preferredAngle == AttackAngle.Rear;
    }
    
    public Vector3 GetDesiredMovementDirection(Vector3 targetPosition, Vector3 targetForward)
    {
        if (_attackProfile == null || Boid == null)
            return (targetPosition - transform.position).normalized;
        
        Vector3 toTarget = targetPosition - Boid.position;
        float distance = toTarget.magnitude;
        Vector3 toTargetNorm = distance > 0.01f ? toTarget / distance : Boid.forward;
        
        switch (_attackProfile.preferredAngle)
        {
            case AttackAngle.Side:
                return GetBroadsideMovement(targetPosition, distance, toTargetNorm);
                
            case AttackAngle.Orbit:
                return GetOrbitMovement(targetPosition, distance, toTargetNorm);
                
            case AttackAngle.Front:
                return GetFrontAttackMovement(targetPosition, distance, toTargetNorm);
                
            case AttackAngle.Rear:
                return GetRearAttackMovement(targetPosition, distance, toTargetNorm, targetForward);
                
            default:
                return toTargetNorm;
        }
    }
    
    private Vector3 GetFrontAttackMovement(Vector3 targetPosition, float distance, Vector3 toTargetNorm)
    {
        float distanceCorrectionStrength = 0f;
        
        if (distance < _attackProfile.minDistance)
        {
            distanceCorrectionStrength = -Mathf.Clamp01((_attackProfile.minDistance - distance) / _attackProfile.minDistance) * 2f;
        }
        else if (distance > _attackProfile.maxDistance)
        {
            distanceCorrectionStrength = 1f;
        }
        else if (distance > _attackProfile.engagementDistance)
        {
            distanceCorrectionStrength = 0.5f;
        }
        else
        {
            float error = distance - _attackProfile.engagementDistance;
            distanceCorrectionStrength = Mathf.Clamp(error / _attackProfile.engagementDistance, -0.5f, 0.5f);
        }
        
        // Return direction, not just strength
        return toTargetNorm * Mathf.Max(distanceCorrectionStrength, 0.3f);
    }
    
    private Vector3 GetBroadsideMovement(Vector3 targetPosition, float distance, Vector3 toTargetNorm)
    {
        if (distance > _attackProfile.maxDistance)
        {
            _hasReachedEngagementRange = false;
            return toTargetNorm;
        }
        
        _hasReachedEngagementRange = true;
        
        if (_committedSide == 0)
        {
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toTargetNorm).normalized;
            Vector3 currentOffset = Boid.position - targetPosition;
            float sideOffset = Vector3.Dot(currentOffset, perpendicular);
            float velocitySide = Vector3.Dot(Boid.Velocity.normalized, perpendicular);
            float combinedSide = sideOffset * 0.3f + velocitySide * 0.7f;
            
            if (Mathf.Abs(combinedSide) < 0.1f)
            {
                combinedSide = Random.value > 0.5f ? 1f : -1f;
            }
            
            _committedSide = combinedSide >= 0 ? 1 : -1;
        }
        
        Vector3 tangent = Vector3.Cross(Vector3.up, toTargetNorm).normalized * _committedSide;
        
        float radialStrength = 0f;
        
        if (distance < _attackProfile.minDistance)
        {
            radialStrength = -Mathf.Clamp01((_attackProfile.minDistance - distance) / (_attackProfile.minDistance * 0.5f)) * 2f;
        }
        else if (distance > _attackProfile.engagementDistance + 100f)
        {
            float excess = distance - _attackProfile.engagementDistance;
            radialStrength = Mathf.Clamp01(excess / _attackProfile.engagementDistance) * 0.8f;
        }
        else if (distance < _attackProfile.engagementDistance - 100f)
        {
            float deficit = _attackProfile.engagementDistance - distance;
            radialStrength = -Mathf.Clamp01(deficit / _attackProfile.engagementDistance) * 0.5f;
        }
        
        Vector3 radial = toTargetNorm * radialStrength;
        float tangentStrength = 1f - Mathf.Abs(radialStrength) * 0.5f;
        Vector3 movement = tangent * tangentStrength + radial;
        
        return movement.normalized;
    }
    
    private Vector3 GetRearAttackMovement(Vector3 targetPosition, float distance, Vector3 toTargetNorm, Vector3 targetForward)
    {
        Vector3 behindTarget = targetPosition + targetForward * _attackProfile.engagementDistance;
        Vector3 toBehind = (behindTarget - Boid.position).normalized;
        
        if (distance < _attackProfile.minDistance)
        {
            return -toTargetNorm;
        }
        
        return toBehind;
    }
    
    private Vector3 GetOrbitMovement(Vector3 targetPosition, float distance, Vector3 toTargetNorm)
    {
        float direction = _attackProfile.preferClockwise ? 1f : -1f;
        Vector3 tangent = Vector3.Cross(Vector3.up, toTargetNorm).normalized * direction;
        
        float distanceError = distance - _attackProfile.engagementDistance;
        float radialStrength = Mathf.Clamp(distanceError / _attackProfile.engagementDistance, -1f, 1f);
        
        Vector3 movement = tangent + toTargetNorm * radialStrength * 0.5f;
        return movement.normalized;
    }
    
    public Vector3 GetDesiredFacingDirection(Vector3 targetPosition)
    {
        if (_attackProfile == null || Boid == null)
            return (targetPosition - transform.position).normalized;
        
        Vector3 toTarget = targetPosition - Boid.position;
        float distance = toTarget.magnitude;
        Vector3 toTargetNorm = distance > 0.01f ? toTarget / distance : Boid.forward;
        
        switch (_attackProfile.preferredAngle)
        {
            case AttackAngle.Front:
                return toTargetNorm;
                
            case AttackAngle.Side:
                if (_hasReachedEngagementRange && distance <= _attackProfile.maxDistance)
                {
                    return GetBroadsideFacing(toTargetNorm);
                }
                return Boid.Velocity.sqrMagnitude > 0.01f ? Boid.Velocity.normalized : Boid.forward;
                
            case AttackAngle.Rear:
                return -toTargetNorm;
                
            case AttackAngle.Orbit:
            case AttackAngle.Strafe:
                return toTargetNorm;
                
            default:
                return toTargetNorm;
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
        
        Vector3 facing = Vector3.Cross(Vector3.up, toTarget).normalized;
        return facing * _committedSide;
    }
}