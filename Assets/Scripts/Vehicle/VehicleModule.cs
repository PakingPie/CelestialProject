using UnityEngine;
using static GlobalHelper;

public class VehicleModule : VehicleBase
{
    [Header("Ownership")]
    public GameObject OwnShip;
    public override Faction FactionType => OwnShip.GetComponent<VehicleBase>().FactionType;

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        OwnShip.GetComponent<VehicleBase>().TakeDamage(damage, ammoType);
        return true;
    }
}