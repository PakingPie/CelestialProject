using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using static GlobalHelper;

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
    public virtual bool TakeDamage(int damage, AmmoType ammoType)
    {
        throw new NotImplementedException();
    }
    public virtual void Repair(int amount)
    {
        throw new NotImplementedException();
    }
    public virtual void RestoreShield()
    {
        throw new NotImplementedException();
    }
    public virtual void DestroyVehicle()
    {
        throw new NotImplementedException();
    }

    public int HitPoints = 100;
    public int MaxHitPoints = 100;
    public int ArmorPoints = 10;
    public int MaxArmorPoints = 10;
    public int ShieldPoints = 10;
    public int MaxShieldPoints = 10;
    public int Speed = 10;
    
}