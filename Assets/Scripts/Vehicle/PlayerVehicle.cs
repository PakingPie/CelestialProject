using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static GlobalHelper;

// This script manages the player's vehicle, including health, armor, shield, and UI updates.
// It used to single handed the whole ship damage model, now it only manages the player vehicle.
// [ExecuteAlways]
public class PlayerVehicle : VehicleBase
{
    public VehicleType Type = VehicleType.Frigate;
    [Header("UI")]
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    public Color HealthBarColor1 = Color.green;
    public Color HealthBarColor2 = Color.yellow;
    public Color HealthBarColor3 = Color.red;
    public Color ArmorBarColor1 = Color.yellow;
    public Color ArmorBarColor2 = Color.yellow;
    public Color ArmorBarColor3 = Color.yellow;

    public Color ShieldBarColor1 = Color.blue;
    public Color ShieldBarColor2 = Color.blue;
    public Color ShieldBarColor3 = Color.blue;

    [Header("Regeneration")]
    public int HitPointsRegenerationRate = 1;
    public float HitPointsRegenerationDelay = 5f;
    public int ArmorRegenerationRate = 1;
    public float ArmorRegenerationDelay = 5f;
    public int ShieldRegenerationRate = 1;
    public float ShieldRegenerationDelay = 5f;

    private float _hitPointsRegenTimer = 0f;
    private float _armorRegenTimer = 0f;
    private float _shieldRegenTimer = 0f;
    private float _lastDamageTime = 0f;

    // Cache components to avoid repeated GetComponent calls
    private Material _healthBarMaterial;
    private Material _armorBarMaterial;
    private Material _shieldBarMaterial;
    private Material _shieldEffectMaterial;

    public override VehicleType VehicleType => Type;

    // public Faction VehicleFaction = Faction.Player;
    // public override Faction FactionType => VehicleFaction;

    // void OnEnable()
    // {
    //     // Register with CombatRegistry
    //     if (Application.isPlaying)
    //         CombatRegistry.Register(this, FactionType);
    // }

    // void OnDisable()
    // {
    //     // Unregister from CombatRegistry
    //     if (Application.isPlaying)
    //         CombatRegistry.Unregister(this, FactionType);
    // }

    void Start()
    {
        if (!Application.isPlaying) return;

        // Create and cache materials
        _healthBarMaterial = new Material(HealthBarShader);
        _healthBarMaterial.SetInt("_MaxHitPoints", MaxHitPoints);
        _healthBarMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _healthBarMaterial.SetVector("_Color1", HealthBarColor1);
        _healthBarMaterial.SetVector("_Color2", HealthBarColor2);
        _healthBarMaterial.SetVector("_Color3", HealthBarColor3);
        HealthBar.material = _healthBarMaterial;

        _armorBarMaterial = new Material(HealthBarShader);
        _armorBarMaterial.SetInt("_MaxHitPoints", MaxArmorPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _armorBarMaterial.SetVector("_Color1", ArmorBarColor1);
        _armorBarMaterial.SetVector("_Color2", ArmorBarColor2);
        _armorBarMaterial.SetVector("_Color3", ArmorBarColor3);
        ArmorBar.material = _armorBarMaterial;

        _shieldBarMaterial = new Material(HealthBarShader);
        _shieldBarMaterial.SetInt("_MaxHitPoints", MaxShieldPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldBarMaterial.SetVector("_Color1", ShieldBarColor1);
        _shieldBarMaterial.SetVector("_Color2", ShieldBarColor2);
        _shieldBarMaterial.SetVector("_Color3", ShieldBarColor3);
        ShieldBar.material = _shieldBarMaterial;

        _shieldEffectMaterial = new Material(EnergyShieldShader);
        _shieldEffectMaterial.SetFloat("_Strength", 1.0f);
        ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial = _shieldEffectMaterial;

        // GetComponent<ShieldHitEffect>().ShieldGO = ShieldEffect;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Handle shield regeneration
        if (ShieldPoints < MaxShieldPoints || ArmorPoints < MaxArmorPoints || HitPoints < MaxHitPoints)
        {
            _lastDamageTime += Time.deltaTime;

            if (_lastDamageTime >= ShieldRegenerationDelay && ShieldPoints < MaxShieldPoints)
            {
                RestoreShield();
            }
            if (_lastDamageTime >= ArmorRegenerationDelay && ArmorPoints < MaxArmorPoints)
            {
                RestoreArmor();
            }
            if (_lastDamageTime >= HitPointsRegenerationDelay && HitPoints < MaxHitPoints)
            {
                RestoreHitPoints();
            }
        }
    }

    public override void RestoreHitPoints() => RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hitPointsRegenTimer, 0.1f, ref _healthBarMaterial, "_CurrentHitPoints");    
    public override void RestoreArmor() => RegenerateAttributtes(ref ArmorPoints, ref MaxArmorPoints, ref ArmorRegenerationRate, ref _armorRegenTimer, 0.1f, ref _armorBarMaterial, "_CurrentHitPoints");
    public override void RestoreShield() => RegenerateAttributtes(ref ShieldPoints, ref MaxShieldPoints, ref ShieldRegenerationRate, ref _shieldRegenTimer, 0.1f, ref _shieldBarMaterial, "_CurrentHitPoints", true);

    private void RegenerateAttributtes(ref int currentAmount, ref int maxAmount, ref int regenerationRate, ref float regenTimer, float delay, ref Material barMat, string matKeyword, bool isShield = false)
    {
        regenTimer += Time.deltaTime;
        if (regenTimer >= delay && currentAmount < maxAmount)
        {
            currentAmount += regenerationRate;
            if (currentAmount > maxAmount)
                currentAmount = maxAmount;

            barMat.SetInt(matKeyword, currentAmount);
            if (isShield)
            {
                _shieldEffectMaterial.SetFloat("_Strength", currentAmount / (float)maxAmount);
            }
            regenTimer = 0f;
        }
    }

    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
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

        HitPoints = Math.Clamp(HitPoints - damage, 0, MaxHitPoints);

        UpdateUI();

        if (HitPoints <= 0)
        {
            // DestroyVehicle();
            // TODO: Handle player vehicle destruction (game over, respawn, etc.), for now just diable the vehicle
            HitPoints = 0;
            return false;
        }

        _lastDamageTime = 0f;

        return HitPoints > 0;
    }

    private void UpdateUI()
    {
        _healthBarMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldEffectMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
    }
}