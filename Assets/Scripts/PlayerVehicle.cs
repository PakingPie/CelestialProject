using UnityEngine;
using UnityEngine.UI;
using static GlobalHelper;

[ExecuteAlways]
public class PlayerVehicle : VehicleBase
{
    [Header("Faction")]
    public Faction FactionType = Faction.Player;

    [Header("UI")]
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;

    [Header("Shield Regeneration")]
    public int ShieldRegenerationRate = 1;
    public float ShieldRegenerationDelay = 5f;
    
    private float _shieldRegenTimer = 0f;
    private float _lastDamageTime = 0f;

    // Cache components to avoid repeated GetComponent calls
    private Material _healthBarMaterial;
    private Material _armorBarMaterial;
    private Material _shieldBarMaterial;
    private Material _shieldEffectMaterial;

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

        // Create and cache materials
        _healthBarMaterial = new Material(HealthBarShader);
        _healthBarMaterial.SetInt("_MaxHitPoints", MaxHitPoints);
        _healthBarMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _healthBarMaterial.SetVector("_Color1", Color.green);
        _healthBarMaterial.SetVector("_Color2", Color.yellow);
        _healthBarMaterial.SetVector("_Color3", Color.red);
        HealthBar.material = _healthBarMaterial;

        _armorBarMaterial = new Material(HealthBarShader);
        _armorBarMaterial.SetInt("_MaxHitPoints", MaxArmorPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _armorBarMaterial.SetVector("_Color1", Color.yellow);
        _armorBarMaterial.SetVector("_Color2", Color.yellow);
        _armorBarMaterial.SetVector("_Color3", new Color(0.6f, 0.3f, 0f)); // brown
        ArmorBar.material = _armorBarMaterial;

        _shieldBarMaterial = new Material(HealthBarShader);
        _shieldBarMaterial.SetInt("_MaxHitPoints", MaxShieldPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldBarMaterial.SetVector("_Color1", Color.cyan);
        _shieldBarMaterial.SetVector("_Color2", Color.cyan);
        _shieldBarMaterial.SetVector("_Color3", new Color(0f, 0.5f, 0.5f)); // dark cyan
        ShieldBar.material = _shieldBarMaterial;

        _shieldEffectMaterial = new Material(EnergyShieldShader);
        _shieldEffectMaterial.SetFloat("_Strength", 1.0f);
        ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial = _shieldEffectMaterial;

        GetComponent<ShieldHitEffect>().ShieldGO = ShieldEffect;
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Handle shield regeneration
        if (ShieldPoints < MaxShieldPoints)
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
        if (_shieldRegenTimer >= 0.1f && ShieldPoints < MaxShieldPoints)
        {
            ShieldPoints += ShieldRegenerationRate;
            if (ShieldPoints > MaxShieldPoints)
                ShieldPoints = MaxShieldPoints;

            _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
            _shieldEffectMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
            _shieldRegenTimer = 0f;
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

        HitPoints -= damage;

        UpdateUI();

        if (HitPoints <= 0)
        {
            // DestroyVehicle();
        }

        _lastDamageTime = 0f;

        return HitPoints > 0;
    }

    private int ProcessKineticDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage / 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage * 2;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints > 0 && ShieldPoints <= 0)
            return (int)(damage * 0.5f) + shieldFloatDamage;
        else
            return 0;
    }

    private int ProcessEnergyDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage * 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage / 2;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.5f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + armorFloatDamage;
        else
            return 0;
    }

    private int ProcessExplosiveDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return damage * 2;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + armorFloatDamage;
        else if (ShieldPoints <= 0 && ArmorPoints > 0)
            return (int)(damage * 0.75f) + shieldFloatDamage;
        else
            return (int)(damage * 0.25f);
    }

    private int ProcessPlasmaDamage(int damage)
    {
        int armorFloatDamage = 0;
        if (ArmorPoints > 0)
        {
            ArmorPoints -= damage / 2;
            if (ArmorPoints <= 0)
            {
                armorFloatDamage = -ArmorPoints / 2;
                ArmorPoints = 0;
            }
        }

        int shieldFloatDamage = 0;
        if (ShieldPoints > 0)
        {
            ShieldPoints -= damage * 3;
            if (ShieldPoints <= 0)
            {
                shieldFloatDamage = -ShieldPoints / 2;
                ShieldPoints = 0;
            }
        }

        if (ArmorPoints <= 0 && ShieldPoints <= 0)
            return (int)(damage * 1.25f) + armorFloatDamage + shieldFloatDamage;
        else if (ArmorPoints <= 0 && ShieldPoints > 0)
            return (int)(damage * 0.5f) + shieldFloatDamage;
        else
            return 0;
    }

    private void UpdateUI()
    {
        _healthBarMaterial.SetInt("_CurrentHitPoints", HitPoints);
        _armorBarMaterial.SetInt("_CurrentHitPoints", ArmorPoints);
        _shieldBarMaterial.SetInt("_CurrentHitPoints", ShieldPoints);
        _shieldEffectMaterial.SetFloat("_Strength", ShieldPoints / (float)MaxShieldPoints);
    }
}