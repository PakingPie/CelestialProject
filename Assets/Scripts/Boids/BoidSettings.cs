using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CombatAnchorMode
{
    None,
    Leader,
    FlockCenter,
    CommandAnchor
}

[CreateAssetMenu]
public class BoidSettings : ScriptableObject
{
    public float minSpeed = 2.0f;
    public float maxSpeed = 5.0f;
    public float perceptionRadius = 2.5f;
    public float maxSteerForce = 3.0f;

    public float alignWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float separateWeight = 1.0f;

    public float targetWeight = 1.0f;

    [Header("Separation")]
    public float separationRadius = 50f;        // One radius for all boid-to-boid
    public float separationWeight = 3f;         // One weight

    [Header("Obstacle Avoidance")]
    public float obstacleDetectionRange = 200f; // For ObstacleRegistry queries
    public float obstacleAvoidanceWeight = 10f;

    [Header("Collisions")]
    public LayerMask obstacleMask;
    public float boundsRadius = 0.27f;
    
    [Header("Formation Settings")]
    public bool useFormation = true;
    public FormationType formationType = FormationType.V;
    public float formationSpacing = 100f;      // Increase default
    public float formationTightness = 50f;     // Increase default
    public float formationMatchSpeed = 20f;    // Increase default
    public float formationDeadZone = 10f;      // NEW
    public float formationUrgencyRange = 100f; // NEW

    [Header("Sub-Flock Settings")]
    [Tooltip("Split the flock into sub-flocks (flights/elements) that each maintain their own internal formation.")]
    public bool useSubFlocks = false;
    [Tooltip("Preferred number of boids per sub-flock.")]
    [Range(2, 10)] public int preferredSubFlockSize = 4;
    [Tooltip("Minimum sub-flock size. Remainders below this merge into the last sub-flock.")]
    [Range(2, 10)] public int minSubFlockSize = 2;
    [Tooltip("Maximum sub-flock size.")]
    [Range(2, 10)] public int maxSubFlockSize = 10;
    [Tooltip("Formation pattern used within each sub-flock.")]
    public FormationType subFlockFormationType = FormationType.V;
    [Tooltip("Spacing between boids inside a sub-flock (typically tighter than parent spacing).")]
    public float subFlockFormationSpacing = 60f;

    [Header("Combat Behavior")]
    public float combatCohesionMultiplier = 0.2f;
    public float combatSeparationMultiplier = 2f;
    public float combatAlignmentMultiplier = 0.5f;
    public float returnToFormationDelay = 3f;

    [Header("Combat Cohesion")]
    public CombatAnchorMode combatAnchorMode = CombatAnchorMode.Leader;
    public float combatAnchorWeight = 1.25f;
    public float combatAnchorSlackRadius = 150f;
    public float combatLeashRadius = 400f;
    public float combatLeashWeight = 3f;
    [Range(0f, 1f)] public float combatSlotRetention = 0.35f;
    public float combatRegroupHysteresis = 75f;
    public float combatTargetPursuitWeight = 1.5f;

    [Header("Adaptive Combat Morale")]
    [Tooltip("Enable morale-based adaptive combat behavior.")]
    public bool useAdaptiveMorale = false;
    [Tooltip("Morale score above this = Confident (keep formation in combat).")]
    [Range(0f, 1f)] public float confidentThreshold = 0.7f;
    [Tooltip("Morale score below this = Broken (flee from combat).")]
    [Range(0f, 1f)] public float brokenThreshold = 0.3f;
    [Tooltip("Extra margin needed to transition UP a morale state (prevents flickering).")]
    [Range(0f, 0.2f)] public float moraleHysteresis = 0.05f;
    [Tooltip("Weight of flock HP ratio in morale score.")]
    [Range(0f, 1f)] public float healthWeight = 0.6f;
    [Tooltip("Weight of flock member count ratio in morale score.")]
    [Range(0f, 1f)] public float strengthWeight = 0.4f;
    [Tooltip("Speed multiplier when fleeing (Broken morale).")]
    public float fleeSpeedMultiplier = 1.5f;
    [Tooltip("How much formation is blended into combat when Confident (0=none, 1=full formation).")]
    [Range(0f, 1f)] public float confidentFormationWeight = 0.5f;
}

public enum CombatMorale
{
    Confident,
    Cautious,
    Broken
}

public enum FormationType
{
    V,
    Line,
    Wedge,
    Box,
    Circle,
    Echelon,
    Sphere,
    Helix,
    Wall
}