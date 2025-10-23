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
        Ally,
        Foe,
        Neutral
    }

    public enum AmmoType
    {
        Kinetic,
        Energy,
        Explosive
    }
}