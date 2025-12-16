using UnityEngine;
using static GlobalHelper;

public class VehicleModule : VehicleBase
{
    [Header("Faction")]
    public Faction VehicleFaction = Faction.Player;
    public override Faction FactionType => VehicleFaction;

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