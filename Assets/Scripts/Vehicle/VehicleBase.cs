using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using static GlobalHelper;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

public abstract class VehicleBase : MonoBehaviour
{

    [Header("Ownership")]
    public GameObject OwnerShip;
    public virtual VehicleType VehicleType => VehicleType.Frigate;
    public int HitPoints = 100;
    public int MaxHitPoints = 100;
    public int ArmorPoints = 10;
    public int MaxArmorPoints = 10;
    public int ShieldPoints = 10;
    public int MaxShieldPoints = 10;
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

    private int _lastDamageFrame = -1;
    private HashSet<int> _damageSourcesThisFrame = new HashSet<int>();


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

    #region Damage Handling

    /// <summary>
    /// Kinetic: Strong against shields (2x), weak against armor (0.5x)
    /// </summary>
    public int ProcessKineticDamage(int damage)
    {
        int remainingDamage = damage;

        // First, shields absorb damage (kinetic is strong against shields)
        if (ShieldPoints > 0)
        {
            int shieldDamage = damage * 2; // 2x effective against shields
            if (ShieldPoints >= shieldDamage)
            {
                ShieldPoints -= shieldDamage;
                return 0; // Shield fully absorbed
            }
            else
            {
                // Shield broke, calculate overflow
                int overflow = shieldDamage - ShieldPoints;
                ShieldPoints = 0;
                remainingDamage = overflow / 2; // Convert back from 2x multiplier
            }
        }

        // Then, armor absorbs remaining damage (kinetic is weak against armor)
        if (ArmorPoints > 0)
        {
            int armorDamage = remainingDamage / 2; // 0.5x effective against armor
            if (armorDamage <= 0) armorDamage = 1; // Minimum 1 damage to armor

            if (ArmorPoints >= armorDamage)
            {
                ArmorPoints -= armorDamage;
                return 0; // Armor fully absorbed
            }
            else
            {
                // Armor broke, calculate overflow
                int overflow = armorDamage - ArmorPoints;
                ArmorPoints = 0;
                remainingDamage = overflow * 2; // Convert back from 0.5x multiplier
            }
        }

        // Both down: bonus damage to HP
        return (int)(remainingDamage * 1.5f);
    }

    /// <summary>
    /// Energy: Strong against armor (2x), weak against shields (0.5x)
    /// </summary>
    public int ProcessEnergyDamage(int damage)
    {
        int remainingDamage = damage;

        // First, armor absorbs damage (energy is strong against armor)
        if (ArmorPoints > 0)
        {
            int armorDamage = damage * 2; // 2x effective against armor
            if (ArmorPoints >= armorDamage)
            {
                ArmorPoints -= armorDamage;
                return 0; // Armor fully absorbed
            }
            else
            {
                int overflow = armorDamage - ArmorPoints;
                ArmorPoints = 0;
                remainingDamage = overflow / 2;
            }
        }

        // Then, shields absorb remaining (energy is weak against shields)
        if (ShieldPoints > 0)
        {
            int shieldDamage = remainingDamage / 2; // 0.5x effective against shields
            if (shieldDamage <= 0) shieldDamage = 1;

            if (ShieldPoints >= shieldDamage)
            {
                ShieldPoints -= shieldDamage;
                return 0; // Shield fully absorbed
            }
            else
            {
                int overflow = shieldDamage - ShieldPoints;
                ShieldPoints = 0;
                remainingDamage = overflow * 2;
            }
        }

        // Both down: bonus damage to HP
        return (int)(remainingDamage * 1.5f);
    }

    /// <summary>
    /// Explosive: Balanced damage to both (1x each), damages both simultaneously
    /// </summary>
    public int ProcessExplosiveDamage(int damage)
    {
        int hpDamage = 0;

        // Explosive damages both armor and shield simultaneously
        int armorOverflow = 0;
        if (ArmorPoints > 0)
        {
            if (ArmorPoints >= damage)
            {
                ArmorPoints -= damage;
            }
            else
            {
                armorOverflow = damage - ArmorPoints;
                ArmorPoints = 0;
            }
        }
        else
        {
            armorOverflow = damage;
        }

        int shieldOverflow = 0;
        if (ShieldPoints > 0)
        {
            if (ShieldPoints >= damage)
            {
                ShieldPoints -= damage;
            }
            else
            {
                shieldOverflow = damage - ShieldPoints;
                ShieldPoints = 0;
            }
        }
        else
        {
            shieldOverflow = damage;
        }

        // HP damage based on protection status
        if (ArmorPoints <= 0 && ShieldPoints <= 0)
        {
            // Both down: full overflow damage with bonus
            hpDamage = (int)((armorOverflow + shieldOverflow) * 0.5f * 1.5f);
        }
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
        {
            // Only armor down
            hpDamage = (int)(armorOverflow * 0.5f);
        }
        else if (ArmorPoints > 0 && ShieldPoints <= 0)
        {
            // Only shield down
            hpDamage = (int)(shieldOverflow * 0.5f);
        }
        // else: both still up, no HP damage

        return hpDamage;
    }

    /// <summary>
    /// Plasma: Very strong against shields (4x), weak against armor (0.25x)
    /// </summary>
    public int ProcessPlasmaDamage(int damage)
    {
        int remainingDamage = damage;

        // First, shields absorb damage (plasma is very weak against shields)
        if (ShieldPoints > 0)
        {
            int shieldDamage = damage / 4; // 0.25x effective against shields
            if (shieldDamage <= 0) shieldDamage = 1; // Minimum 1 damage to shields

            if (ShieldPoints >= shieldDamage)
            {
                ShieldPoints -= shieldDamage;
                return 0;
            }
            else
            {
                int overflow = shieldDamage - ShieldPoints;
                ShieldPoints = 0;
                remainingDamage = overflow * 4;
            }
        }

        // Then, armor absorbs remaining (plasma is strong against armour)
        if (ArmorPoints > 0)
        {
            int armorDamage = remainingDamage * 4; // 4x effective against armor
            if (ArmorPoints >= armorDamage)
            {
                ArmorPoints -= armorDamage;
                return 0;
            }
            else
            {
                int overflow = armorDamage - ArmorPoints;
                ArmorPoints = 0;
                remainingDamage = overflow / 4;
            }
        }

        // Both down: bonus damage
        return (int)(remainingDamage);
    }

    /// <summary>
    /// EMP: Destroys shields completely, no HP damage
    /// </summary>
    public int ProcessEMPDamage(int damage)
    {
        ShieldPoints = 0;
        return 0;
    }

    /// <summary>
    /// Pierce: Ignores armor and shields, direct HP damage
    /// </summary>
    public int ProcessPierceDamage(int damage)
    {
        return damage;
    }



    public bool TakeDamageFromSource(int damage, AmmoType ammoType, int sourceId)
    {
        if (Time.frameCount != _lastDamageFrame)
        {
            _lastDamageFrame = Time.frameCount;
            _damageSourcesThisFrame.Clear();
        }

        if (_damageSourcesThisFrame.Contains(sourceId))
        {
            return HitPoints > 0;
        }

        _damageSourcesThisFrame.Add(sourceId);
        return TakeDamage(damage, ammoType);
    }

    #endregion
}