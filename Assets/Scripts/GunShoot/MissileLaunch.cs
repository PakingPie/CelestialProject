using UnityEngine;

public class MissileLaunch : MonoBehaviour
{
    [Header("Missile Settings")]
    [Tooltip("Prefab of the missile to be launched.")]
    public GameObject missilePrefab;

    [Tooltip("Number of updates per second for the gun targeting system.")]
    public int UpdateRate = 60;

    [Tooltip("The range within which the gun can target enemies.")]
    public Vector2 ActiveRange = new Vector2(5f, 300f);
    public Transform MissileSpawnPoint;
    public float LaunchInterval = 3f;
    public float TurretRotateSpeed = 5f;

    private Transform _targetPoint;

    void Start()
    {
        InvokeRepeating("LaunchMissile", 0.0f, LaunchInterval);
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
        InvokeRepeating("LockOn", 0f, 1.0f / UpdateRate);
    }
    
    void LockOn()
    {
        if (_targetPoint == null)
        {
            return;
        }
        Vector3 dir = _targetPoint.position - transform.position;
        Quaternion look_rotation = Quaternion.LookRotation(dir);
        Vector3 rotation = Quaternion.Lerp(transform.rotation, look_rotation, Time.deltaTime * TurretRotateSpeed).eulerAngles;
        transform.rotation = Quaternion.Euler(rotation);
    }

    void LaunchMissile()
    {
        if (_targetPoint != null || Vector3.Distance(transform.position, _targetPoint.position) < ActiveRange.y)
        {
            GameObject fired_object = Instantiate(missilePrefab, MissileSpawnPoint.position, MissileSpawnPoint.rotation);
            fired_object.GetComponent<MissilePhysics>().Seek(_targetPoint);
        }
    }

    public void UpdateTarget()
    {
        if (_targetPoint != null)
        {
            if (Vector3.Distance(transform.position, _targetPoint.position) > ActiveRange.y)
            {
                _targetPoint = null;
            }
            else
            {
                return;
            }
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

        if (nearest_enemy && shortest_distance <= ActiveRange.y)
        {
            _targetPoint = nearest_enemy.transform;
        }
        else
        {
            _targetPoint = null;
        }
    }
}