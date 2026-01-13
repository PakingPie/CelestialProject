using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Combat Behavior")]
    public float combatCohesionMultiplier = 0.2f;
    public float combatSeparationMultiplier = 2f;
    public float combatAlignmentMultiplier = 0.5f;
    public float returnToFormationDelay = 3f;
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