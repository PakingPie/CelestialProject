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
    
    [Header("Formation Settings")]
    public bool useFormation = true;
    public FormationType formationType = FormationType.V;
    public float formationSpacing = 100f;      // Increase default
    public float formationTightness = 50f;     // Increase default
    public float formationMatchSpeed = 20f;    // Increase default
    public float formationDeadZone = 10f;      // NEW
    public float formationUrgencyRange = 100f; // NEW

    [Header("Mixed Formation Spacing")]
    [Tooltip("Spacing multiplier for Large ships (Cruiser, Battleship, Carrier, Station, Platform).")]
    public float capitalSpacingMultiplier = 2.0f;
    [Tooltip("Spacing multiplier for Medium ships (Frigate, Destroyer).")]
    public float escortSpacingMultiplier = 1.5f;

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

    [Header("Newtonian Physics")]
    [Tooltip("Linear drag coefficient — controls velocity decay and drift feel.")]
    [Range(0f, 2f)]
    public float linearDrag = 0.3f;
    [Tooltip("Rotational drag — how quickly angular velocity decays (higher = snappier stops).")]
    [Range(0f, 10f)]
    public float rotationalDrag = 4f;
    [Tooltip("Torque strength for turning (degrees/s²). Higher = faster turning. Keep proportional to maxSteerForce/maxSpeed.")]
    public float torqueStrength = 12f;
    [Tooltip("Maximum angular speed (degrees/s).")]
    public float maxAngularSpeed = 45f;
    [Tooltip("How much velocity rotates with the ship (0 = full drift, 1 = full coupling).")]
    [Range(0f, 1f)]
    public float velocityCoupling = 0.75f;
    [Tooltip("Lateral/vertical thruster authority as a fraction of forward thrust. 0 = turn-then-burn only, 1 = omnidirectional. Models RCS ports.")]
    [Range(0f, 1f)]
    public float rcsAuthority = 0.15f;
    [Tooltip("Reverse thruster authority as a fraction of forward thrust. 0.35 ≈ fighter with weak aft thrusters.")]
    [Range(0f, 1f)]
    public float reverseThrustRatio = 0.35f;
    [Tooltip("RCS authority multiplier for attack profiles that use custom facing (orbiting / strafing). Effective lateral = rcsAuthority × combatRcsBoost.")]
    [Range(1f, 8f)]
    public float combatRcsBoost = 3f;

    [Header("Ship Size Physics Multipliers")]
    [Tooltip("Torque multiplier for Large ships (lower = slower turning).")]
    public float capitalTorqueMultiplier = 0.3f;
    [Tooltip("Drag multiplier for Large ships (lower = more drift).")]
    public float capitalDragMultiplier = 0.6f;
    [Tooltip("Torque multiplier for Medium ships.")]
    public float escortTorqueMultiplier = 0.6f;
    [Tooltip("Drag multiplier for Medium ships.")]
    public float escortDragMultiplier = 0.8f;

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

    [Header("Spherical Leash")]
    [Tooltip("Enable spherical boundary around the flock target.")]
    public bool useLeash = false;
    [Tooltip("Maximum distance a boid can travel from the leash center.")]
    public float leashRadius = 5000f;
    [Tooltip("Fraction of radius where soft steering begins (e.g. 0.8 = at 80% of radius).")]
    [Range(0.5f, 0.99f)] public float leashSoftEdge = 0.85f;
    [Tooltip("Strength of the leash pull-back force.")]
    [Range(0.5f, 5f)] public float leashStrength = 2f;

    [Header("Moorage")]
    [Tooltip("Type of moorage for this flock.")]
    public MoorageType moorageType = MoorageType.None;
    [Tooltip("If true, boids start in docked/parked state and must be launched.")]
    public bool startDocked = false;
    [Tooltip("Time between each boid launch (staggered launch).")]
    public float launchInterval = 0.5f;
    [Tooltip("Drift speed for parked boids (station parking only).")]
    public float parkedDriftSpeed = 0.5f;
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

public enum MoorageType
{
    None,
    CarrierDocking,
    StationParking
}