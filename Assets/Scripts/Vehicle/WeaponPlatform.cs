using System;
using UnityEngine;
using static GlobalHelper;

[RequireComponent(typeof(WeaponBase))]
public class WeaponPlatform : VehicleBase
{
    [Header("Regeneration")]
    public int HitPointsRegenerationRate = 1;
    public float HitPointsRegenerationDelay = 5f;
    public int ArmorRegenerationRate = 1;
    public float ArmorRegenerationDelay = 5f;


    [Header("Faction")]
    public Faction VehicleFaction = Faction.Player;
    public override Faction FactionType => VehicleFaction;

    private float _hitPointsRegenTimer = 0f;
    private float _armorRegenTimer = 0f;
    private float _lastDamageTime = 0f;

    private bool _registeredWithCombat = false;

    void OnEnable()
    {
        if (!Application.isPlaying) return;

        // Only register if this is a standalone weapon (no parent vehicle to route damage through)
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

    void Start()
    {
        GetComponent<WeaponBase>().IsFunctional = true;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Handle shield regeneration
        if (ArmorPoints < MaxArmorPoints || HitPoints < MaxHitPoints)
        {
            _lastDamageTime += Time.deltaTime;

            if (_lastDamageTime >= ArmorRegenerationDelay && ArmorPoints < MaxArmorPoints)
            {
                RestoreArmor();
            }
            if (_lastDamageTime >= HitPointsRegenerationDelay && HitPoints < MaxHitPoints)
            {
                RestoreHitPoints();
                GetComponent<WeaponBase>().Effectiveness = HitPoints / (float)MaxHitPoints;
            }
        }

        // If hitpoint is restored to max and it was non-functional, make it functional again
        if (HitPoints >= MaxHitPoints && !GetComponent<WeaponBase>().IsFunctional)
        {
            GetComponent<WeaponBase>().IsFunctional = true;
        }
    }

    public override void RestoreHitPoints() => RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hitPointsRegenTimer, 0.1f);
    public override void RestoreArmor() => RegenerateAttributtes(ref ArmorPoints, ref MaxArmorPoints, ref ArmorRegenerationRate, ref _armorRegenTimer, 0.1f);

    private void RegenerateAttributtes(ref int currentAmount, ref int maxAmount, ref int regenerationRate, ref float regenTimer, float delay)
    {
        regenTimer += Time.deltaTime;
        if (regenTimer >= delay && currentAmount < maxAmount)
        {
            currentAmount += regenerationRate;
            if (currentAmount > maxAmount)
                currentAmount = maxAmount;

            regenTimer = 0f;
        }
    }

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        return TakeDamage(DamageContext.Legacy(damage, ammoType, VehicleType));
    }

    public override bool TakeDamage(DamageContext damageContext)
    {
        if (OwnerShip != null && OwnerShip != gameObject)
        {
            var ownerVehicle = OwnerShip.GetComponent<VehicleBase>();
            if (ownerVehicle != null && ownerVehicle != this)
            {
                ownerVehicle.TakeDamage(damageContext);
            }
        }

        return TakeSelfDamage(damageContext);
    }

    public bool TakeSelfDamage(DamageContext damageContext)
    {
        return TakeSelfDamage(damageContext.ResolvedDamage, damageContext.AmmoType);
    }

    public bool TakeSelfDamage(int damage, AmmoType ammoType)
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
            case AmmoType.Ion:
                damage = ProcessIonDamage(damage);
                break;
            case AmmoType.Plasma:
                damage = ProcessPlasmaDamage(damage);
                break;
            case AmmoType.Pierce:
                // Full damage, ignores armor and shields
                break;
        }

        HitPoints = Math.Clamp(HitPoints - damage, 0, MaxHitPoints);
        // Debug.Log($"WeaponPlatform took {damage} damage, remaining HP: {HitPoints}");

        if (damage > HitPoints / 2)
        {
            GetComponent<WeaponBase>().IsFunctional = false;
        }

        // Impair Effectiveness of Weapon Platform base on HitPoints
        GetComponent<WeaponBase>().Effectiveness = HitPoints / (float)MaxHitPoints;

        if (HitPoints <= 0)
        {
            GetComponent<WeaponBase>().IsFunctional = false;
        }

        _lastDamageTime = 0f;

        return HitPoints > 0;
    }
}