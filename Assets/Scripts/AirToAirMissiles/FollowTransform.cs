using UnityEngine;

/// <summary>
/// Makes this object follow a target transform's position and rotation
/// without being parented (avoiding scale inheritance from parent hierarchy).
/// </summary>
public class FollowTransform : MonoBehaviour
{
    [HideInInspector] public Transform target;
    [HideInInspector] public Vector3 localPositionOffset;
    [HideInInspector] public Quaternion localRotationOffset;
    
    /// <summary>
    /// If true, this object will be destroyed when the target is destroyed.
    /// Set to false before launching so the missile survives.
    /// </summary>
    [HideInInspector] public bool destroyWithTarget = true;

    /// <summary>
    /// Sets the target to follow with optional local offsets.
    /// </summary>
    public void SetTarget(Transform followTarget, Vector3 posOffset = default, Quaternion rotOffset = default)
    {
        target = followTarget;
        localPositionOffset = posOffset;
        localRotationOffset = rotOffset == default ? Quaternion.identity : rotOffset;
        destroyWithTarget = true;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            // Target lost - destroy self if still attached (not launched)
            if (destroyWithTarget)
            {
                Destroy(gameObject);
                return;
            }
            
            // Otherwise just disable following (missile has launched)
            enabled = false;
            return;
        }

        // Apply position: target's world position + offset rotated by target's rotation
        transform.position = target.TransformPoint(localPositionOffset);
        
        // Apply rotation: target's world rotation * local offset
        transform.rotation = target.rotation * localRotationOffset;
    }

    /// <summary>
    /// Call this when the missile launches to stop following.
    /// </summary>
    public void StopFollowing()
    {
        destroyWithTarget = false;
        target = null;
        enabled = false;
    }
}
