using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
using System.Collections.Generic;
using static GlobalHelper;
using UnityEngine.UI;
using UnityEngine.Serialization;

[ExecuteAlways]
public class EnemyVehicle : VehicleBase
{
    private static readonly int _maxHitPointsID = Shader.PropertyToID("_MaxHitPoints");
    private static readonly int _currentHitPointsID = Shader.PropertyToID("_CurrentHitPoints");
    private static readonly int _damageID = Shader.PropertyToID("_Damage");
    private static readonly int _damageFlashSpeedID = Shader.PropertyToID("_DamageFlashSpeed");
    private static readonly int _damageFlashColorID = Shader.PropertyToID("_DamageFlashColor");
    private static readonly int _legacyDamageFlashColorID = Shader.PropertyToID("_DamgeFlashColor");

    public Faction VehicleFaction = Faction.Foe;
    public VehicleType Type = VehicleType.Frigate;
    public GameObject HitpointBarCanvas;
    public Image HealthBar;
    public Image ArmorBar;
    public Image ShieldBar;
    public Shader HealthBarShader;
    public Shader ArmorBarShader;
    public Shader ShieldBarShader;
    public Material HealthBarMaterialTemplate;
    public Material ArmorBarMaterialTemplate;
    public Material ShieldBarMaterialTemplate;
    [FormerlySerializedAs("DamageFlashTime")]
    [Min(0f)] public float DamageFlashSpeed = 2f;
    [Min(0f)] public float DamageFlashHoldDuration = 0.35f;
    public Color ArmorDamageFlashColor = Color.white;
    public Color ShieldDamageFlashColor = Color.white;
    public Shader EnergyShieldShader;
    public GameObject ShieldEffect;
    public VisualEffect ExplodeEffect;
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
    private Coroutine _deathRoutine;
    private Faction _deathFaction = Faction.None;
    private bool _hitBarVisible = false;
    private int _recentHullDamage;
    private int _recentArmorDamage;
    private int _recentShieldDamage;
    private float _recentHullDamageTimer;
    private float _recentArmorDamageTimer;
    private float _recentShieldDamageTimer;
    private Material _healthBarMat;
    private Material _armorBarMat;
    private Material _shieldBarMat;
    private Material _shieldEffectMat;

    public int RecentHullDamage => _recentHullDamage;
    public int RecentArmorDamage => _recentArmorDamage;
    public int RecentShieldDamage => _recentShieldDamage;

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
            if (FactionType == Faction.Foe) _predictionManager.UnregisterEnemy(this);
            else if (FactionType == Faction.Ally) _predictionManager.UnregisterAlly(this);
        }

        ReleaseMaterial(ref _healthBarMat);
        ReleaseMaterial(ref _armorBarMat);
        ReleaseMaterial(ref _shieldBarMat);
        ReleaseMaterial(ref _shieldEffectMat);
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

        InitializeHitpointBarMaterials();

        if (ShieldEffect)
        {
            var renderer = ShieldEffect.GetComponent<MeshRenderer>();
            _shieldEffectMat = new Material(EnergyShieldShader);
            renderer.sharedMaterial = _shieldEffectMat;
            _shieldEffectMat.SetFloat("_Strength", 1.0f);
        }

        if (DamagedPoint != null)
        {
            _damagedSmokeInstance = DamagedPoint.GetComponentInChildren<VisualEffect>(true);
            if (_damagedSmokeInstance != null)
            {
                _damagedSmokeInstance.Stop();
                _damagedSmokeInstance.gameObject.SetActive(false);
            }
        }
    }

    void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime > 0f)
            _velocity = (transform.position - _lastPosition) / deltaTime;
        else
            _velocity = Vector3.zero;

        _lastPosition = transform.position;

        UpdateDamageFlashTimers();

        // Handle shield regeneration
        if (ShieldPoints < MaxShieldPoints || ArmorPoints < MaxArmorPoints || HitPoints < MaxHitPoints)
        {
            _lastDamageTime += deltaTime;

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
        RegenerateAttributtes(ref HitPoints, ref MaxHitPoints, ref HitPointsRegenerationRate, ref _hitPointsRegenTimer, 0.1f, _healthBarMat);
    }
    public override void RestoreArmor()
    {
        RegenerateAttributtes(ref ArmorPoints, ref MaxArmorPoints, ref ArmorRegenerationRate, ref _armorRegenTimer, 0.1f, _armorBarMat);
    }
    public override void RestoreShield()
    {
        RegenerateAttributtes(ref ShieldPoints, ref MaxShieldPoints, ref ShieldRegenerationRate, ref _shieldRegenTimer, 0.1f, _shieldBarMat, true);
    }
    private void RegenerateAttributtes(ref int currentAmount, ref int maxAmount, ref int regenerationRate, ref float regenTimer, float delay, Material barMat = null, bool isShield = false)
    {
        regenTimer += Time.deltaTime;
        if (regenTimer >= delay && currentAmount < maxAmount)
        {
            currentAmount += regenerationRate;
            if (currentAmount > maxAmount)
                currentAmount = maxAmount;

            UpdateCurrentHitPointValue(barMat, currentAmount);
            if (isShield && _shieldEffectMat != null)
            {
                _shieldEffectMat.SetFloat("_Strength", currentAmount / (float)maxAmount);
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

        int previousHitPoints = HitPoints;
        int previousArmorPoints = ArmorPoints;
        int previousShieldPoints = ShieldPoints;

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

        HitPoints = Mathf.Clamp(HitPoints - damage, 0, MaxHitPoints);

        RegisterRecentDamage(previousHitPoints - HitPoints, ref _recentHullDamage, ref _recentHullDamageTimer, _healthBarMat);
        RegisterRecentDamage(previousArmorPoints - ArmorPoints, ref _recentArmorDamage, ref _recentArmorDamageTimer, _armorBarMat);
        RegisterRecentDamage(previousShieldPoints - ShieldPoints, ref _recentShieldDamage, ref _recentShieldDamageTimer, _shieldBarMat);

        UpdateCurrentHitPointValue(_healthBarMat, HitPoints);
        UpdateCurrentHitPointValue(_armorBarMat, ArmorPoints);
        UpdateCurrentHitPointValue(_shieldBarMat, ShieldPoints);

        if (_shieldEffectMat != null)
            _shieldEffectMat.SetFloat("_Strength", MaxShieldPoints > 0 ? ShieldPoints / (float)MaxShieldPoints : 0f);

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
        if(_hitBarVisible == enable) return;
        _hitBarVisible = enable;
        if (HitpointBarCanvas)
            HitpointBarCanvas.SetActive(enable);
    }

    private void InitializeHitpointBarMaterials()
    {
        ReleaseMaterial(ref _healthBarMat);
        ReleaseMaterial(ref _armorBarMat);
        ReleaseMaterial(ref _shieldBarMat);

        _healthBarMat = CreateHitpointBarMaterial(HealthBar, HealthBarMaterialTemplate, HealthBarShader);
        _armorBarMat = CreateHitpointBarMaterial(ArmorBar, ArmorBarMaterialTemplate, ArmorBarShader != null ? ArmorBarShader : HealthBarShader);
        _shieldBarMat = CreateHitpointBarMaterial(ShieldBar, ShieldBarMaterialTemplate, ShieldBarShader != null ? ShieldBarShader : HealthBarShader);

        ConfigureHitpointBarMaterial(_healthBarMat, MaxHitPoints, HitPoints, _recentHullDamage, DamageFlashSpeed, null);
        ConfigureHitpointBarMaterial(_armorBarMat, MaxArmorPoints, ArmorPoints, _recentArmorDamage, DamageFlashSpeed, ArmorDamageFlashColor);
        ConfigureHitpointBarMaterial(_shieldBarMat, MaxShieldPoints, ShieldPoints, _recentShieldDamage, DamageFlashSpeed, ShieldDamageFlashColor);
    }

    private Material CreateHitpointBarMaterial(Image image, Material materialTemplate, Shader shader)
    {
        if (image == null)
            return null;

        Material materialInstance = null;
        if (materialTemplate != null)
            materialInstance = new Material(materialTemplate);
        else if (shader != null)
            materialInstance = new Material(shader);

        if (materialInstance != null)
            image.material = materialInstance;

        return materialInstance;
    }

    private void ConfigureHitpointBarMaterial(Material barMat, int maxAmount, int currentAmount, int recentDamage, float damageFlashSpeed, Color? damageFlashColor)
    {
        if (barMat == null)
            return;

        barMat.SetFloat(_maxHitPointsID, maxAmount);
        barMat.SetFloat(_currentHitPointsID, currentAmount);
        barMat.SetFloat(_damageID, recentDamage);

        if (barMat.HasProperty(_damageFlashSpeedID))
            barMat.SetFloat(_damageFlashSpeedID, damageFlashSpeed);

        if (damageFlashColor.HasValue)
            SetDamageFlashColor(barMat, damageFlashColor.Value);
    }

    private void UpdateCurrentHitPointValue(Material barMat, int currentAmount)
    {
        if (barMat == null)
            return;

        barMat.SetFloat(_currentHitPointsID, currentAmount);
    }

    private void RegisterRecentDamage(int damageAmount, ref int recentDamage, ref float recentDamageTimer, Material barMat)
    {
        if (damageAmount <= 0)
            return;

        float holdDuration = Mathf.Max(0f, DamageFlashHoldDuration);
        if (holdDuration <= 0f)
        {
            recentDamage = 0;
            recentDamageTimer = 0f;

            if (barMat != null)
                barMat.SetFloat(_damageID, 0f);

            return;
        }

        recentDamage = recentDamageTimer > 0f ? recentDamage + damageAmount : damageAmount;
        recentDamageTimer = holdDuration;

        if (barMat != null)
            barMat.SetFloat(_damageID, recentDamage);
    }

    private void UpdateDamageFlashTimers()
    {
        UpdateDamageFlashTimer(ref _recentHullDamage, ref _recentHullDamageTimer, _healthBarMat);
        UpdateDamageFlashTimer(ref _recentArmorDamage, ref _recentArmorDamageTimer, _armorBarMat);
        UpdateDamageFlashTimer(ref _recentShieldDamage, ref _recentShieldDamageTimer, _shieldBarMat);
    }

    private void UpdateDamageFlashTimer(ref int recentDamage, ref float recentDamageTimer, Material barMat)
    {
        if (recentDamageTimer <= 0f)
            return;

        recentDamageTimer -= Time.deltaTime;
        if (recentDamageTimer > 0f)
            return;

        recentDamageTimer = 0f;
        recentDamage = 0;

        if (barMat != null)
            barMat.SetFloat(_damageID, 0f);
    }

    private void SetDamageFlashColor(Material barMat, Color color)
    {
        if (barMat == null)
            return;

        if (barMat.HasProperty(_legacyDamageFlashColorID))
            barMat.SetColor(_legacyDamageFlashColorID, color);

        if (barMat.HasProperty(_damageFlashColorID))
            barMat.SetColor(_damageFlashColorID, color);
    }

    private void ReleaseMaterial(ref Material material)
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