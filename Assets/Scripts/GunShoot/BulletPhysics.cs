using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletPhysics : MonoBehaviour
{
    public int Damage = 2;
    public float Speed = 50f;
    public float LifeTime = 5f;
    private float lifeTimer;
    public int FuseDetonationDistance = 1;

    public GlobalHelper.AmmoType DamageType = GlobalHelper.AmmoType.Kinetic;

    internal EnemyVehicle[] enemyVehicles;
    private RaycastHit _hit;
    void Start()
    {
    }
    public void FindClosestTarget()
    {
        enemyVehicles = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None);
        foreach (EnemyVehicle enemy in enemyVehicles)
        {
            if (enemy == null)
            {
                break;
            }
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            
            if (distance < FuseDetonationDistance)
            {
                var isEnemyDestroyed = enemy.TakeDamage(Damage, DamageType);
                if (isEnemyDestroyed)
                {
                    Destroy(this.gameObject, 0.01f);
                    break;
                }
                Destroy(this.gameObject);
            }
        }

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

    void Update()
    {
        FindClosestTarget();
        transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= LifeTime)
        {
            Destroy(gameObject);
        }

        
        Physics.Raycast(transform.position, transform.forward, out _hit);
        if(_hit.collider != null && Vector3.Distance(_hit.point, transform.position) <= FuseDetonationDistance + 0.1f)
        {
            if(_hit.collider.GetComponent<ShieldHitEffect>())
            _hit.collider.GetComponent<ShieldHitEffect>().GetHit(_hit);
        }
    }

    // private void OnCollisionEnter(Collision other)
    // {
    //     Collider[] colliders = Physics.OverlapSphere(transform.position, FuseDetonationDistance);
    //     for(int i = 0; i < colliders.Length; i++)
    //     {
    //       EnemyVehicle enemy = colliders[i].GetComponent<EnemyVehicle>();
    //         if (enemy != null)
    //         {
    //             enemy.TakeDamage(1, GlobalHelper.AmmoType.Kinetic);
    //             Destroy(this.gameObject);
    //         }
    //     }
    // }
}