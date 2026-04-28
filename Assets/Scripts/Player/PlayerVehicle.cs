using System;
using UnityEngine;
using static GlobalHelper;

// This script manages the player's vehicle, including health, armor, shield, and UI updates.
// It used to single handed the whole ship damage model, now it only manages the player vehicle.
// [ExecuteAlways]
public class PlayerVehicle : VehicleBase
{
    public VehicleType Type = VehicleType.Frigate;

    [Header("Effects")]
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;

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

    private Material _shieldEffectMaterial;

    public override VehicleType VehicleType => Type;

    public static bool IsPlayerAlive = true;

    [HideInInspector]
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

    void Start()
    {
        if (!Application.isPlaying) return;

        InitializeShieldEffect();
    }

    void OnDestroy()
    {
        ReleaseMaterial(ref _shieldEffectMaterial);
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

    public override void RestoreHitPoints() => RegenerateAttributes(ref HitPoints, MaxHitPoints, HitPointsRegenerationRate, ref _hullPointsRegenTimer, 0.1f);
    public override void RestoreArmor() => RegenerateAttributes(ref ArmorPoints, MaxArmorPoints, ArmorRegenerationRate, ref _armorRegenTimer, 0.1f);
    public override void RestoreShield() => RegenerateAttributes(ref ShieldPoints, MaxShieldPoints, ShieldRegenerationRate, ref _shieldRegenTimer, 0.1f, true);

    private void RegenerateAttributes(ref int currentAmount, int maxAmount, int regenerationRate, ref float regenTimer, float delay, bool updateShieldEffect = false)
    {
        regenTimer += Time.deltaTime;
        if (regenTimer < delay || currentAmount >= maxAmount)
            return;

        currentAmount = Math.Min(maxAmount, currentAmount + regenerationRate);

        if (updateShieldEffect)
            UpdateShieldEffectStrength();

        regenTimer = 0f;
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
            case AmmoType.Ion:
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
    UpdateShieldEffectStrength();

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

    private void InitializeShieldEffect()
    {
        if (EnergyShieldShader == null || ShieldEffect == null)
            return;

        MeshRenderer shieldRenderer = ShieldEffect.GetComponent<MeshRenderer>();
        if (shieldRenderer == null)
            return;

        _shieldEffectMaterial = new Material(EnergyShieldShader);
        shieldRenderer.sharedMaterial = _shieldEffectMaterial;
        UpdateShieldEffectStrength();
    }

    private void UpdateShieldEffectStrength()
    {
        if (_shieldEffectMaterial == null)
            return;

        float strength = MaxShieldPoints > 0 ? ShieldPoints / (float)MaxShieldPoints : 0f;
        _shieldEffectMaterial.SetFloat("_Strength", strength);
    }

    private static void ReleaseMaterial(ref Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);

        material = null;
    }
}
