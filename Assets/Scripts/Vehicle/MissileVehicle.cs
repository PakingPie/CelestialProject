using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;

public class MissileVehicle : VehicleBase
{
    public Faction VehicleFaction = Faction.Foe;
    [SerializeField] private VehicleType _vehicleType = VehicleType.Missile;
    
    public override VehicleType VehicleType => _vehicleType;
    public bool IsDying { get; private set; } = false;
    public bool EnableIndication = false;
    // No need registration for missiles because it is done on AAMissile script

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        // Simple damage calculation; can be expanded based on ammoType and armor/shield
        switch (ammoType)
        {
            case AmmoType.Kinetic:
                damage = ProcessKineticDamage(damage);
                break;
            case AmmoType.Energy:
                damage = ProcessEnergyDamage(damage);
                break;
            case AmmoType.Explosive:
                damage = ProcessExplosiveDamage(damage);
                break;
            case AmmoType.EMP:
                ShieldPoints = 0;
                damage = 0;
                break;
            case AmmoType.Plasma:
                damage = ProcessPlasmaDamage(damage);
                break;
            case AmmoType.Pierce:
                // Full damage, ignores armor and shields
                break;
        }

        HitPoints -= damage;

        if (HitPoints <= 0)
        {
            DestroyVehicle();
        }

        return HitPoints > 0;
    }

    public override void DestroyVehicle()
    {
        if (IsDying) return; // Prevent double-destroy
        IsDying = true;

        Destroy(gameObject, 0.1f);
    }
}