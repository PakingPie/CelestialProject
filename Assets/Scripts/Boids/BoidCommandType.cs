// BoidCommand.cs - Command types for the flock
using UnityEngine;

public enum BoidCommandType
{
    None,
    FollowTarget,      // Follow a specific transform (player ship)
    AttackTarget,      // Attack a specific target
    MoveToPosition,    // Move to a world position
    Patrol,            // Patrol between waypoints
    ReturnToBase,      // Return to carrier/base
    FormUp,            // Tighten formation around leader
    BreakFormation,    // Free movement / engage at will
    Defend,            // Defend a specific target
    Hold,              // Hold current position
    Spawn            // Spawn new boids (not a movement command)
}

[System.Serializable]
public class BoidCommand
{
    public BoidCommandType Type;
    public Transform Target;
    public Vector3 Position;
    public float Radius;
    public float Duration; // 0 = indefinite

    private float _startTime;

    public bool IsExpired => Duration > 0 && Time.time - _startTime > Duration;

    public BoidCommand(BoidCommandType type)
    {
        Type = type;
        _startTime = Time.time;
    }

    public static BoidCommand Spawn(int count = -1)
    {
        return new BoidCommand(BoidCommandType.Spawn)
        {
            Radius = count  // Reuse Radius field to store spawn count
        };
    }

    public static BoidCommand Follow(Transform target)
    {
        return new BoidCommand(BoidCommandType.FollowTarget)
        {
            Target = target
        };
    }

    public static BoidCommand Attack(Transform target)
    {
        return new BoidCommand(BoidCommandType.AttackTarget)
        {
            Target = target
        };
    }

    public static BoidCommand MoveTo(Vector3 position, float radius = 50f)
    {
        return new BoidCommand(BoidCommandType.MoveToPosition)
        {
            Position = position,
            Radius = radius
        };
    }

    public static BoidCommand ReturnToBase()
    {
        return new BoidCommand(BoidCommandType.ReturnToBase);
    }

    public static BoidCommand Defend(Transform target, float radius = 200f)
    {
        return new BoidCommand(BoidCommandType.Defend)
        {
            Target = target,
            Radius = radius
        };
    }

    public static BoidCommand Hold(float duration = 0f)
    {
        return new BoidCommand(BoidCommandType.Hold)
        {
            Duration = duration
        };
    }
}