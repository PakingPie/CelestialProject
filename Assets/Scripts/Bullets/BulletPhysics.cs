using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

public class BulletPhysics : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private ParticleSystem _impactFXPrefab = null;
    [SerializeField] private ParticleSystem _explodeFXPrefab = null;
    [SerializeField] private List<TrailRenderer> _childTrails = new List<TrailRenderer>();

    public int Damage = 2;

    public bool CanDamagePlanet = false;
    public bool CanDamageAsteroids = true;

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

    public AmmoType DamageType = AmmoType.Kinetic;
    public Faction FireTarget = Faction.Foe;

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
    private Vector3 _previousPosition;

    // Reusable lists
    private static List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    private static List<VehicleBase> _enemiesToDamage = new List<VehicleBase>(16);
    private static List<VehicleBase> _nearbyNeutrals = new List<VehicleBase>(32);

    void Awake()
    {
        _cachedTransform = transform;
        _fuseDistSqr = FuseDetonationDistance * FuseDetonationDistance;
        _explosionRadiusSqr = ExplosionRadius * ExplosionRadius;
    }

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

        // Check for hits - returns true if bullet was destroyed
        if (CheckAllHits())
        {
            return;
        }
    }

    /// <summary>
    /// Check for hits against all valid targets. Returns true if bullet was destroyed.
    /// </summary>
    private bool CheckAllHits()
    {
        Vector3 myPosition = _cachedTransform.position;

        VehicleBase closestTarget = null;
        float closestDistSqr = float.MaxValue;
        bool isEnemy = false;

        // Check enemies
        CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, FireTarget, _nearbyEnemies, true);

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];
            if (enemy == null) continue;

            float distSqr = (enemy.CachedTransform.position - myPosition).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closestTarget = enemy;
                isEnemy = true;
            }
        }

        // Check neutrals (asteroids) if enabled
        if (CanDamageAsteroids)
        {
            CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, Faction.Neutral, _nearbyNeutrals, true);

            for (int i = 0; i < _nearbyNeutrals.Count; i++)
            {
                VehicleBase neutral = _nearbyNeutrals[i];
                if (neutral == null) continue;

                float distSqr = (neutral.CachedTransform.position - myPosition).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closestTarget = neutral;
                    isEnemy = false;
                }
            }
        }

        // Check if closest target is within fuse distance
        if (closestTarget != null && closestDistSqr <= _fuseDistSqr)
        {
            if (isEnemy)
            {
                // Collect all enemies in explosion radius for AoE damage
                _enemiesToDamage.Clear();
                for (int i = 0; i < _nearbyEnemies.Count; i++)
                {
                    VehicleBase enemy = _nearbyEnemies[i];
                    if (enemy == null) continue;

                    float distSqr = (enemy.CachedTransform.position - myPosition).sqrMagnitude;
                    if (distSqr <= _explosionRadiusSqr)
                    {
                        _enemiesToDamage.Add(enemy);
                    }
                }
                DestroyBulletWithDamage(myPosition, _enemiesToDamage);
            }
            else
            {
                // Hit neutral (asteroid)
                HitNeutralTarget(closestTarget, myPosition);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle hitting a neutral target (asteroid)
    /// </summary>
    private void HitNeutralTarget(VehicleBase target, Vector3 impactPoint)
    {
        if (target == null) return;

        Debug.Log($"Bullet hitting neutral target with {Damage} {DamageType} damage");

        // Apply damage to primary target
        target.TakeDamage(Damage, DamageType);

        // Handle explosions
        if (ExplodeOnImpact)
        {
            if (_explodeFXPrefab != null)
            {
                Instantiate(_explodeFXPrefab, impactPoint, Quaternion.identity).Play();
            }

            // Damage nearby neutrals in explosion radius
            DamageNearbyNeutrals(impactPoint);

            // Also damage nearby enemies if explosive
            DamageNearbyEnemies(impactPoint);
        }
        else if (_impactFXPrefab != null)
        {
            Instantiate(_impactFXPrefab, impactPoint, Quaternion.identity).Play();
        }

        // Destroy bullet
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

    /// <summary>
    /// Damage neutrals within explosion radius
    /// </summary>
    private void DamageNearbyNeutrals(Vector3 center)
    {
        if (!CanDamageAsteroids) return;

        CombatRegistry.GetNearbyEnemies(center, ExplosionRadius, Faction.Neutral, _nearbyNeutrals, true);

        for (int i = 0; i < _nearbyNeutrals.Count; i++)
        {
            VehicleBase neutral = _nearbyNeutrals[i];
            if (neutral == null) continue;

            float dist = Vector3.Distance(center, neutral.CachedTransform.position);
            float falloff = 1f - (dist / ExplosionRadius);
            int explosionDamage = Mathf.RoundToInt(Damage * falloff * 2f);

            if (explosionDamage > 0)
            {
                neutral.TakeDamage(explosionDamage, DamageType);
            }
        }
    }

    /// <summary>
    /// Damage enemies within explosion radius
    /// </summary>
    private void DamageNearbyEnemies(Vector3 center)
    {
        CombatRegistry.GetNearbyEnemies(center, ExplosionRadius, FireTarget, _nearbyEnemies, true);

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            VehicleBase enemy = _nearbyEnemies[i];
            if (enemy == null) continue;

            float dist = Vector3.Distance(center, enemy.CachedTransform.position);
            float falloff = 1f - (dist / ExplosionRadius);
            int explosionDamage = Mathf.RoundToInt(Damage * falloff * 2f);

            if (explosionDamage > 0)
            {
                enemy.TakeDamage(explosionDamage, DamageType);
            }
        }
    }

    private void DestroyBulletWithDamage(Vector3 impactPoint, List<VehicleBase> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            VehicleBase enemy = enemies[i];
            if (enemy == null) continue;

            VehicleBase ownerShip = enemy.OwnerShip.GetComponent<VehicleBase>();

            if (ownerShip.ShieldPoints > 0)
            {
                Vector3 enemyPos = ownerShip.CachedTransform.position;
                Vector3 dir = (enemyPos - impactPoint).normalized;

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

        // Also damage nearby neutrals if explosive
        if (ExplodeOnImpact && CanDamageAsteroids)
        {
            DamageNearbyNeutrals(impactPoint);
        }

        if (ExplodeOnImpact && _explodeFXPrefab != null)
            Instantiate(_explodeFXPrefab, impactPoint, _cachedTransform.rotation).Play();
        else if (_impactFXPrefab != null)
            Instantiate(_impactFXPrefab, impactPoint, _cachedTransform.rotation).Play();

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

        return enemy.CachedTransform.localScale.magnitude * 5f;
    }

    private void DestroyBullet()
    {
        if (_impactFXPrefab != null)
            Instantiate(_impactFXPrefab, _cachedTransform.position, _cachedTransform.rotation).Play();

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