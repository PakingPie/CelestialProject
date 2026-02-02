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
    public GameObject HitpointBarCanvas;
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    public ParticleSystem ExplodeEffect;
    public ParticleSystem DamagedSmokeEffect;
    public Transform DamagedPoint;
    [Header("Regeneration")]
    public int HitPointsRegenerationRate = 1;
    public float HitPointsRegenerationDelay = 40f;
    public int ArmorRegenerationRate = 1;
    public float ArmorRegenerationDelay = 40f;
    public int ShieldRegenerationRate = 1;
    public float ShieldRegenerationDelay = 20f;

    private float _hitPointsRegenTimer = 0f;
    private float _armorRegenTimer = 0f;
    private float _shieldRegenTimer = 0f;
    private float _lastDamageTime = 0f;
    public override Faction FactionType => VehicleFaction;
    public override VehicleType VehicleType => Type;

    public bool EnableIndication = false;
    public bool EnableModuleHits = false;
    public bool IsDying { get; private set; } = false;
    private Vector3 _lastPosition;
    private Vector3 _velocity;
    public Vector3 Velocity => _velocity;
    private EnemyPredictionManager _predictionManager;
    private ParticleSystem _damagedSmokeInstance;
    private bool _smokeEffectInitialized = false;

    void OnEnable()
    {
        if (!EnableModuleHits)
            CombatRegistry.Register(this, FactionType);
    }

    void OnDisable()
    {
        if (!EnableModuleHits)
            CombatRegistry.Unregister(this, FactionType);
    }

    void OnDestroy()
    {
        if (_predictionManager != null && EnableIndication)
        {
            if (FactionType == Faction.Foe) _predictionManager.UnregisterEnemy(this);
            else if (FactionType == Faction.Ally) _predictionManager.UnregisterAlly(this);
        }
    }

    void Start()
    {
        _lastPosition = transform.position;

        // Register with manager
        _predictionManager = FindAnyObjectByType<EnemyPredictionManager>();
        if (_predictionManager != null && EnableIndication)
        {
            if (FactionType == Faction.Foe) _predictionManager.RegisterEnemy(this);
            else if (FactionType == Faction.Ally) _predictionManager.RegisterAlly(this);
        }

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
        }

        if (!_smokeEffectInitialized && DamagedSmokeEffect != null && DamagedPoint != null)
        {
            _damagedSmokeInstance = Instantiate(DamagedSmokeEffect, DamagedPoint);
            _damagedSmokeInstance.transform.localPosition = Vector3.zero;
            _damagedSmokeInstance.transform.localEulerAngles = Vector3.zero;
            _damagedSmokeInstance.Stop();
            _smokeEffectInitialized = true;
        }
    }

    void Update()
    {
        _velocity = (transform.position - _lastPosition) / Time.deltaTime;

        _lastPosition = transform.position;

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
            EnableHitpointBar(true);
        }
        else
        {
            EnableHitpointBar(false);
        }

        if (HitPoints < MaxHitPoints / 2)
        {
            if (_damagedSmokeInstance != null && !_damagedSmokeInstance.isPlaying)
            {
                _damagedSmokeInstance.transform.position = DamagedPoint.position;
                _damagedSmokeInstance.Play();
            }
        }
        else
        {
            if (_damagedSmokeInstance != null && _damagedSmokeInstance.isPlaying)
            {
                _damagedSmokeInstance.Stop();
            }
        }
    }

    public override void RestoreHitPoints()
    {
        if (HealthBar != null)
            RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hitPointsRegenTimer, 0.1f, HealthBar.GetComponent<Image>().material, "_CurrentHitPoints");
        else
            RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hitPointsRegenTimer);
    }
    public override void RestoreArmor()
    {
        if (ArmorBar != null)
            RegenerateAttributtes(ref ArmorPoints, ref MaxArmorPoints, ref ArmorRegenerationRate, ref _armorRegenTimer, 0.1f, ArmorBar.GetComponent<Image>().material, "_CurrentHitPoints");
        else
            RegenerateAttributtes(ref ArmorPoints, ref MaxArmorPoints, ref ArmorRegenerationRate, ref _armorRegenTimer);

    }
    public override void RestoreShield()
    {
        if (ShieldBar != null)
            RegenerateAttributtes(ref ShieldPoints, ref MaxShieldPoints, ref ShieldRegenerationRate, ref _shieldRegenTimer, 0.1f, ShieldBar.GetComponent<Image>().material, "_CurrentHitPoints", true);
        else
            RegenerateAttributtes(ref ShieldPoints, ref MaxShieldPoints, ref ShieldRegenerationRate, ref _shieldRegenTimer);
    }
    private void RegenerateAttributtes(ref int currentAmount, ref int maxAmount, ref int regenerationRate, ref float regenTimer, float delay, Material barMat = null, string matKeyword = "", bool isShield = false)
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
                ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", currentAmount / (float)maxAmount);
            }
            regenTimer = 0f;
        }
    }

    private void RegenerateAttributtes(ref int currentAmount, ref int maxAmount, ref int regenerationRate, ref float regenTimer, float delay = 0.1f, bool isShield = false)
    {
        regenTimer += Time.deltaTime;
        if (regenTimer >= delay && currentAmount < maxAmount)
        {
            currentAmount += regenerationRate;
            if (currentAmount > maxAmount)
                currentAmount = maxAmount;

            if (isShield)
            {
                ShieldEffect.GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Strength", currentAmount / (float)maxAmount);
            }
            regenTimer = 0f;
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
        // Disable all weapons but keeps visual, then destroy after short delay
        var weapons = GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            weapon.enabled = false;
        }
        // Set Faction to Neutral to avoid further interactions
        VehicleFaction = Faction.Neutral;

        Destroy(gameObject, 3.0f);
    }

    public void EnableHitpointBar(bool enable)
    {
        if (HitpointBarCanvas)
            HitpointBarCanvas.SetActive(enable);
    }
}