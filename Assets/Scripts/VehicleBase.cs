using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public abstract class VehicleBase : MonoBehaviour
{
    public virtual void Move()
    {
        throw new NotImplementedException();
    }
    public virtual void Attack()
    {
        throw new NotImplementedException();
    }

    public int HitPoints = 100;
    public int Armor = 0;
    public int Shield = 0;
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
}