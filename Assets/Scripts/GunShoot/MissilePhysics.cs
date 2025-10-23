using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MissilePhysics : MonoBehaviour
{
    public int speed = 100;
    Transform target;
    public int damage = 10;
    // public GameObject impact_effect;

    // Start is called before the first frame update
    void Start()
    {
    }

    public void seek(Transform _target)
    {
        target = _target;
    }
    private void Update()
    {
        if (!target)
        {
            Destroy(this.gameObject);
            return;
        }
        Vector3 dir = target.position - transform.position;
        float distance = speed * Time.deltaTime;

        if (dir.magnitude <= distance)
        {
            hitTarget();
            return;
        }

        transform.Translate(dir.normalized * distance, Space.World);
        

    }

    void hitTarget()
    {
        // GameObject effect = Instantiate(impact_effect, transform.position, transform.rotation);
        // Destroy(effect.gameObject, 2f);
        // audio_manager.PlayAudio(explosion_sound);
        Destroy(this.gameObject);
        // target.GetComponent<EnemyMovement>().takenDamage(damage);
    }
}
