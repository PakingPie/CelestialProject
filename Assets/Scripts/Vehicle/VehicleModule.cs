using UnityEngine;
using static GlobalHelper;

public class VehicleModule : VehicleBase
{
    public override Faction FactionType => OwnerShip.GetComponent<VehicleBase>().FactionType;

    void OnEnable()
    {
        // Register with CombatRegistry
        if (Application.isPlaying)
            CombatRegistry.Register(this, FactionType);
    }

    void OnDisable()
    {
        // Unregister from CombatRegistry
        if (Application.isPlaying)
            CombatRegistry.Unregister(this, FactionType);
    }

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        OwnerShip.GetComponent<VehicleBase>().TakeDamage(damage, ammoType);
        return true;
    }
}