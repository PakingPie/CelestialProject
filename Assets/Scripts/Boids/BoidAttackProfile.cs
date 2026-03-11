// BoidAttackProfile.cs - REPLACEMENT
using UnityEngine;

public enum AttackMode
{
    Charge,           // Close distance aggressively, get as close as possible
    MaintainDistance, // Stay at preferred engagement distance
    HitAndRun,        // Approach, engage briefly, retreat, repeat
    Orbit             // Circle around target at distance
}

public enum AttackFacing
{
    Forward,    // Face target directly (bow weapons)
    Broadside,  // Show side to target (broadside weapons)
    Rear        // Face away from target (rear weapons, kiting)
}

[CreateAssetMenu(menuName = "Boid/Attack Profile")]
public class BoidAttackProfile : ScriptableObject
{
    [Header("Attack Pattern")]
    public AttackMode attackMode = AttackMode.MaintainDistance;
    public AttackFacing facing = AttackFacing.Forward;
    
    [Header("Distances")]
    [Tooltip("Preferred combat distance")]
    public float engagementDistance = 150f;
    [Tooltip("Minimum safe distance - will retreat if closer")]
    public float minDistance = 80f;
    [Tooltip("Maximum distance - will approach if farther")]
    public float maxDistance = 300f;
    
    [Header("Hit and Run")]
    [Tooltip("How long to stay in engagement range before retreating")]
    public float engageTime = 3f;
    [Tooltip("Distance to retreat to before approaching again")]
    public float retreatDistance = 400f;
    [Tooltip("Time to stay at retreat distance before re-engaging")]
    public float regroupTime = 2f;
    
    [Header("Orbit")]
    [Tooltip("Angular speed for orbit mode (radians per second)")]
    public float orbitSpeed = 0.5f;
    public bool preferClockwise = true;
    
    [Header("Speed Modifiers")]
    public float approachSpeedMultiplier = 1f;
    public float engageSpeedMultiplier = 1f;
    public float retreatSpeedMultiplier = 1.5f;
    
    [Header("Behavior Tuning")]
    [Range(0f, 1f)]
    [Tooltip("How strictly to maintain facing angle vs movement direction")]
    public float facingStrictness = 0.8f;
    [Tooltip("Allow reversing to maintain distance (for ships that can)")]
    public bool allowReverseThrust = false;

    [Header("Squad Discipline")]
    [Tooltip("Multiplier for how strongly this profile obeys squad cohesion and leash rules.")]
    public float squadDisciplineMultiplier = 1f;
}