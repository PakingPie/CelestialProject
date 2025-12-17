using UnityEngine;
using static GlobalHelper;

public class VehicleModule : VehicleBase
{
    [Header("Faction")]
    public Faction VehicleFaction = Faction.Player;
    public override Faction FactionType => VehicleFaction;
    [Header("Damage Multiplier")]
    public float DamageMultiplier = 1f;

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
        OwnerShip.GetComponent<VehicleBase>().TakeDamage((int)(damage * DamageMultiplier), ammoType);
        return true;
    }
}