using UnityEngine;
using System.Collections;
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
        Player = 1 << 0, // 0001
        Ally = 1 << 1,   // 0010
        Foe = 1 << 2,    // 0100
        Neutral = 1 << 3 // 1000
    }

    public enum AmmoType
    {
        Kinetic,   // 0000
        Energy,    // 0001
        Explosive, // 0010
        EMP,       // 0011
        Plasma,    // 0100
        Pierce     // 0101
    }

    public enum GuidanceType
    {
        Pursuit,
        Lead
    };

    public static string[] FactionNames = { "Player", "Ally", "Foe", "Neutral" };

    public static List<GameObject> FindEnemies(Faction targetFlags)
    {
        List<GameObject> enemies = new List<GameObject>();

        foreach (Faction flag in System.Enum.GetValues(typeof(Faction)))
        {
            if (flag == Faction.None) continue;

            if (targetFlags.HasFlag(flag))
            {
                int index = GetFlagIndex(flag);
                GameObject[] found = GameObject.FindGameObjectsWithTag(FactionNames[index]);
                enemies.AddRange(found);
            }
        }

        return enemies;
    }

    // Convert flag to index (0, 1, 2, 3...)
    private static int GetFlagIndex(Faction flag)
    {
        return (int)Mathf.Log((int)flag, 2);
    }
}