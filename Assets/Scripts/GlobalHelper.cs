using UnityEngine;
using System.Collections.Generic;

public static class GlobalHelper
{
    public enum VehicleType
    {
        Frigate,
        Destroyer,
        Cruiser,
        Battleship
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
}