using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletPhysics : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("Effect played when the bullet impacts something.")]
    [SerializeField] private ParticleSystem _impactFXPrefab = null;
    [Tooltip("Effect played when the bullet explodes.")]
    [SerializeField] private ParticleSystem _explodeFXPrefab = null;
    [Tooltip("Any trails listed here will be cleaned up nicely on the bullet's destruction. " +
            "Used to prevent unsightly deleted trails.")]
    [SerializeField] private List<TrailRenderer> _childTrails = new List<TrailRenderer>();

    public int Damage = 2;
    [Header("Motion")]
    [Tooltip("Speed (m/s) that the bullet travels.")]
    public float Speed = 50f;
    [Tooltip("How long (seconds) the bullet lasts")]
    public float LifeTime = 5f;
    private float lifeTimer;
    public int FuseDetonationDistance = 1;
    public int ExplosionRadius = 5;

    public GlobalHelper.AmmoType DamageType = GlobalHelper.AmmoType.Kinetic;
    public GlobalHelper.Faction FireTarget = GlobalHelper.Faction.Foe;

    [Header("Explosions")]
    public bool ExplodeOnImpact = false;

    public Transform TargetObject;
    private RaycastHit _hit;



    void Update()
    {
        // FindClosestTarget();
        transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= LifeTime)
        {
            DestroyBulletFromImpact(transform.position, transform.rotation);
        }
        UpdateBullet();
    }

    public void DestroyBulletFromImpact(Vector3 impactedPoint, Quaternion impactRotation, List<GameObject> enemiesToBeDamaged = null)
    {
        if (enemiesToBeDamaged != null)
        {
            foreach (var enemy in enemiesToBeDamaged)
            {
                Vector3 dir = (enemy.transform.position - transform.position).normalized;
                Physics.Raycast(transform.position - 10 * dir, dir, out _hit);
                if (_hit.collider != null)
                {
                    if (_hit.collider.GetComponent<ShieldHitEffect>())
                    {
                        _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
                    }
                }
                enemy.GetComponent<VehicleBase>().TakeDamage(Damage, DamageType);
            }
        }

        if (!ExplodeOnImpact && _impactFXPrefab != null)
            Instantiate(_impactFXPrefab, impactedPoint, impactRotation).Play();
        else
            Instantiate(_explodeFXPrefab, impactedPoint, impactRotation).Play();

        CleanUpTrails();
        Destroy(gameObject);
    }

    private void CleanUpTrails()
    {
        foreach (var trail in _childTrails)
        {
            trail.emitting = false;
            trail.autodestruct = true;
            trail.transform.SetParent(null);
        }
    }

    float _minDist = float.MaxValue;

    void UpdateBullet()
    {
        // if (TargetObject != null && Vector3.Distance(transform.position, TargetObject.position) <= FuseDetonationDistance)
        // {
        //     TargetObject.gameObject.GetComponent<VehicleBase>().TakeDamage(Damage, DamageType);
        //     Vector3 dir = (TargetObject.position - transform.position).normalized;
        //     Physics.Raycast(transform.position - 10 * dir, dir, out _hit);
        //     if (_hit.collider != null)
        //     {
        //         if (_hit.collider.GetComponent<ShieldHitEffect>())
        //         {
        //             _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
        //         }
        //     }
        //     DestroyBulletFromImpact(transform.position, transform.rotation);
        // }
        // else
        // {
        // Find if any enemy is within FuseDetonationDistance, this should be done more efficiently with spatial partitioning or call from a manager
        // I plan to place it outside of this scope later.
        // Should be only called when enemy number is changed, i.e. enemy spawned or destroyed.
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);
        List<GameObject> enemiesToBeDamaged = new List<GameObject>();
        
        foreach (var enemy in enemies)
        {
            float distToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distToEnemy <= ExplosionRadius)
            {
                enemiesToBeDamaged.Add(enemy);
            }
            if(distToEnemy < _minDist)
            {
                _minDist = distToEnemy;
            }
        }
        if (_minDist < FuseDetonationDistance)
        {
            DestroyBulletFromImpact(transform.position, transform.rotation, enemiesToBeDamaged);
        }
        // }
    }

    // private void OnCollisionEnter(Collision other)
    // {
    //     if (other.gameObject.tag == "Foe")
    //     {
    //         other.gameObject.GetComponent<EnemyVehicle>().TakeDamage(Damage, DamageType);
    //         var contactPoint = other.contacts[0].point;
    //         Vector3 dir = (other.transform.position - transform.position).normalized;
    //         Physics.Raycast(transform.position, dir, out _hit);
    //         if (_hit.collider != null)
    //         {
    //             if (_hit.collider.GetComponent<ShieldHitEffect>())
    //                 _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
    //         }
    //         DestroyBulletFromImpact(contactPoint, transform.rotation);
    //     }
    // }

    public void FindClosestTarget()
    {
        // enemyVehicles = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None);
        // foreach (EnemyVehicle enemy in enemyVehicles)
        // {
        //     if (enemy == null)
        //     {
        //         break;
        //     }
        //     float distance = Vector3.Distance(transform.position, enemy.transform.position);

        //     if (distance < FuseDetonationDistance)
        //     {
        //         Vector3 dir = (enemy.transform.position - transform.position).normalized;
        //         Physics.Raycast(transform.position - dir * 10, dir, out _hit);
        //         if (_hit.collider != null && Vector3.Distance(_hit.point, transform.position) <= FuseDetonationDistance + 0.1f)
        //         {
        //             if (_hit.collider.GetComponent<ShieldHitEffect>())
        //                 _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
        //         }

        //         var isEnemyDestroyed = enemy.TakeDamage(Damage, DamageType);
        //         if (isEnemyDestroyed)
        //         {
        //             DestroyBulletFromImpact(transform.position, transform.rotation);
        //             break;
        //         }
        //         DestroyBulletFromImpact(transform.position, transform.rotation);
        //     }
        // }

        // Search for enemies within FuseDetonationDistance
        // Collider[] hitColliders = Physics.OverlapSphere(transform.position, FuseDetonationDistance);
        // foreach (var hitCollider in hitColliders)
        // {
        //     EnemyVehicle enemy = hitCollider.GetComponent<EnemyVehicle>();
        //     if (enemy != null)
        //     {
        //         enemy.TakeDamage(Damage, DamageType);
        //         Destroy(this.gameObject);
        //     }
        // }
    }
}