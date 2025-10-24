using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissilePhysics : MonoBehaviour
{
    public int Velocity = 100;
    Transform _target;
    public int Damage = 10;
    public int ExplodeRadius = 5;
    // public GameObject impact_effect;

    // Start is called before the first frame update
    void Start()
    {
    }

    public void seek(Transform target)
    {
        _target = target;
    }
    private void Update()
    {
        if (!_target)
        {
            Destroy(this.gameObject);
            return;
        }
        Vector3 dir = _target.position - transform.position;
        float distance = Velocity * Time.deltaTime;

        if (dir.magnitude <= distance)
        {
            hitTarget();
            return;
        }

        transform.Translate(dir.normalized * distance, Space.World);
        transform.LookAt(_target);
    }

    void hitTarget()
    {
        // GameObject effect = Instantiate(impact_effect, transform.position, transform.rotation);
        // Destroy(effect.gameObject, 2f);
        // audio_manager.PlayAudio(explosion_sound);
        Destroy(this.gameObject);
        // _target.GetComponent<EnemyMovement>().takenDamage(Damage);
    }

    void Explode()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, ExplodeRadius);

        foreach(Collider collider in colliders)
        {
            if(collider.tag == "Foe")
            {
                collider.GetComponent<EnemyVehicle>().TakeDamage(Damage, GlobalHelper.AmmoType.Explosive);
            }
        }
    }
}
