using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using static GlobalHelper;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

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
    public virtual void RestoreHitPoints()
    {
        throw new NotImplementedException();
    }
    public virtual void RestoreArmor()
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
    public Vector3 Scale = Vector3.one;
    [HideInInspector] public BoidsManager BoidManager;

    // Add faction to base class for efficient lookup
    public virtual Faction FactionType => Faction.None;

    // Cache transform for performance
    private Transform _cachedTransform;
    public Transform CachedTransform
    {
        get
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;
            return _cachedTransform;
        }
    }

    #region Damage Handling
    public int ProcessKineticDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage / 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage * 2;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints > 0 && ShieldPoints <= 0)
            return (int)(damage * 0.5f) + shieldFloatDamage;
        else
            return 0;
    }

    public int ProcessEnergyDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage * 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage / 2;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + armorFloatDamage;
        else
            return 0;
    }

    public int ProcessExplosiveDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return damage * 2;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + armorFloatDamage;
        else if (ShieldPoints <= 0 && ArmorPoints > 0)
            return (int)(damage * 0.75f) + shieldFloatDamage;
        else
            return (int)(damage * 0.25f);
    }

    public int ProcessPlasmaDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage / 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage * 3;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.25f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + shieldFloatDamage;
        else
            return 0;
    }
    #endregion
}