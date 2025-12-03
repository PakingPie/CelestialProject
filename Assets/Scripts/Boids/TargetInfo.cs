using UnityEngine;

public class TargetInfo
{
    public Transform Target;
    public float ThreatLevel;
    public float Distance;
    public int AssignedBoidCount;
    public float LastSeenTime;
    public Vector3 LastKnownPosition;
    public Vector3 EstimatedVelocity;
    
    public bool IsValid => Target != null && Time.time - LastSeenTime < 5f;
}