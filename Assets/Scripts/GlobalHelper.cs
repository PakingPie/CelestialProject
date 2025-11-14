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

    public enum Faction
    {
        Player,
        Ally,
        Foe,
        Neutral
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
}