using UnityEngine;
using System.Collections.Generic;

public static class GlobalHelper
{
    public enum VehicleType
    {
        // Small/Fast targets
        Missile = 0,
        Fighter = 1,
        Bomber = 2,
        Corvette = 3,

        // Medium targets
        Frigate = 10,
        Destroyer = 11,

        // Large targets
        Cruiser = 20,
        Battleship = 21,
        Carrier = 22,

        // Structures
        Station = 30,
        Platform = 31
    }

    public enum WeaponSize
    {
        Large,
        Medium,
        Small,
    }

    public enum WeaponType
    {
        Gun,
        MissileLauncher,
        LaserLauncher,
        PointDefense
    }

    [System.Flags]
    public enum Faction
    {
        None = 0,
        Player = 1 << 0,
        Ally = 1 << 1,
        Foe = 1 << 2,
        Neutral = 1 << 3
    }

    public enum AmmoType
    {
        Kinetic,
        Energy,
        Explosive,
        EMP,
        Plasma,
        Pierce
    }

    public enum GuidanceType
    {
        Pursuit,
        Lead
    };

    public enum IndicatorType
    {
        Enemy,
        Missile,
        Ally,
        Objective
    }

    public static string[] FactionNames = { "Player", "Ally", "Foe", "Neutral" };

    // Reusable list to avoid allocations
    private static List<VehicleBase> _tempVehicles = new List<VehicleBase>(500);
    private static List<GameObject> _tempGameObjects = new List<GameObject>(500);

    public enum Team
    {
        Neutral,
        Player,
        Foe,
        Ally
    }

    public class TeamIdentity : MonoBehaviour
    {
        [SerializeField] private Team _team = Team.Neutral;
        public Team Team => _team;
    }


    public class TrackedTarget
    {
        public Transform Transform { get; private set; }
        public IndicatorType Type { get; private set; }
        public System.Func<Vector3> GetVelocity { get; private set; }

        public bool IsValid => Transform != null;
        public Vector3 Position => Transform != null ? Transform.position : Vector3.zero;
        public Vector3 Velocity => GetVelocity != null ? GetVelocity() : Vector3.zero;

        public TrackedTarget(Transform transform, IndicatorType type, System.Func<Vector3> velocityGetter)
        {
            Transform = transform;
            Type = type;
            GetVelocity = velocityGetter;
        }
    }

    public class GunBarrel
    {
        public float RecoilLength = 0.3f;
        public float RecoverSpeed = 1f;

        private Transform barrel = null;
        private Vector3 startLocalPosition = Vector3.zero;
        private float recoil = 0f;

        public GunBarrel(Transform barrel, float recoilLength, float recoverSpeed)
        {
            this.barrel = barrel;
            RecoilLength = recoilLength;
            RecoverSpeed = recoverSpeed;
            startLocalPosition = this.barrel.localPosition;
        }

        public void FireRecoil()
        {
            recoil = RecoilLength;
        }

        public void ResetBarrelOverTime(float deltaTime)
        {
            recoil = Mathf.MoveTowards(recoil, 0f, RecoverSpeed * deltaTime);

            // This means that when a barrel is fully reset it'll never be EXACTLY
            // back at where it started, but this distance should be small enough
            // that hopefully it won't be noticeable.
            if (recoil > 0f)
                barrel.transform.localPosition = startLocalPosition + (Vector3.back * recoil);
        }
    }
}