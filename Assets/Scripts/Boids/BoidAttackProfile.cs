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

public enum LocalStyleBias
{
    Unspecified,
    Assault,
    Standoff,
    Skirmish,
    Orbiting,
    Interceptor,
    Broadside
}

[CreateAssetMenu(menuName = "Boid/Attack Profile")]
public class BoidAttackProfile : ScriptableObject
{
    [Header("Attack Pattern")]
    [Tooltip("Legacy local style hint. In the hybrid AI flow this acts as a movement-style fallback, not the squad's top-level tactical selector.")]
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

    [Header("Hybrid Tactical Tuning")]
    [Tooltip("Preferred local maneuver style once a higher-level tactical decision has already been made.")]
    public LocalStyleBias localStyleBias = LocalStyleBias.Unspecified;
    [Tooltip("Center of the preferred combat envelope. Falls back to engagementDistance when unset.")]
    public float desiredRangeCenter = 0f;
    [Tooltip("Half-width of the preferred combat envelope. Falls back to legacy range values when unset.")]
    public float desiredRangeTolerance = 0f;
    [Tooltip("Emergency near-distance threshold. Falls back to minDistance when unset.")]
    public float hardAvoidDistance = 0f;
    [Tooltip("Preferred breakaway distance before re-engaging. Falls back to retreatDistance when unset.")]
    public float breakawayDistance = 0f;
    [Tooltip("Minimum delay before re-engaging after a breakaway. Falls back to regroupTime when unset.")]
    public float reengageDelay = 0f;
    [Tooltip("Per-ship pursuit leash relative to squad anchor. Set to 0 to defer entirely to squad settings.")]
    public float maxPursuitAnchorDistance = 0f;
    [Range(0f, 1f)] public float focusFireAffinity = 0.5f;
    [Range(0f, 1f)] public float strafeBias = 0.5f;
    [Range(0f, 1f)] public float rejoinUrgency = 0.5f;
    [Range(0f, 1f)] public float defensiveEvasionBias = 0.5f;

    public LocalStyleBias PreferredLocalStyle => localStyleBias != LocalStyleBias.Unspecified ? localStyleBias : MapLegacyStyle(attackMode, facing);
    public float DesiredRangeCenter => desiredRangeCenter > 0f ? desiredRangeCenter : engagementDistance;
    public float HardAvoidDistance => hardAvoidDistance > 0f ? hardAvoidDistance : minDistance;
    public float DesiredRangeTolerance => desiredRangeTolerance > 0f ? desiredRangeTolerance : GetFallbackRangeTolerance();
    public float DesiredRangeMax => desiredRangeCenter > 0f && desiredRangeTolerance > 0f
        ? Mathf.Max(HardAvoidDistance, desiredRangeCenter + desiredRangeTolerance)
        : Mathf.Max(HardAvoidDistance, maxDistance);
    public float BreakawayDistance => breakawayDistance > 0f ? breakawayDistance : Mathf.Max(retreatDistance, HardAvoidDistance * 1.25f);
    public float ReengageDelay => reengageDelay > 0f ? reengageDelay : regroupTime;
    public float MaxPursuitAnchorDistance => Mathf.Max(0f, maxPursuitAnchorDistance);
    public float FocusFireAffinity => Mathf.Clamp01(focusFireAffinity);
    public float StrafeBias => Mathf.Clamp01(strafeBias);
    public float RejoinUrgency => Mathf.Clamp01(rejoinUrgency);
    public float DefensiveEvasionBias => Mathf.Clamp01(defensiveEvasionBias);

    private float GetFallbackRangeTolerance()
    {
        float upperBand = Mathf.Max(0f, maxDistance - DesiredRangeCenter);
        float lowerBand = Mathf.Max(0f, DesiredRangeCenter - HardAvoidDistance);
        return Mathf.Max(25f, upperBand, lowerBand);
    }

    private static LocalStyleBias MapLegacyStyle(AttackMode mode, AttackFacing attackFacing)
    {
        if (attackFacing == AttackFacing.Broadside)
            return LocalStyleBias.Broadside;

        switch (mode)
        {
            case AttackMode.Charge:
                return LocalStyleBias.Assault;
            case AttackMode.MaintainDistance:
                return LocalStyleBias.Standoff;
            case AttackMode.HitAndRun:
                return LocalStyleBias.Skirmish;
            case AttackMode.Orbit:
                return LocalStyleBias.Orbiting;
            default:
                return LocalStyleBias.Unspecified;
        }
    }
}