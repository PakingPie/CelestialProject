// AttackProfile.cs
using UnityEngine;

public enum AttackAngle
{
    Front,      // Direct charge
    Side,       // Broadside attack (like naval ships)
    Rear,       // Attack from behind
    Orbit,      // Circle around target
    Strafe      // Side-to-side passes
}

[CreateAssetMenu(menuName = "Boid/Attack Profile")]
public class BoidAttackProfile : ScriptableObject
{
    [Header("Attack Approach")]
    public AttackAngle preferredAngle = AttackAngle.Front;
    
    [Header("Distances")]
    public float engagementDistance = 150f;     // Preferred combat distance
    public float minDistance = 80f;              // Don't get closer than this
    public float maxDistance = 300f;             // Don't stay farther than this
    
    [Header("Angle Settings")]
    [Range(0f, 180f)]
    public float attackAngleDegrees = 90f;      // For Side: 90 = perpendicular
    [Range(0f, 1f)]
    public float angleStrictness = 0.8f;        // How strictly to maintain angle
    
    [Header("Movement")]
    public float orbitSpeed = 0.5f;             // For Orbit mode
    public float strafeInterval = 3f;           // For Strafe mode
    public bool preferClockwise = true;         // Orbit/strafe direction
    
    [Header("Approach Behavior")]
    public float approachSpeedMultiplier = 1f;
    public float retreatSpeedMultiplier = 1.5f;
    public bool allowReverseThrust = false;
}