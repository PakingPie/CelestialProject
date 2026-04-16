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

    // Cached bounding sphere radius (half-diagonal of combined renderer bounds)
    private float _boundsRadius = -1f;
    private Bounds _worldBounds;
    private WeaponPlatform[] _childWeaponPlatforms;
    private VehicleModule[] _childVehicleModules;

    public float BoundsRadius
    {
        get
        {
            if (_boundsRadius < 0f)
                RecalculateBounds();
            return _boundsRadius;
        }
    }

    public Bounds WorldBounds
    {
        get
        {
            if (_boundsRadius < 0f)
                RecalculateBounds();
            // Update center to current position (size stays the same)
            _worldBounds.center = CachedTransform.position + _localBoundsOffset;
            return _worldBounds;
        }
    }

    private Vector3 _localBoundsOffset;

    public void RecalculateBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            _localBoundsOffset = combined.center - CachedTransform.position;
            _worldBounds = combined;
            _boundsRadius = combined.extents.magnitude;
        }
        else
        {
            _localBoundsOffset = Vector3.zero;
            _worldBounds = new Bounds(CachedTransform.position, Vector3.one);
            _boundsRadius = CachedTransform.localScale.magnitude * 0.5f;
        }

        // Cache child weapon platforms and modules for damage routing
        _childWeaponPlatforms = GetComponentsInChildren<WeaponPlatform>();
        _childVehicleModules = GetComponentsInChildren<VehicleModule>();
    }

    /// <summary>
    /// Takes damage and routes to the nearest child WeaponPlatform/VehicleModule at the impact point.
    /// </summary>
    public virtual bool TakeDamageAtPoint(int damage, AmmoType ammoType, Vector3 impactPoint)
    {
        // Track whether a VehicleModule handled damage (it forwards to parent internally)
        bool moduleHandledDamage = false;

        // Route to closest child VehicleModule if near impact
        // Check this first because VehicleModule.TakeDamage forwards to the root vehicle
        if (_childVehicleModules != null)
        {
            float bestDistSqr = float.MaxValue;
            VehicleModule closest = null;
            for (int i = 0; i < _childVehicleModules.Length; i++)
            {
                var vm = _childVehicleModules[i];
                if (vm == null) continue;
                float distSqr = (vm.CachedTransform.position - impactPoint).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    closest = vm;
                }
            }
            if (closest != null)
            {
                float moduleRadius = closest.BoundsRadius;
                if (bestDistSqr <= moduleRadius * moduleRadius * 4f)
                {
                    // VehicleModule.TakeDamage already forwards damage to the root vehicle
                    closest.TakeDamage(damage, ammoType);
                    moduleHandledDamage = true;
                }
            }
        }

        // Only damage root directly if no VehicleModule already forwarded the damage
        if (!moduleHandledDamage)
            TakeDamage(damage, ammoType);

        // Route to closest child WeaponPlatform if near impact
        if (_childWeaponPlatforms != null)
        {
            float bestDistSqr = float.MaxValue;
            WeaponPlatform closest = null;
            for (int i = 0; i < _childWeaponPlatforms.Length; i++)
            {
                var wp = _childWeaponPlatforms[i];
                if (wp == null || wp.HitPoints <= 0) continue;
                float distSqr = (wp.CachedTransform.position - impactPoint).sqrMagnitude;
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    closest = wp;
                }
            }
            if (closest != null)
            {
                // Only damage turret if impact is reasonably close to it
                float turretRadius = closest.BoundsRadius;
                if (bestDistSqr <= turretRadius * turretRadius * 4f) // within 2x turret radius
                    closest.TakeSelfDamage(damage, ammoType);
            }
        }

        return HitPoints > 0;
    }

    /// <summary>
    /// Returns the closest point on this vehicle's bounding box to the given position.
    /// </summary>
    public Vector3 ClosestBoundsPoint(Vector3 position)
    {
        return WorldBounds.ClosestPoint(position);
    }

    /// <summary>
    /// Returns the squared distance from a position to the surface of this vehicle's bounds.
    /// Returns 0 if the position is inside the bounds.
    /// </summary>
    public float SqrDistanceToBounds(Vector3 position)
    {
        Vector3 closest = WorldBounds.ClosestPoint(position);
        return (closest - position).sqrMagnitude;
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

        // Debug.Log($"Explosive damage processed: HP Damage = {hpDamage}, Remaining Damage = {remainingDamage}");

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