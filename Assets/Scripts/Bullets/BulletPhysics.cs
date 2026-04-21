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
    [HideInInspector] public float Speed;
    public float LifeTime = 5f;
    [Range(0f, 0.3f)]
    [Tooltip("Random range variance per bullet (0.1 = ±10%).")]
    public float RangeVariance = 0.1f;
    public int FuseDetonationDistance = 1;
    public int ExplosionRadius = 5;

    [Header("Velocity Inheritance")]
    [Range(0f, 1f)]
    public float velocityInheritance = 1f;
    public bool alignToVelocity = false;
    public float alignmentSpeed = 5f;

    public AmmoType DamageType = AmmoType.Kinetic;
    [Tooltip("Source-owned damage multipliers applied before the target processes ammo-type defense rules.")]
    public DamageProfile ImpactDamageProfile = new DamageProfile();
    public Faction FireTarget = Faction.Foe;

    [Header("Explosions")]
    public bool ExplodeOnImpact = false;

    [Header("Performance")]
    [Tooltip("Use CombatRegistry spatial grid for faster queries.")]
    public bool UseSpatialGrid = true;
    [Tooltip("Only check for hits every N frames. 1 = every frame.")]
    [Min(1)] public int HitCheckIntervalFrames = 2;

    // Cached
    private Transform _cachedTransform;
    private float _lifeTimer;
    private float _randomizedLifeTime;
    private float _fuseDistSqr;
    private float _explosionRadiusSqr;

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

    // Maximum expected bounds radius for query range padding
    private const float MAX_BOUNDS_PADDING = 50f;

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
        _randomizedLifeTime = LifeTime * Random.Range(1f - RangeVariance, 1f + RangeVariance);
        _initialized = true;
    }

    public void Initialize(Vector3 direction, Vector3 shooterVelocity)
    {
        _cachedTransform.forward = direction.normalized;
        _inheritedVelocity = shooterVelocity * velocityInheritance;
        _velocity = direction.normalized * Speed + _inheritedVelocity;
        _previousPosition = _cachedTransform.position;
        _randomizedLifeTime = LifeTime * Random.Range(1f - RangeVariance, 1f + RangeVariance);
        _initialized = true;
    }

    void Update()
    {
        if (!_initialized)
        {
            _velocity = _cachedTransform.forward * Speed;
            _previousPosition = _cachedTransform.position;
            _randomizedLifeTime = LifeTime * Random.Range(1f - RangeVariance, 1f + RangeVariance);
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

        // Lifetime check — primary destruction trigger
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _randomizedLifeTime)
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
        Vector3 travelDir = myPosition - _previousPosition;
        float travelDist = travelDir.magnitude;

        // Query from midpoint of travel segment, with range covering both endpoints + padding
        Vector3 queryCenter = (myPosition + _previousPosition) * 0.5f;
        float queryRange = travelDist * 0.5f + ExplosionRadius + MAX_BOUNDS_PADDING;

        // Get targets based on FireTarget faction (padded range to catch large ships)
        if (UseSpatialGrid)
            CombatRegistry.FindEnemiesInRange(queryCenter, queryRange, FireTarget, _nearbyTargets);
        else
            CombatRegistry.GetNearbyEnemies(queryCenter, queryRange, FireTarget, _nearbyTargets, true);

        // Also check neutrals if we can damage asteroids
        if (CanDamageAsteroids)
        {
            if (UseSpatialGrid)
                CombatRegistry.FindEnemiesInRange(queryCenter, queryRange, Faction.Neutral, _nearbyNeutrals);
            else
                CombatRegistry.GetNearbyEnemies(queryCenter, queryRange, Faction.Neutral, _nearbyNeutrals, true);

            if (_nearbyNeutrals.Count > 0)
                _nearbyTargets.AddRange(_nearbyNeutrals);
        }

        // Swept collision: raycast along travel path to find first hit
        Vector3 sweepImpact = myPosition;
        bool fuseTriggered = false;

        if (travelDist > 0.001f)
        {
            Vector3 sweepDir = travelDir / travelDist; // normalized
            float bestHitDist = float.MaxValue;

            for (int i = 0; i < _nearbyTargets.Count; i++)
            {
                VehicleBase target = _nearbyTargets[i];
                if (target == null) continue;

                // Raycast the travel segment against target's OBB
                if (target.RaycastBounds(_previousPosition, sweepDir, out Vector3 hitPt, out float hitDist))
                {
                    if (hitDist <= travelDist + FuseDetonationDistance && hitDist < bestHitDist)
                    {
                        bestHitDist = hitDist;
                        sweepImpact = hitPt;
                        fuseTriggered = true;
                    }
                }
            }
        }

        // Fallback: also check current position proximity (for slow bullets or targets overlapping)
        if (!fuseTriggered)
        {
            for (int i = 0; i < _nearbyTargets.Count; i++)
            {
                VehicleBase target = _nearbyTargets[i];
                if (target == null) continue;

                float distSqr = target.SqrDistanceToBounds(myPosition);
                if (distSqr <= _fuseDistSqr)
                {
                    sweepImpact = target.ClosestBoundsPoint(myPosition);
                    fuseTriggered = true;
                    break;
                }
            }
        }

        if (fuseTriggered)
        {
            // Collect all targets in explosion radius from the impact point
            _targetsInExplosion.Clear();
            for (int i = 0; i < _nearbyTargets.Count; i++)
            {
                VehicleBase target = _nearbyTargets[i];
                if (target == null) continue;

                float distSqr = target.SqrDistanceToBounds(sweepImpact);
                if (distSqr <= _explosionRadiusSqr)
                {
                    _targetsInExplosion.Add(target);
                }
            }

            DestroyBulletWithDamage(sweepImpact, _targetsInExplosion);
        }
    }

    private void DestroyBulletWithDamage(Vector3 impactPoint, List<VehicleBase> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            VehicleBase target = targets[i];
            if (target == null) continue;

            // Use bounds-aware impact: find closest surface point on target for VFX and damage routing
            target.TakeDamageAtPoint(ImpactDamageProfile.CreateContext(this, Damage, DamageType, target, impactPoint));
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
        _randomizedLifeTime = 0f;
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