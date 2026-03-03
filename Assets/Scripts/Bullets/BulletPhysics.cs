using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;
using static GlobalHelper;

public class BulletPhysics : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private VisualEffect _impactFXPrefab = null;
    [SerializeField] private VisualEffect _explodeFXPrefab = null;
    [SerializeField] private List<TrailRenderer> _childTrails = new List<TrailRenderer>();

    public int Damage = 2;

    public bool CanDamagePlanet = false;
    public bool CanDamageAsteroids = true;  // Keep this flag for Gun.cs to check

    [Header("Motion")]
    public float Speed = 50f;
    public float LifeTime = 5f;
    public int FuseDetonationDistance = 1;
    public int ExplosionRadius = 5;

    [Header("Velocity Inheritance")]
    [Range(0f, 1f)]
    public float velocityInheritance = 1f;
    public bool alignToVelocity = false;
    public float alignmentSpeed = 5f;

    public AmmoType DamageType = AmmoType.Kinetic;
    public Faction FireTarget = Faction.Foe;

    [Header("Explosions")]
    public bool ExplodeOnImpact = false;

    [Header("Performance")]
    [Tooltip("Use CombatRegistry spatial grid for faster queries.")]
    public bool UseSpatialGrid = true;
    [Tooltip("Only check for hits every N frames. 1 = every frame.")]
    [Min(1)] public int HitCheckIntervalFrames = 2;

    public Transform TargetObject;

    // Cached
    private Transform _cachedTransform;
    private float _lifeTimer;
    private float _fuseDistSqr;
    private float _explosionRadiusSqr;
    private RaycastHit _hit;

    // Velocity-based movement
    private Vector3 _velocity;
    private Vector3 _inheritedVelocity;
    private bool _initialized = false;
    private Vector3 _previousPosition;
    private int _hitCheckCounter = 0;

    // Reusable lists
    private static List<VehicleBase> _nearbyTargets = new List<VehicleBase>(64);
    private static List<VehicleBase> _targetsInExplosion = new List<VehicleBase>(16);
    private static List<VehicleBase> _nearbyNeutrals = new List<VehicleBase>(32);
    private static HashSet<VehicleBase> _damagedParents = new HashSet<VehicleBase>(64);

    void Awake()
    {
        _cachedTransform = transform;
        _fuseDistSqr = FuseDetonationDistance * FuseDetonationDistance;
        _explosionRadiusSqr = ExplosionRadius * ExplosionRadius;
    }

    private void OnEnable()  { BlackHoleGravity.RegisterBullet(this); }
    private void OnDisable() { BlackHoleGravity.UnregisterBullet(this); }

    public void Initialize(Vector3 shooterVelocity)
    {
        _inheritedVelocity = shooterVelocity * velocityInheritance;
        _velocity = _cachedTransform.forward * Speed + _inheritedVelocity;
        _previousPosition = _cachedTransform.position;
        _initialized = true;
    }

    public void Initialize(Vector3 direction, Vector3 shooterVelocity)
    {
        _cachedTransform.forward = direction.normalized;
        _inheritedVelocity = shooterVelocity * velocityInheritance;
        _velocity = direction.normalized * Speed + _inheritedVelocity;
        _previousPosition = _cachedTransform.position;
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized)
        {
            _velocity = _cachedTransform.forward * Speed;
            _previousPosition = _cachedTransform.position;
            _initialized = true;
        }

        _previousPosition = _cachedTransform.position;

        // Move bullet
        Vector3 movement = _velocity * Time.deltaTime;
        _cachedTransform.position += movement;

        // Align to velocity
        if (alignToVelocity && _velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_velocity.normalized);
            _cachedTransform.rotation = Quaternion.Slerp(
                _cachedTransform.rotation,
                targetRotation,
                alignmentSpeed * Time.deltaTime
            );
        }

        // Lifetime check
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= LifeTime)
        {
            DestroyBullet();
            return;
        }

        // Check for hits
        if (HitCheckIntervalFrames < 1) HitCheckIntervalFrames = 1;
        _hitCheckCounter++;
        if (_hitCheckCounter >= HitCheckIntervalFrames)
        {
            _hitCheckCounter = 0;
            CheckHits();
        }
    }

    private void CheckHits()
    {
        if (ExplosionRadius <= 0 || FuseDetonationDistance <= 0)
            return;

        Vector3 myPosition = _cachedTransform.position;

        // Get targets based on FireTarget faction
        if (UseSpatialGrid)
            CombatRegistry.FindEnemiesInRange(myPosition, ExplosionRadius, FireTarget, _nearbyTargets);
        else
            CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, FireTarget, _nearbyTargets, true);

        // Also check neutrals if we can damage asteroids
        if (CanDamageAsteroids)
        {
            if (UseSpatialGrid)
                CombatRegistry.FindEnemiesInRange(myPosition, ExplosionRadius, Faction.Neutral, _nearbyNeutrals);
            else
                CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, Faction.Neutral, _nearbyNeutrals, true);

            if (_nearbyNeutrals.Count > 0)
                _nearbyTargets.AddRange(_nearbyNeutrals);
        }

        // Find closest target within fuse distance
        bool fuseTriggered = false;

        for (int i = 0; i < _nearbyTargets.Count; i++)
        {
            VehicleBase target = _nearbyTargets[i];
            if (target == null) continue;

            float distSqr = (target.CachedTransform.position - myPosition).sqrMagnitude;
            if (distSqr <= _fuseDistSqr)
            {
                fuseTriggered = true;
                break;
            }
        }

        // Check if closest target is within fuse distance
        if (fuseTriggered)
        {
            // Collect all targets in explosion radius
            _targetsInExplosion.Clear();
            for (int i = 0; i < _nearbyTargets.Count; i++)
            {
                VehicleBase target = _nearbyTargets[i];
                if (target == null) continue;

                float distSqr = (target.CachedTransform.position - myPosition).sqrMagnitude;
                if (distSqr <= _explosionRadiusSqr)
                {
                    _targetsInExplosion.Add(target);
                }
            }

            DestroyBulletWithDamage(myPosition, _targetsInExplosion);
        }
    }

    private void DestroyBulletWithDamage(Vector3 impactPoint, List<VehicleBase> targets)
    {
        // Group VehicleModules and WeaponPlatforms by their parent vehicle to prevent multiple damage from same parent
        _damagedParents.Clear();

        for (int i = 0; i < targets.Count; i++)
        {
            VehicleBase target = targets[i];
            if (target == null) continue;

            // Determine the actual vehicle to damage
            VehicleBase vehicleToDamage = null;

            if (target is VehicleModule vehicleModule)
            {
                // For VehicleModule, get its parent vehicle
                vehicleToDamage = vehicleModule.OwnerShip?.GetComponent<VehicleBase>();
            }
            else if (target is WeaponPlatform weaponPlatform)
            {
                // For WeaponPlatform, get its parent vehicle
                vehicleToDamage = weaponPlatform.OwnerShip?.GetComponent<VehicleBase>();
            }
            else
            {
                // For other vehicles, damage directly
                vehicleToDamage = target;
            }

            // Only damage if we haven't already damaged this parent
            if (vehicleToDamage != null && !_damagedParents.Contains(vehicleToDamage))
            {
                vehicleToDamage.TakeDamage(Damage, DamageType);
                _damagedParents.Add(vehicleToDamage);
            }

            // if (ownerShip != null && ownerShip.ShieldPoints > 0)
            // {
            //     Vector3 targetPos = ownerShip.CachedTransform.position;
            //     Vector3 dir = (targetPos - impactPoint).normalized;

            //     float targetRadius = GetTargetRadius(ownerShip);
            //     float rayStartOffset = targetRadius * 2f;
            //     float rayDistance = targetRadius * 3f;

            //     Vector3 rayStart = targetPos - dir * rayStartOffset;

            //     if (Physics.Raycast(rayStart, dir, out _hit, rayDistance, LayerMask.GetMask("Shield")))
            //     {
            //         ShieldHitEffect shieldEffect = _hit.collider.GetComponent<ShieldHitEffect>();
            //         if (shieldEffect != null)
            //             shieldEffect.GetHit(_hit);
            //     }
            // }
        }

        if (ExplodeOnImpact && _explodeFXPrefab != null)
        {
                VisualEffect vfx = VFXPool.Instance.Get(_explodeFXPrefab, impactPoint, _cachedTransform.rotation);
                if (vfx != null)
                {
                    VFXPooledInstance pooled = vfx.GetComponent<VFXPooledInstance>();
                    if (pooled == null)
                    {
                        pooled = vfx.gameObject.AddComponent<VFXPooledInstance>();
                        pooled.Initialize(_explodeFXPrefab);
                    }
                    else
                        pooled.spawnTime = Time.time;
                }
        }
        else if (_impactFXPrefab != null)
        {
                VisualEffect vfx = VFXPool.Instance.Get(_impactFXPrefab, impactPoint, _cachedTransform.rotation);
                if (vfx != null)
                {
                    VFXPooledInstance pooled = vfx.GetComponent<VFXPooledInstance>();
                    if (pooled == null)
                    {
                        pooled = vfx.gameObject.AddComponent<VFXPooledInstance>();
                        pooled.Initialize(_impactFXPrefab);
                    }
                    else
                        pooled.spawnTime = Time.time;
                }
        }

        CleanUpTrails();

        if (BulletPool.Instance != null)
        {
            ResetBullet();
            BulletPool.Instance.Return(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private float GetTargetRadius(VehicleBase target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds.extents.magnitude;
        }

        return target.CachedTransform.localScale.magnitude * 5f;
    }

    private void DestroyBullet()
    {
        if (_impactFXPrefab != null)
        {
                VisualEffect vfx = VFXPool.Instance.Get(_impactFXPrefab, _cachedTransform.position, _cachedTransform.rotation);
                if (vfx != null)
                {
                    VFXPooledInstance pooled = vfx.GetComponent<VFXPooledInstance>();
                    if (pooled == null)
                    {
                        pooled = vfx.gameObject.AddComponent<VFXPooledInstance>();
                        pooled.Initialize(_impactFXPrefab);
                    }
                    else
                        pooled.spawnTime = Time.time;
                }
        }

        CleanUpTrails();
        ResetBullet();

        if (BulletPool.Instance != null)
            BulletPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    public void ResetBullet()
    {
        _lifeTimer = 0f;
        _velocity = Vector3.zero;
        _inheritedVelocity = Vector3.zero;
        _initialized = false;
    }

    /// <summary>Adds an external velocity impulse (e.g. from black hole gravity). Modifies the bullet's velocity directly.</summary>
    public void AddExternalVelocity(Vector3 deltaV) { _velocity += deltaV; }

    /// <summary>Silently consumes this bullet (event horizon). Returns it to pool without spawning any VFX.</summary>
    public void ConsumeBullet()
    {
        CleanUpTrails();
        ResetBullet(); // also calls UnregisterBullet
        if (BulletPool.Instance != null)
            BulletPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    private void CleanUpTrails()
    {
        for (int i = 0; i < _childTrails.Count; i++)
        {
            TrailRenderer trail = _childTrails[i];
            if (trail == null) continue;

            trail.emitting = false;
            trail.autodestruct = true;
            trail.transform.SetParent(null);
        }
    }

    public Vector3 Velocity => _velocity;
}