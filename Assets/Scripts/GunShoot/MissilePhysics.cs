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
            return;
        }
        Vector3 dir = _target.position - transform.position;
        float distance = Velocity * Time.deltaTime;

        if (dir.magnitude <= DetonationRadius)
        {
            HitTarget();
            Debug.Log("Missile hit target: " + _target.name);
            return;
        }

        transform.Translate(dir.normalized * distance, Space.World);
        transform.LookAt(_target);
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
}
