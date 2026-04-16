using UnityEngine;
using static GlobalHelper;

public class VehicleModule : VehicleBase
{
    [Header("Faction")]
    public Faction VehicleFaction = Faction.Player;
    public override Faction FactionType => VehicleFaction;
    [Header("Damage Multiplier")]
    public float DamageMultiplier = 1f;

    private bool _registeredWithCombat = false;

    void OnEnable()
    {
        if (!Application.isPlaying) return;

        // Only register if this is standalone (no parent vehicle to route damage through)
        if (OwnerShip == null || OwnerShip == gameObject || OwnerShip.GetComponent<VehicleBase>() == null)
        {
            CombatRegistry.Register(this, FactionType);
            _registeredWithCombat = true;
        }
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;

        if (_registeredWithCombat)
        {
            CombatRegistry.Unregister(this, FactionType);
            _registeredWithCombat = false;
        }
    }

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        if (OwnerShip == null) return false;

        var ownerVehicle = OwnerShip.GetComponent<VehicleBase>();
        if (ownerVehicle == null) return false;

        int finalDamage = (int)(damage * DamageMultiplier);

        ownerVehicle.TakeDamage(finalDamage, ammoType);

        // // Try to use source-tracked damage to prevent duplicates, not working, will do in BulletPhysics.cs and AAMissiles.cs
        // if (ownerVehicle is EnemyVehicle enemyVehicle)
        // {
        //     enemyVehicle.TakeDamageFromSource(finalDamage, ammoType, GetInstanceID());
        // }
        // else if (ownerVehicle is PlayerVehicle playerVehicle)
        // {
        //     playerVehicle.TakeDamageFromSource(finalDamage, ammoType, GetInstanceID());
        // }
        // else
        // {
        //     // Fallback for other vehicle types
        //     ownerVehicle.TakeDamage(finalDamage, ammoType);
        // }
        // Debug.Log($"Module on {OwnerShip.name} took {finalDamage} damage (original: {damage}, multiplier: {DamageMultiplier})");

        return true;
    }
}