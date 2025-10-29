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
                RaycastHit hit;
                Physics.Raycast(transform.position + transform.forward, -transform.forward, out hit);
                if(hit.collider != null)
                {
                    enemy.GetComponentInChildren<ShieldHitEffect>().GetHit(hit);
                }

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