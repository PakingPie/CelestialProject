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
    public Image HullPoint;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HullPointShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    public Color HullPointColor1 = Color.green;
    public Color HullPointColor2 = Color.yellow;
    public Color HullPointColor3 = Color.red;
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

    private float _hullPointsRegenTimer = 0f;
    private float _armorRegenTimer = 0f;
    private float _shieldRegenTimer = 0f;
    private float _lastDamageTime = 0f;

    // Cache components to avoid repeated GetComponent calls
    private Material _HullPointMaterial;
    private Material _armorBarMaterial;
    private Material _shieldBarMaterial;
    private Material _shieldEffectMaterial;

    public override VehicleType VehicleType => Type;

    public static bool IsPlayerAlive = true;

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
        _HullPointMaterial = new Material(HullPointShader);
        _HullPointMaterial.SetInt("_MaxHitPoints", MaxHitPoints);
        _HullPointMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _HullPointMaterial.SetVector("_Color1", HullPointColor1);
        _HullPointMaterial.SetVector("_Color2", HullPointColor2);
        _HullPointMaterial.SetVector("_Color3", HullPointColor3);
        HullPoint.material = _HullPointMaterial;

        _armorBarMaterial = new Material(HullPointShader);
        _armorBarMaterial.SetInt("_MaxHitPoints", MaxArmorPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _armorBarMaterial.SetVector("_Color1", ArmorBarColor1);
        _armorBarMaterial.SetVector("_Color2", ArmorBarColor2);
        _armorBarMaterial.SetVector("_Color3", ArmorBarColor3);
        ArmorBar.material = _armorBarMaterial;

        _shieldBarMaterial = new Material(HullPointShader);
        _shieldBarMaterial.SetInt("_MaxHitPoints", MaxShieldPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldBarMaterial.SetVector("_Color1", ShieldBarColor1);
        _shieldBarMaterial.SetVector("_Color2", ShieldBarColor2);
        _shieldBarMaterial.SetVector("_Color3", ShieldBarColor3);
        ShieldBar.material = _shieldBarMaterial;

        _shieldEffectMaterial = new Material(EnergyShieldShader);
        _shieldEffectMaterial.SetFloat("_Strength", 1.0f);

        if (ShieldEffect != null)
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

    public override void RestoreHitPoints() => RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hullPointsRegenTimer, 0.1f, ref _HullPointMaterial, "_CurrentHitPoints");
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
            HitPoints = 0;
            IsPlayerAlive = false;
            GameManager.Instance.GameOver();
            return false;
        }

        _lastDamageTime = 0f;

        return HitPoints > 0;
    }

    private void UpdateUI()
    {
        _HullPointMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldEffectMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
    }
}