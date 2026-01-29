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
    /// Sets the target to follow with optional local offsets.
    /// </summary>
    public void SetTarget(Transform followTarget, Vector3 posOffset = default, Quaternion rotOffset = default)
    {
        target = followTarget;
        localPositionOffset = posOffset;
        localRotationOffset = rotOffset == default ? Quaternion.identity : rotOffset;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            // Target lost - disable following (missile may have launched)
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
        target = null;
        enabled = false;
    }
}
