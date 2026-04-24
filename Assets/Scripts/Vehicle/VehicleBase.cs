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

    // Cached bounding radius (half-diagonal of OBB extents, used for broad-phase)
    private float _boundsRadius = -1f;
    private WeaponPlatform[] _childWeaponPlatforms;
    private VehicleModule[] _childVehicleModules;
    private ShieldHitEffect _shieldHitEffect;

    public float BoundsRadius
    {
        get
        {
            if (_boundsRadius < 0f)
                RecalculateBounds();
            return _boundsRadius;
        }
    }

    private Vector3 _localBoundsOffset;
    private Vector3 _localBoundsExtents;

    public void RecalculateBounds()
    {
        // Compute bounds in local space for a tight oriented bounding box (OBB)
        Bounds localCombined = default;
        bool hasAny = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                // Transform each renderer's 8 local-bounds corners into vehicle local space
                Bounds lb = r.localBounds;
                Vector3 bMin = lb.min;
                Vector3 bMax = lb.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? bMin.x : bMax.x,
                        (i & 2) == 0 ? bMin.y : bMax.y,
                        (i & 4) == 0 ? bMin.z : bMax.z
                    );
                    Vector3 worldPt = r.transform.TransformPoint(corner);
                    Vector3 localPt = CachedTransform.InverseTransformPoint(worldPt);

                    if (!hasAny)
                    {
                        localCombined = new Bounds(localPt, Vector3.zero);
                        hasAny = true;
                    }
                    else
                    {
                        localCombined.Encapsulate(localPt);
                    }
                }
            }
        }

        if (hasAny)
        {
            _localBoundsOffset = localCombined.center;
            _localBoundsExtents = localCombined.extents;
            _boundsRadius = localCombined.extents.magnitude; // keep for broad-phase
        }
        else
        {
            _localBoundsOffset = Vector3.zero;
            _localBoundsExtents = CachedTransform.localScale * 0.5f;
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
        return TakeDamageAtPoint(DamageContext.Legacy(damage, ammoType, VehicleType, impactPoint));
    }

    public virtual bool TakeDamageAtPoint(DamageContext damageContext)
    {
        Vector3 impactPoint = damageContext.HasImpactPoint ? damageContext.ImpactPoint : CachedTransform.position;

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
                    closest.TakeDamage(damageContext);
                    moduleHandledDamage = true;
                }
            }
        }

        // Only damage root directly if no VehicleModule already forwarded the damage
        if (!moduleHandledDamage)
            TakeDamage(damageContext);

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
                    closest.TakeSelfDamage(damageContext);
            }
        }

        return HitPoints > 0;
    }

    public ShieldHitEffect GetShieldHitEffect()
    {
        if (OwnerShip != null && OwnerShip != gameObject)
        {
            VehicleBase ownerVehicle = OwnerShip.GetComponent<VehicleBase>();
            if (ownerVehicle != null && ownerVehicle != this)
                return ownerVehicle.GetShieldHitEffect();
        }

        if (_shieldHitEffect == null)
            _shieldHitEffect = GetComponentInChildren<ShieldHitEffect>(true);

        return _shieldHitEffect;
    }

    /// <summary>
    /// Returns the closest point on this vehicle's oriented bounding box to the given world position.
    /// </summary>
    public Vector3 ClosestBoundsPoint(Vector3 position)
    {
        if (_boundsRadius < 0f)
            RecalculateBounds();

        // Transform query point into vehicle local space, clamp to local AABB, transform back
        Vector3 localPos = CachedTransform.InverseTransformPoint(position);
        Vector3 clamped = new Vector3(
            Mathf.Clamp(localPos.x, _localBoundsOffset.x - _localBoundsExtents.x, _localBoundsOffset.x + _localBoundsExtents.x),
            Mathf.Clamp(localPos.y, _localBoundsOffset.y - _localBoundsExtents.y, _localBoundsOffset.y + _localBoundsExtents.y),
            Mathf.Clamp(localPos.z, _localBoundsOffset.z - _localBoundsExtents.z, _localBoundsOffset.z + _localBoundsExtents.z)
        );
        return CachedTransform.TransformPoint(clamped);
    }

    /// <summary>
    /// Returns the squared distance from a world position to the surface of this vehicle's oriented bounding box.
    /// Returns 0 if the position is inside the box.
    /// </summary>
    public float SqrDistanceToBounds(Vector3 position)
    {
        Vector3 closest = ClosestBoundsPoint(position);
        return (position - closest).sqrMagnitude;
    }

    /// <summary>
    /// Raycasts against this vehicle's oriented bounding box.
    /// Returns true if the ray hits, with the world-space hit point and distance.
    /// </summary>
    public bool RaycastBounds(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 hitPoint, out float hitDistance)
    {
        if (_boundsRadius < 0f)
            RecalculateBounds();

        // Transform ray into vehicle local space
        Vector3 localOrigin = CachedTransform.InverseTransformPoint(rayOrigin);
        Vector3 localDir = CachedTransform.InverseTransformVector(rayDirection);

        Vector3 bMin = _localBoundsOffset - _localBoundsExtents;
        Vector3 bMax = _localBoundsOffset + _localBoundsExtents;

        // Slab method for ray-AABB intersection
        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;

        for (int i = 0; i < 3; i++)
        {
            float ori = localOrigin[i];
            float dir = localDir[i];
            float mn = bMin[i];
            float mx = bMax[i];

            if (Mathf.Abs(dir) < 1e-8f)
            {
                // Ray parallel to slab — miss if origin outside
                if (ori < mn || ori > mx)
                {
                    hitPoint = default;
                    hitDistance = 0f;
                    return false;
                }
            }
            else
            {
                float t1 = (mn - ori) / dir;
                float t2 = (mx - ori) / dir;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                if (t1 > tMin) tMin = t1;
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax)
                {
                    hitPoint = default;
                    hitDistance = 0f;
                    return false;
                }
            }
        }

        if (tMin < 0f)
            tMin = tMax; // ray starts inside, use exit point
        if (tMin < 0f)
        {
            hitPoint = default;
            hitDistance = 0f;
            return false; // box is behind the ray
        }

        Vector3 localHit = localOrigin + localDir * tMin;
        hitPoint = CachedTransform.TransformPoint(localHit);
        hitDistance = Vector3.Distance(rayOrigin, hitPoint);
        return true;
    }

    private int _lastDamageFrame = -1;
    private HashSet<int> _damageSourcesThisFrame = new HashSet<int>();

    private bool CanProcessDamageSource(int sourceId)
    {
        if (sourceId == 0)
            return true;

        if (Time.frameCount != _lastDamageFrame)
        {
            _lastDamageFrame = Time.frameCount;
            _damageSourcesThisFrame.Clear();
        }

        if (_damageSourcesThisFrame.Contains(sourceId))
            return false;

        _damageSourcesThisFrame.Add(sourceId);
        return true;
    }


    public virtual bool TakeDamage(DamageContext damageContext)
    {
        if (!CanProcessDamageSource(damageContext.SourceId))
            return HitPoints > 0;

        return TakeDamage(damageContext.ResolvedDamage, damageContext.AmmoType);
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
        return TakeDamage(DamageContext.Legacy(damage, ammoType, VehicleType, sourceId));
    }
}