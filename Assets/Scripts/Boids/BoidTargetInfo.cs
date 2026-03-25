using UnityEngine;

public class BoidTargetInfo
{
    public Transform Target;
    public float ThreatLevel;
    public float Distance;
    public int AssignedBoidCount;
    public float LastSeenTime;
    public Vector3 LastKnownPosition;
    public Vector3 EstimatedVelocity;
    public WeaponBase[] CachedWeapons;
    public VehicleBase CachedVehicle;

    public bool IsValid
    {
        get
        {
            // Use Unity's implicit bool operator which handles destroyed objects
            if (!Target) return false;
            return Time.time - LastSeenTime < 5f;
        }
    }
}