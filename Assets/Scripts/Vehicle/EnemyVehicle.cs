using UnityEngine;
using UnityEngine.VFX;
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
    public VisualEffect ExplodeEffect;
    public VisualEffect DamagedSmokeEffect;
    public Transform DamagedPoint;
    [Header("Death Chaos")]
    public bool EnableChaosDeath = true;
    [Range(0f, 1f)] public float ChaosDeathChance = 0.3f;
    public Vector2 ChaosFlyDurationRange = new Vector2(0.5f, 1f);
    public float ChaosForwardSpeedMultiplier = 0.85f;
    public float ChaosMinimumDriftSpeed = 15f;
    public float ChaosDriftJitter = 10f;
    public Vector2 ChaosAngularSpeedRange = new Vector2(180f, 540f);
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
    private VisualEffect _damagedSmokeInstance;
    private bool _smokeEffectInitialized = false;
    private Coroutine _deathRoutine;
    private Faction _deathFaction = Faction.None;

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
            _damagedSmokeInstance.gameObject.SetActive(false);
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
            if (_damagedSmokeInstance != null && !_damagedSmokeInstance.gameObject.activeSelf)
            {
                _damagedSmokeInstance.gameObject.SetActive(true);
                _damagedSmokeInstance.transform.position = DamagedPoint.position;
                _damagedSmokeInstance.Play();
            }
        }
        else
        {
            if (_damagedSmokeInstance != null && _damagedSmokeInstance.gameObject.activeSelf)
            {
                _damagedSmokeInstance.Stop();
                _damagedSmokeInstance.gameObject.SetActive(false);
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
        if (IsDying)
        {
            return false;
        }

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

        BeginDeathState();

        if (ShouldEnterChaosFly())
        {
            _deathRoutine = StartCoroutine(ChaosFlyAndDestroy());
            return;
        }

        FinalizeVehicleDestruction();
    }

    private void BeginDeathState()
    {
        _deathFaction = VehicleFaction;

        if ((_deathFaction & Faction.Foe) != 0)
        {
            PawnCountManager.UpdateEnemyCountAction?.Invoke();
        }
        else if ((_deathFaction & Faction.Ally) != 0)
        {
            PawnCountManager.UpdateAllyCountAction?.Invoke();
        }

        DisableCombatSystems();
        RemoveFromTargetingSystems();
        EnableHitpointBar(false);
    }

    private bool ShouldEnterChaosFly()
    {
        if (!EnableChaosDeath)
        {
            return false;
        }

        return Random.value <= ChaosDeathChance;
    }

    private IEnumerator ChaosFlyAndDestroy()
    {
        float minDuration = Mathf.Min(ChaosFlyDurationRange.x, ChaosFlyDurationRange.y);
        float maxDuration = Mathf.Max(ChaosFlyDurationRange.x, ChaosFlyDurationRange.y);
        float duration = Random.Range(minDuration, maxDuration);
        float driftSpeed = Mathf.Max(Velocity.magnitude * ChaosForwardSpeedMultiplier, ChaosMinimumDriftSpeed);
        Vector3 driftVelocity = transform.forward * driftSpeed + Random.insideUnitSphere * ChaosDriftJitter;
        float angularSpeed = Random.Range(ChaosAngularSpeedRange.x, ChaosAngularSpeedRange.y);
        Vector3 angularVelocity = Random.onUnitSphere * angularSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            driftVelocity += Random.insideUnitSphere * (ChaosDriftJitter * 0.35f * Time.deltaTime);
            transform.position += driftVelocity * Time.deltaTime;
            transform.Rotate(angularVelocity * Time.deltaTime, Space.Self);
            yield return null;
        }

        _deathRoutine = null;
        FinalizeVehicleDestruction();
    }

    private void DisableCombatSystems()
    {
        var weapons = GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            weapon.Targeted = null;
            weapon.IsAimed = false;
            weapon.enabled = false;
        }

        var boid = GetComponent<Boid>();
        if (boid != null)
        {
            BoidManager?.RemoveBoid(boid);
            boid.enabled = false;
        }

        BoidAttackBehavior attackBehavior = GetComponent<BoidAttackBehavior>();
        if (attackBehavior != null)
        {
            attackBehavior.enabled = false;
        }

        BoidCommandController commandController = GetComponent<BoidCommandController>();
        if (commandController != null)
        {
            commandController.enabled = false;
        }
    }

    private void RemoveFromTargetingSystems()
    {
        if (!EnableModuleHits)
        {
            CombatRegistry.Unregister(this, _deathFaction);
        }

        if (_predictionManager != null && EnableIndication)
        {
            if ((_deathFaction & Faction.Foe) != 0)
            {
                _predictionManager.UnregisterEnemy(this);
            }
            else if ((_deathFaction & Faction.Ally) != 0)
            {
                _predictionManager.UnregisterAlly(this);
            }
        }

        VehicleFaction = Faction.Neutral;
    }

    private void FinalizeVehicleDestruction()
    {
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        if (ExplodeEffect != null)
        {
            VisualEffect explode = VFXPool.Instance.Get(ExplodeEffect, transform.position, transform.rotation);
            if (explode != null)
            {
                VFXPooledInstance pooled = explode.GetComponent<VFXPooledInstance>();
                if (pooled == null)
                {
                    pooled = explode.gameObject.AddComponent<VFXPooledInstance>();
                    pooled.Initialize(ExplodeEffect);
                }
                else
                    pooled.spawnTime = Time.time;
            }
        }

        DetachDamagedSmokeOnDeath();

        Destroy(gameObject);
    }

    private void DetachDamagedSmokeOnDeath()
    {
        if (_damagedSmokeInstance == null || !_damagedSmokeInstance.gameObject.activeSelf)
        {
            return;
        }

        _damagedSmokeInstance.transform.parent = null;
        if (_damagedSmokeInstance.HasBool("EnableSmoke"))
        {
            _damagedSmokeInstance.SetBool("EnableSmoke", false);
        }
        _damagedSmokeInstance.Stop();

        AARemoveEffect remove = _damagedSmokeInstance.GetComponent<AARemoveEffect>();
        if (remove == null)
        {
            remove = _damagedSmokeInstance.gameObject.AddComponent<AARemoveEffect>();
        }
        remove.readyToDestroy = true;
    }

    public void EnableHitpointBar(bool enable)
    {
        if (HitpointBarCanvas)
            HitpointBarCanvas.SetActive(enable);
    }
}