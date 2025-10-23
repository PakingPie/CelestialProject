using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BulletPhysics : MonoBehaviour
{
    public float Speed = 50f;
    public float LifeTime = 5f;
    private float lifeTimer;
    public int FuseDetonationDistance = 1;
    public void FindClosestTarget()
    {
        // EnemyVehicle[] enemies = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None);
        // float closestDistance = FuseDetonationDistance;

        // foreach (EnemyVehicle enemy in enemies)
        // {
        //     float distance = Vector3.Distance(transform.position, enemy.transform.position);
        //     if (distance < closestDistance)
        //     {
        //         enemy.TakeDamage(1, GlobalHelper.AmmoType.Kinetic);
        //     }
        // }

        // Search for enemies within FuseDetonationDistance
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, FuseDetonationDistance);
        foreach (var hitCollider in hitColliders)
        {
            EnemyVehicle enemy = hitCollider.GetComponent<EnemyVehicle>();
            if (enemy != null)
            {
                enemy.TakeDamage(1, GlobalHelper.AmmoType.Kinetic);
                Destroy(this.gameObject);
            }
        }
    }

    void Update()
    {
        transform.Translate(Vector3.forward * Speed * Time.deltaTime);
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= LifeTime)
        {
            Destroy(gameObject);
        }
    }


}