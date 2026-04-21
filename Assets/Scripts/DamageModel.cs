using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TargetDamageModifier
{
    public GlobalHelper.VehicleType TargetType = GlobalHelper.VehicleType.Missile;

    [Min(0f)]
    public float Multiplier = 1f;
}

[Serializable]
public class DamageProfile
{
    [Min(0f)]
    public float DefaultMultiplier = 1f;

    public List<TargetDamageModifier> TargetTypeModifiers = new List<TargetDamageModifier>();

    public float GetTargetTypeMultiplier(GlobalHelper.VehicleType targetType)
    {
        for (int i = 0; i < TargetTypeModifiers.Count; i++)
        {
            if (TargetTypeModifiers[i].TargetType == targetType)
                return Mathf.Max(0f, TargetTypeModifiers[i].Multiplier);
        }

        return 1f;
    }

    public int ResolveDamage(int baseDamage, VehicleBase target)
    {
        if (baseDamage <= 0)
            return 0;

        float resolvedDamage = baseDamage * Mathf.Max(0f, DefaultMultiplier);
        if (target != null)
            resolvedDamage *= GetTargetTypeMultiplier(target.VehicleType);

        return Mathf.Max(0, Mathf.RoundToInt(resolvedDamage));
    }

    public DamageContext CreateContext(Component source, int baseDamage, GlobalHelper.AmmoType ammoType, VehicleBase target, Vector3 impactPoint)
    {
        int sourceId = source != null ? source.GetInstanceID() : 0;
        GlobalHelper.VehicleType targetType = target != null ? target.VehicleType : GlobalHelper.VehicleType.Frigate;
        int resolvedDamage = ResolveDamage(baseDamage, target);
        return new DamageContext(baseDamage, resolvedDamage, ammoType, targetType, sourceId, impactPoint, true);
    }
}

public struct DamageContext
{
    public int BaseDamage { get; }
    public int ResolvedDamage { get; }
    public GlobalHelper.AmmoType AmmoType { get; }
    public GlobalHelper.VehicleType TargetVehicleType { get; }
    public int SourceId { get; }
    public Vector3 ImpactPoint { get; }
    public bool HasImpactPoint { get; }

    public DamageContext(int baseDamage, int resolvedDamage, GlobalHelper.AmmoType ammoType, GlobalHelper.VehicleType targetVehicleType, int sourceId = 0, Vector3 impactPoint = default, bool hasImpactPoint = false)
    {
        BaseDamage = baseDamage;
        ResolvedDamage = resolvedDamage;
        AmmoType = ammoType;
        TargetVehicleType = targetVehicleType;
        SourceId = sourceId;
        ImpactPoint = impactPoint;
        HasImpactPoint = hasImpactPoint;
    }

    public DamageContext WithResolvedDamage(int resolvedDamage)
    {
        return new DamageContext(BaseDamage, resolvedDamage, AmmoType, TargetVehicleType, SourceId, ImpactPoint, HasImpactPoint);
    }

    public static DamageContext Legacy(int damage, GlobalHelper.AmmoType ammoType, GlobalHelper.VehicleType targetVehicleType, Vector3 impactPoint)
    {
        return new DamageContext(damage, damage, ammoType, targetVehicleType, 0, impactPoint, true);
    }

    public static DamageContext Legacy(int damage, GlobalHelper.AmmoType ammoType, GlobalHelper.VehicleType targetVehicleType, int sourceId = 0)
    {
        return new DamageContext(damage, damage, ammoType, targetVehicleType, sourceId);
    }
}