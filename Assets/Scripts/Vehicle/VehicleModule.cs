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
        if (OwnerShip == null) return false;

        var ownerVehicle = OwnerShip.GetComponent<VehicleBase>();
        if (ownerVehicle == null) return false;

        int finalDamage = (int)(damage * DamageMultiplier);

        // Try to use source-tracked damage to prevent duplicates
        if (ownerVehicle is EnemyVehicle enemyVehicle)
        {
            enemyVehicle.TakeDamageFromSource(finalDamage, ammoType, GetInstanceID());
        }
        else if (ownerVehicle is PlayerVehicle playerVehicle)
        {
            playerVehicle.TakeDamageFromSource(finalDamage, ammoType, GetInstanceID());
        }
        else
        {
            // Fallback for other vehicle types
            ownerVehicle.TakeDamage(finalDamage, ammoType);
        }

        return true;
    }
}