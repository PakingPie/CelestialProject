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
    public bool IsShowHitpointBar = false;
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

    #region Improved Damage Handling

    /// <summary>
    /// Core damage processing - damage flows through all layers
    /// </summary>
    /// <param name="damage">Base damage amount</param>
    /// <param name="shieldEffectiveness">Multiplier for shield damage (>1 = strong vs shield, <1 = weak)</param>
    /// <param name="armorEffectiveness">Multiplier for armor damage (>1 = strong vs armor, <1 = weak)</param>
    /// <param name="shieldAbsorption">How much of the damage shields try to absorb (0-1)</param>
    /// <returns>Final HP damage</returns>
    private int ProcessDamageFlow(int damage, float shieldEffectiveness, float armorEffectiveness, float shieldAbsorption = 0.6f)
    {
        if (damage <= 0) return 0;

        float remainingDamage = damage;

        // === SHIELD PHASE ===
        // Shield tries to absorb a portion of damage
        if (ShieldPoints > 0)
        {
            // How much damage the shield attempts to block
            float damageToShield = damage * shieldAbsorption;

            // Apply effectiveness (high effectiveness = shield takes more damage to block same amount)
            float shieldDamage = damageToShield * shieldEffectiveness;

            if (ShieldPoints >= shieldDamage)
            {
                // Shield absorbs its portion
                ShieldPoints -= Mathf.RoundToInt(shieldDamage);
                remainingDamage = damage * (1f - shieldAbsorption);
            }
            else
            {
                // Shield breaks - calculate how much it actually blocked
                float actualBlocked = ShieldPoints / shieldEffectiveness;
                float blockRatio = actualBlocked / damageToShield;
                ShieldPoints = 0;

                // Remaining damage = unblocked portion + what shield couldn't block
                remainingDamage = damage * (1f - shieldAbsorption * blockRatio);
            }
        }

        if (remainingDamage <= 0) return 0;

        // === ARMOR PHASE ===
        // Armor tries to absorb remaining damage
        if (ArmorPoints > 0)
        {
            float armorDamage = remainingDamage * armorEffectiveness;

            if (ArmorPoints >= armorDamage)
            {
                // Armor fully absorbs remaining damage
                ArmorPoints -= Mathf.RoundToInt(armorDamage);
                return 0;
            }
            else
            {
                // Armor breaks
                float actualAbsorbed = ArmorPoints / armorEffectiveness;
                ArmorPoints = 0;
                remainingDamage -= actualAbsorbed;
            }
        }

        // === HP PHASE ===
        // Apply vulnerability bonus when protections are down
        float hpMultiplier = 1f;
        if (ShieldPoints <= 0 && ArmorPoints <= 0)
        {
            hpMultiplier = 1.5f; // Vulnerable when both are down
        }

        return Mathf.RoundToInt(remainingDamage * hpMultiplier);
    }

    /// <summary>
    /// Kinetic: Strong vs shields (1.5x), weak vs armor (0.5x)
    /// High velocity rounds punch through energy barriers but deflect off plating
    /// </summary>
    public int ProcessKineticDamage(int damage)
    {
        return ProcessDamageFlow(
            damage,
            shieldEffectiveness: 1.5f,   // Shields take 1.5x damage to block kinetic
            armorEffectiveness: 0.5f,    // Armor only takes 0.5x damage to block kinetic
            shieldAbsorption: 0.5f       // Shields try to block 50% of kinetic
        );
    }

    /// <summary>
    /// Energy: Strong vs armor (1.5x), weak vs shields (0.5x)
    /// Concentrated energy melts plating but disperses against shields
    /// </summary>
    public int ProcessEnergyDamage(int damage)
    {
        return ProcessDamageFlow(
            damage,
            shieldEffectiveness: 0.5f,   // Shields easily block energy
            armorEffectiveness: 1.5f,    // Armor struggles against energy
            shieldAbsorption: 0.7f       // Shields try to block 70% of energy
        );
    }

    /// <summary>
    /// Explosive: Balanced damage, hits everything
    /// Shockwave affects all layers simultaneously
    /// </summary>
    public int ProcessExplosiveDamage(int damage)
    {
        int hpDamage = 0;
        float remainingDamage = damage;

        // Explosive damages shield and armor simultaneously (split damage)
        float shieldPortion = 0.5f;
        float armorPortion = 0.5f;

        // Shield takes its portion
        if (ShieldPoints > 0)
        {
            int shieldDamage = Mathf.RoundToInt(damage * shieldPortion);
            if (ShieldPoints >= shieldDamage)
            {
                ShieldPoints -= shieldDamage;
                remainingDamage -= damage * shieldPortion;
            }
            else
            {
                remainingDamage -= ShieldPoints;
                ShieldPoints = 0;
            }
        }

        // Armor takes its portion
        if (ArmorPoints > 0)
        {
            int armorDamage = Mathf.RoundToInt(damage * armorPortion);
            if (ArmorPoints >= armorDamage)
            {
                ArmorPoints -= armorDamage;
                remainingDamage -= damage * armorPortion;
            }
            else
            {
                remainingDamage -= ArmorPoints;
                ArmorPoints = 0;
            }
        }

        // HP damage from overflow
        if (remainingDamage > 0)
        {
            float multiplier = (ShieldPoints <= 0 && ArmorPoints <= 0) ? 1.25f : 1f;
            hpDamage = Mathf.RoundToInt(remainingDamage * multiplier);
        }

        return hpDamage;
    }

    /// <summary>
    /// Plasma: Very strong vs armor (2.5x), very weak vs shields (0.25x)
    /// Superheated matter vaporizes hull but shields diffuse it
    /// </summary>
    public int ProcessPlasmaDamage(int damage)
    {
        return ProcessDamageFlow(
            damage,
            shieldEffectiveness: 0.25f,  // Shields easily absorb plasma
            armorEffectiveness: 2.5f,    // Plasma melts through armor
            shieldAbsorption: 0.8f       // Shields try to block 80% of plasma
        );
    }

    /// <summary>
    /// EMP: Destroys shields, no direct damage
    /// Electromagnetic pulse overloads energy systems
    /// </summary>
    public int ProcessEMPDamage(int damage)
    {
        ShieldPoints = 0;
        return 0;
    }

    /// <summary>
    /// Pierce: Bypasses all defenses, direct HP damage
    /// Specialized rounds designed to penetrate
    /// </summary>
    public int ProcessPierceDamage(int damage)
    {
        // Small bleed-through to armor (represents the round passing through)
        if (ArmorPoints > 0)
        {
            int armorBleed = Mathf.RoundToInt(damage * 0.1f);
            ArmorPoints = Mathf.Max(0, ArmorPoints - armorBleed);
        }
        return damage;
    }

    #endregion



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
}