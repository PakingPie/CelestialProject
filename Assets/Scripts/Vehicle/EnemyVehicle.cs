using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;

[ExecuteAlways]
public class EnemyVehicle : VehicleBase
{
    public Faction VehicleFaction = Faction.Foe;
    public VehicleType Type = VehicleType.Frigate;
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    public ParticleSystem ExplodeEffect;
    public int ShieldRegenerationRate = 1; // Points per second
    public float ShieldRegenerationDelay = 5f; // Seconds after taking damage before regeneration starts
    private float _shieldRegenTimer = 0f;
    private float _lastDamageTime = 0f;
    public override Faction FactionType => VehicleFaction;

    private Vector3 _lastPosition;
    private Vector3 _velocity;
    public Vector3 Velocity => _velocity;
    private EnemyPredictionManager _predictionManager;
    public bool EnableIndication = false;
    public bool IsDying { get; private set; } = false;

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
        if (_predictionManager != null)
            _predictionManager.UnregisterEnemy(this);
    }

    void Start()
    {
        _lastPosition = transform.position;

        // Register with manager
        _predictionManager = FindAnyObjectByType<EnemyPredictionManager>();
        if (_predictionManager != null && EnableIndication)
            _predictionManager.RegisterEnemy(this);

        if (HealthBar)
        {
            HealthBar.GetComponent<Image>().material = new Material(HealthBarShader);
            HealthBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxHitPoints);
            HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);
            HealthBar.GetComponent<Image>().material.SetVector("_Color1", Color.green);
            HealthBar.GetComponent<Image>().material.SetVector("_Color2", Color.yellow);
            HealthBar.GetComponent<Image>().material.SetVector("_Color3", Color.red);
        }

        if (ArmorBar)
        {
            ArmorBar.GetComponent<Image>().material = new Material(HealthBarShader);
            ArmorBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxArmorPoints);
            ArmorBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ArmorPoints);
            ArmorBar.GetComponent<Image>().material.SetVector("_Color1", Color.yellow);
            ArmorBar.GetComponent<Image>().material.SetVector("_Color2", Color.yellow);
            ArmorBar.GetComponent<Image>().material.SetVector("_Color3", Color.yellow);
        }

        if (ShieldBar)
        {
            ShieldBar.GetComponent<Image>().material = new Material(HealthBarShader);
            ShieldBar.GetComponent<Image>().material.SetInt("_MaxHitPoints", MaxShieldPoints);
            ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);
            ShieldBar.GetComponent<Image>().material.SetVector("_Color1", Color.cyan);
            ShieldBar.GetComponent<Image>().material.SetVector("_Color2", Color.cyan);
            ShieldBar.GetComponent<Image>().material.SetVector("_Color3", Color.cyan);
        }

        if (ShieldEffect)
        {
            ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial = new Material(EnergyShieldShader);
            ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", 1.0f);

            // GetComponent<ShieldHitEffect>().ShieldGO = ShieldEffect;
        }
    }

    void Update()
    {
        _velocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;
        // Handle shield regeneration
        if (ShieldEffect && ShieldPoints < MaxShieldPoints)
        {
            _lastDamageTime += Time.deltaTime;
            if (_lastDamageTime >= ShieldRegenerationDelay)
            {
                RestoreShield();
            }
        }
    }

    public override void RestoreShield()
    {
        _shieldRegenTimer += Time.deltaTime;
        if (_shieldRegenTimer >= 0.1f && ShieldPoints < MaxShieldPoints) // Regenerate shield every 0.1 second
        {
            ShieldPoints += ShieldRegenerationRate;
            if (ShieldPoints > MaxShieldPoints)
            {
                ShieldPoints = MaxShieldPoints;
            }
            ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);
            ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
            _shieldRegenTimer = 0f;
        }
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
        // Debug.Log($"EnemyVehicle took {damage} damage, remaining HP: {HitPoints}");
        if (HealthBar) HealthBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", HitPoints);
        if (ArmorBar) ArmorBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ArmorPoints);
        if (ShieldBar) ShieldBar.GetComponent<Image>().material.SetInt("_CurrentHitPoints", ShieldPoints);

        if (ShieldEffect) ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);

        if (HitPoints <= 0)
        {
            DestroyVehicle();
        }

        _lastDamageTime = 0f; // Reset shield regeneration timer on taking damage

        return HitPoints > 0;
    }

    public override void DestroyVehicle()
    {
        if (IsDying) return; // Prevent double-destroy
        IsDying = true;

        if (VehicleFaction == Faction.Foe)
        {
            PawnCountManager.UpdateEnemyCountAction?.Invoke();
        }
        else if (VehicleFaction == Faction.Ally)
        {
            PawnCountManager.UpdateAllyCountAction?.Invoke();
        }

        var boid = GetComponent<Boid>();

        if (boid != null && BoidManager != null)
        {
            BoidManager.RemoveBoid(boid);
        }

        if (ExplodeEffect != null)
        {
            Instantiate(ExplodeEffect, transform.position, transform.rotation);
        }
        Destroy(gameObject, 0.1f);
    }
}