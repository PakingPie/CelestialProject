using UnityEngine;
using static GlobalHelper;

public class Asteroid : VehicleBase
{
    [Header("Asteroid Settings")]
    [SerializeField] private VehicleType _vehicleType = VehicleType.Platform;
    [SerializeField] private Faction _faction = Faction.Neutral;
    
    [Header("Destruction")]
    [SerializeField] private ParticleSystem _destructionFXPrefab;
    [SerializeField] private GameObject[] _debrisPrefabs;
    [SerializeField] private int _debrisCount = 3;
    [SerializeField] private float _debrisForce = 10f;
    
    [Header("Drops")]
    [SerializeField] private GameObject[] _dropPrefabs;
    [SerializeField, Range(0f, 1f)] private float _dropChance = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _destroySound;
    
    // References
    private MeshRenderer _renderer;
    private AsteroidRingSpawner _spawner;
    private int _asteroidIndex = -1;
    
    // Visual feedback
    private Material _material;
    private Color _originalColor;
    private float _hitFlashTimer;
    private const float HIT_FLASH_DURATION = 0.1f;

    // Velocity tracking
    private Vector3 _lastPosition;
    private Vector3 _velocity;
    public Vector3 Velocity => _velocity;

    // VehicleBase overrides
    public override VehicleType VehicleType => _vehicleType;
    public override Faction FactionType => _faction;

    void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        
        if (_renderer != null)
        {
            _material = _renderer.material;
            _originalColor = _material.color;
        }
        
        _lastPosition = transform.position;
        
        // Asteroids have no shields or armor by default
        ShieldPoints = 0;
        MaxShieldPoints = 0;
        ArmorPoints = 0;
        MaxArmorPoints = 0;
        
        // Set owner to self
        OwnerShip = gameObject;
    }

    void OnEnable()
    {
        // Register with combat registry as Neutral
        CombatRegistry.Register(this, _faction);
    }

    void OnDisable()
    {
        // Unregister from combat registry
        CombatRegistry.Unregister(this, _faction);
    }
    
    void Update()
    {
        // Track velocity
        _velocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;
        
        // Handle hit flash
        if (_hitFlashTimer > 0)
        {
            _hitFlashTimer -= Time.deltaTime;
            if (_hitFlashTimer <= 0 && _material != null)
            {
                _material.color = _originalColor;
            }
        }
    }
    
    /// <summary>
    /// Initialize asteroid with spawner reference for cleanup
    /// </summary>
    public void Initialize(AsteroidRingSpawner spawner, int index, int health)
    {
        _spawner = spawner;
        _asteroidIndex = index;
        MaxHitPoints = health;
        HitPoints = health;
    }
    
    /// <summary>
    /// Initialize standalone asteroid (not from spawner)
    /// </summary>
    public void Initialize(int health)
    {
        MaxHitPoints = health;
        HitPoints = health;
    }
    
    /// <summary>
    /// Handle damage from bullets
    /// </summary>
    public override bool TakeDamage(int damage, AmmoType ammoType)
    {
        if (HitPoints <= 0) return false;
        
        // Apply damage type multipliers
        int finalDamage = CalculateDamage(damage, ammoType);
        
        HitPoints -= finalDamage;
        
        // Debug.Log($"Asteroid took {finalDamage} {ammoType} damage! Health: {HitPoints}/{MaxHitPoints}");
        
        FlashHit();
        
        if (_hitSound != null)
        {
            AudioSource.PlayClipAtPoint(_hitSound, transform.position, 0.5f);
        }
        
        if (HitPoints <= 0)
        {
            DestroyVehicle();
            return false;
        }
        
        return true;
    }
    
    private int CalculateDamage(int damage, AmmoType ammoType)
    {
        float multiplier = ammoType switch
        {
            AmmoType.Kinetic => 1.5f,    // Kinetic is strong vs asteroids
            AmmoType.Explosive => 2.0f,   // Explosives are very effective
            AmmoType.Energy => 0.75f,     // Energy less effective vs rock
            AmmoType.Plasma => 1.25f,     // Plasma melts rock decently
            AmmoType.Pierce => 0.5f,      // Pierce designed for armor, not rock
            AmmoType.EMP => 0f,           // EMP does nothing to rock
            _ => 1f
        };
        
        return Mathf.RoundToInt(damage * multiplier);
    }
    
    private void FlashHit()
    {
        if (_material != null)
        {
            _material.color = Color.white;
            _hitFlashTimer = HIT_FLASH_DURATION;
        }
    }
    
    public override void DestroyVehicle()
    {
        // Debug.Log($"Asteroid {_asteroidIndex} destroyed!");
        
        // Notify spawner
        if (_spawner != null)
        {
            _spawner.OnAsteroidDestroyed(_asteroidIndex);
        }
        
        // Play destruction sound
        if (_destroySound != null)
        {
            AudioSource.PlayClipAtPoint(_destroySound, transform.position);
        }
        
        // Spawn destruction FX
        if (_destructionFXPrefab != null)
        {
            ParticleSystem fx = Instantiate(_destructionFXPrefab, transform.position, Quaternion.identity);
            fx.transform.localScale = transform.localScale;
            fx.Play();
        }
        
        SpawnDebris();
        SpawnDrops();
        
        Destroy(gameObject);
    }
    
    private void SpawnDebris()
    {
        if (_debrisPrefabs == null || _debrisPrefabs.Length == 0) return;
        
        for (int i = 0; i < _debrisCount; i++)
        {
            GameObject debrisPrefab = _debrisPrefabs[Random.Range(0, _debrisPrefabs.Length)];
            if (debrisPrefab == null) continue;
            
            Vector3 offset = Random.insideUnitSphere * transform.localScale.magnitude * 0.5f;
            GameObject debris = Instantiate(debrisPrefab, transform.position + offset, Random.rotation);
            
            debris.transform.localScale = transform.localScale * Random.Range(0.2f, 0.4f);
            
            Rigidbody rb = debris.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Random.insideUnitSphere * _debrisForce;
                rb.angularVelocity = Random.insideUnitSphere * _debrisForce;
            }
            
            Destroy(debris, 5f);
        }
    }
    
    private void SpawnDrops()
    {
        if (_dropPrefabs == null || _dropPrefabs.Length == 0) return;
        if (Random.value > _dropChance) return;
        
        GameObject dropPrefab = _dropPrefabs[Random.Range(0, _dropPrefabs.Length)];
        if (dropPrefab != null)
        {
            Instantiate(dropPrefab, transform.position, Quaternion.identity);
        }
    }
    
    // VehicleBase method stubs - asteroids don't regenerate
    public override void RestoreHitPoints() { }
    public override void RestoreArmor() { }
    public override void RestoreShield() { }
    
    void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}