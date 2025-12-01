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

    // Reusable lists - static to share across all bullets
    private static List<VehicleBase> _nearbyEnemies = new List<VehicleBase>(64);
    private static List<VehicleBase> _enemiesToDamage = new List<VehicleBase>(16);

    void Awake()
    {
        _cachedTransform = transform;
        _fuseDistSqr = FuseDetonationDistance * FuseDetonationDistance;
        _explosionRadiusSqr = ExplosionRadius * ExplosionRadius;
    }

    void Update()
    {
        // Move bullet
        _cachedTransform.Translate(Vector3.forward * Speed * Time.deltaTime);

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
        CombatRegistry.GetNearbyEnemies(myPosition, ExplosionRadius, FireTarget, _nearbyEnemies);

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
            if (enemy == null) continue;

            // Shield hit effect
            if (enemy.ShieldPoints > 0)
            {
                Vector3 dir = (enemy.transform.position - impactPoint).normalized;
                if (Physics.Raycast(impactPoint - 10f * dir, dir, out _hit))
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

    private void DestroyBullet()
    {
        if (_impactFXPrefab != null)
            Instantiate(_impactFXPrefab, _cachedTransform.position, _cachedTransform.rotation).Play();

        CleanUpTrails();
        // Return to pool instead of destroying
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
}