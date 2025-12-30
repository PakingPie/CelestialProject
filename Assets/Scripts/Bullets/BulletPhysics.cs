using UnityEngine;
using System.Collections.Generic;

public class BulletPhysics : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private ParticleSystem _impactFXPrefab = null;
    [SerializeField] private ParticleSystem _explodeFXPrefab = null;
    [SerializeField] private List<TrailRenderer> _childTrails = new List<TrailRenderer>();

    public int Damage = 2;

    [Header("Motion")]
    public float Speed = 50f;
    public float LifeTime = 5f;
    public int FuseDetonationDistance = 1;
    public int ExplosionRadius = 5;

    [Header("Velocity Inheritance")]
    [Tooltip("How much of the shooter's velocity is inherited (0 = none, 1 = full)")]
    [Range(0f, 1f)]
    public float velocityInheritance = 1f;

    [Tooltip("Gradually align bullet direction to velocity over time")]
    public bool alignToVelocity = false;

    [Tooltip("How quickly bullet aligns to its velocity direction")]
    public float alignmentSpeed = 5f;

    public GlobalHelper.AmmoType DamageType = GlobalHelper.AmmoType.Kinetic;
    public GlobalHelper.Faction FireTarget = GlobalHelper.Faction.Foe;

    [Header("Explosions")]
    public bool ExplodeOnImpact = false;

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

    // Reusable lists - static to share across all bullets
    private static List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    private static List<VehicleBase> _enemiesToDamage = new List<VehicleBase>(16);

    void Awake()
    {
        _cachedTransform = transform;
        _fuseDistSqr = FuseDetonationDistance * FuseDetonationDistance;
        _explosionRadiusSqr = ExplosionRadius * ExplosionRadius;
    }

    /// <summary>
    /// Initialize bullet with shooter's velocity. Call this after instantiation.
    /// </summary>
    /// <param name="shooterVelocity">The velocity of the ship/turret that fired this bullet</param>
    public void Initialize(Vector3 shooterVelocity)
    {
        _inheritedVelocity = shooterVelocity * velocityInheritance;
        _velocity = _cachedTransform.forward * Speed + _inheritedVelocity;
        _initialized = true;
    }

    /// <summary>
    /// Initialize with explicit direction and shooter velocity
    /// </summary>
    /// <param name="direction">Direction the bullet should travel</param>
    /// <param name="shooterVelocity">The velocity of the ship/turret that fired this bullet</param>
    public void Initialize(Vector3 direction, Vector3 shooterVelocity)
    {
        _cachedTransform.forward = direction.normalized;
        _inheritedVelocity = shooterVelocity * velocityInheritance;
        _velocity = direction.normalized * Speed + _inheritedVelocity;
        _initialized = true;
    }

    void Update()
    {
        // Fallback if Initialize wasn't called
        if (!_initialized)
        {
            _velocity = _cachedTransform.forward * Speed;
            _initialized = true;
        }

        // Move bullet using velocity
        _cachedTransform.position += _velocity * Time.deltaTime;

        // Optionally align bullet rotation to velocity direction
        if (alignToVelocity && _velocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_velocity.normalized);
            _cachedTransform.rotation = Quaternion.Slerp(
                _cachedTransform.rotation,
                targetRotation,
                alignmentSpeed * Time.deltaTime
            );
        }

        // Check lifetime
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= LifeTime)
        {
            DestroyBullet();
            return;
        }

        // Check for enemies
        CheckProximityDetonation();
    }

    private void CheckProximityDetonation()
    {
        Vector3 myPosition = _cachedTransform.position;

        // Use spatial partitioning - only check nearby enemies
        CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, FireTarget, _nearbyEnemies, true);

        if (_nearbyEnemies.Count == 0)
            return;

        float minDistSqr = float.MaxValue;
        _enemiesToDamage.Clear();

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];
            if (enemy == null) continue;

            Vector3 enemyPos = enemy.transform.position;
            float distSqr = (enemyPos - myPosition).sqrMagnitude;

            // Within explosion radius?
            if (distSqr <= _explosionRadiusSqr)
                _enemiesToDamage.Add(enemy);

            // Track closest
            if (distSqr < minDistSqr)
                minDistSqr = distSqr;
        }

        // Detonate if closest enemy within fuse distance
        if (minDistSqr <= _fuseDistSqr)
        {
            DestroyBulletWithDamage(myPosition, _enemiesToDamage);
        }
    }

    private void DestroyBulletWithDamage(Vector3 impactPoint, List<VehicleBase> enemies)
    {
        // Apply damage
        for (int i = 0; i < enemies.Count; i++)
        {
            VehicleBase enemy = enemies[i];
            VehicleBase ownerShip = enemy.OwnerShip.GetComponent<VehicleBase>();
            
            if (enemy == null) continue;

            // Shield hit effect
            if (ownerShip.ShieldPoints > 0)
            {
                Vector3 enemyPos = ownerShip.transform.position;
                Vector3 dir = (enemyPos - impactPoint).normalized;

                // Calculate proper raycast distance based on enemy size
                float enemyRadius = GetEnemyRadius(ownerShip);
                float rayStartOffset = enemyRadius * 2f;
                float rayDistance = enemyRadius * 3f;

                Vector3 rayStart = enemyPos - dir * rayStartOffset;

                if (Physics.Raycast(rayStart, dir, out _hit, rayDistance, LayerMask.GetMask("Shield")))
                {
                    ShieldHitEffect shieldEffect = _hit.collider.GetComponent<ShieldHitEffect>();
                    if (shieldEffect != null)
                        shieldEffect.GetHit(_hit);
                }
            }

            enemy.TakeDamage(Damage, DamageType);
        }

        // Spawn FX
        if (ExplodeOnImpact && _explodeFXPrefab != null)
            Instantiate(_explodeFXPrefab, impactPoint, _cachedTransform.rotation).Play();
        else if (_impactFXPrefab != null)
            Instantiate(_impactFXPrefab, impactPoint, _cachedTransform.rotation).Play();

        CleanUpTrails();
        Destroy(gameObject);
    }

    private float GetEnemyRadius(VehicleBase enemy)
    {
        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds.extents.magnitude;
        }

        return enemy.transform.localScale.magnitude * 5f;
    }

    private void DestroyBullet()
    {
        if (_impactFXPrefab != null)
            Instantiate(_impactFXPrefab, _cachedTransform.position, _cachedTransform.rotation).Play();

        CleanUpTrails();

        // Reset state for pooling
        ResetBullet();

        if (BulletPool.Instance != null)
            BulletPool.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Reset bullet state for object pooling
    /// </summary>
    public void ResetBullet()
    {
        _lifeTimer = 0f;
        _velocity = Vector3.zero;
        _inheritedVelocity = Vector3.zero;
        _initialized = false;
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

    /// <summary>
    /// Current bullet velocity (for external systems)
    /// </summary>
    public Vector3 Velocity => _velocity;
}