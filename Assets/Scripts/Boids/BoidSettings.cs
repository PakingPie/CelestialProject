using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class BoidSettings : ScriptableObject
{
    public float minSpeed = 2.0f;
    public float maxSpeed = 5.0f;
    public float perceptionRadius = 2.5f;
    public float avoidanceRadius = 1.0f;
    public float maxSteerForce = 3.0f;

    public float alignWeight = 1.0f;
    public float cohesionWeight = 1.0f;
    public float separateWeight = 1.0f;

    public float targetWeight = 1.0f;

    [Header("Collisions")]
    public LayerMask obstacleMask;
    public float boundsRadius = 0.27f;
    public float avoidCollisionWeight = 10.0f;
    public float collisionAvoidDistance = 5.0f;

    [Header("Formation Settings")]
    public bool useFormation = true;
    public FormationType formationType = FormationType.V;
    public float formationSpacing = 5f;
    public float formationTightness = 2f;
    public float formationMatchSpeed = 0.5f;

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
    Echelon
}