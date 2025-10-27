using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissilePhysics : MonoBehaviour
{
    public int Velocity = 10;
    Transform _target;
    public int Damage = 100;
    public int ExplodeRadius = 5;
    public float LifeTime = 5f;
    public int DetonationRadius = 3;

    // public GameObject impact_effect;

    // Start is called before the first frame update
    void Start()
    {
        Destroy(gameObject, LifeTime);
    }

    public void Seek(Transform target)
    {
        _target = target;
    }
    private void Update()
    {
        if (!_target)
        {
            // Lock another target or self-destruct
            UpdateTarget();
            if (!_target)
            {
                Destroy(gameObject);
                return;
            }
        }

        Vector3 dir = _target.position - transform.position;
        float distance = Velocity * Time.deltaTime;

        if (dir.magnitude <= DetonationRadius)
        {
            HitTarget();
            Debug.Log("Missile hit target: " + _target.name);
            return;
        }

        // transform.Translate(dir.normalized * distance, Space.World);
        // The previous line results in a very straight movement, so we use MoveTowards for smoother motion
        transform.position = Vector3.MoveTowards(transform.position, _target.position, distance);

        transform.LookAt(_target);
    }

    public void UpdateTarget()
    {
        if (_target != null)
        {
            return;
        }

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Foe");
        float shortest_distance = Mathf.Infinity;
        GameObject nearest_enemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance_to_enemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance_to_enemy < shortest_distance)
            {
                shortest_distance = distance_to_enemy;
                nearest_enemy = enemy;
            }
        }

        if (nearest_enemy)
        {
            _target = nearest_enemy.transform;
        }
        else
        {
            _target = null;
        }
    }

    void HitTarget()
    {
        if (ExplodeRadius > 0)  // Area damage
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, ExplodeRadius);

            foreach (Collider collider in colliders)
            {
                if (collider.tag == "Foe")
                {
                    // Damage reduce if farther from explosion center
                    // float distance = Vector3.Distance(transform.position, collider.transform.position);
                    // int damage = Damage * (int)(1 - distance / ExplodeRadius);
                    // damage = Mathf.Max(damage, 0);
                    collider.GetComponent<EnemyVehicle>().TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
                }
            }

        }
        else    // Direct hit
        {
            _target.GetComponent<EnemyVehicle>().TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
        }
        Destroy(this.gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DetonationRadius);
    }
}
