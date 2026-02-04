using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;

public class MissileVehicle : VehicleBase
{
    public Faction VehicleFaction = Faction.Foe;
    public VehicleType Type = VehicleType.Missile;

    private EnemyPredictionManager _predictionManager;
    private Vector3 _lastPosition;
    private Vector3 _velocity;

    public float Velocity => _velocity.magnitude;
    public override Faction FactionType =>VehicleFaction;
    public override VehicleType VehicleType => Type;
    public bool IsDying { get; private set; } = false;
    public bool EnableIndication = false;
    // No need registration for missiles because it is done on AAMissile script


    void OnEnable()
    {
        CombatRegistry.Register(this, FactionType);
    }

    void OnDisable()
    {
        CombatRegistry.Unregister(this, FactionType);
    }

    void OnDestroy()
    {
        if (_predictionManager != null && EnableIndication)
        {
            _predictionManager.UnregisterMissile(this);
        }
    }

    void Start()
    {
        _lastPosition = transform.position;

        // Register with manager
        _predictionManager = FindAnyObjectByType<EnemyPredictionManager>();
        if (_predictionManager != null && EnableIndication)
        {
            _predictionManager.RegisterMissile(this);
        }
    }

    void Update()
    {
        _velocity = (transform.position - _lastPosition) / Time.deltaTime;

        _lastPosition = transform.position;
    }

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

        var missile = GetComponent<AAMissile>();
        if (missile != null)
        {
            missile.DestroyMissile(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}