using UnityEngine;


public class MissileTurret : WeaponBase
{
    private AALauncher _launcher;
    private Transform _launchTransform;

    public float FireInterval = 2.0f;
    public bool IsFiring = false;

    private float _fireTimer = 0.0f;

    void Start()
    {
        _launcher = GetComponentInChildren<AALauncher>();
        _launchTransform = _launcher.GetComponent<Transform>();
        InvokeRepeating("UpdateTarget", 0f, 1.0f / UpdateRate);
    }

    void Update()
    {
        if (!IsAimed)
            IsFiring = false;
        else
            IsFiring = true;

        if (IsFiring && Targeted != null)
        {
            _fireTimer += Time.deltaTime;
            if (_fireTimer < FireInterval)
                return;
            _fireTimer = 0.0f;
            
            Vector3 directionToTarget = (Targeted.position - _launcher.transform.position).normalized;
            float angleToTarget = Vector3.Angle(_launcher.transform.forward, directionToTarget);
            if (angleToTarget > _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone / 2f)
            {
                return;
            }

            if (_launcher.missilePrefabToLaunch.GetComponent<AAMissile>().ActiveRange < Vector3.Distance(transform.position, Targeted.position))
            {
                return;
            }
            _launcher.Launch(Targeted);
        }

        if (IsIdle || Targeted == null)
        {
            if (!IsTurretAtRest)
                RotateTurretToIdle();
            IsAimed = false;
        }
        else
        {
            Vector3 aimPosition = Targeted.position;
            RotateBaseToFaceTarget(aimPosition);

            if (HasBarrels)
                RotateBarrelsToFaceTarget(aimPosition);

            AngleToTarget = GetTurretAngleToTarget(aimPosition);

            // Turret is considered "aimed" when it's pointed at the target.
            IsAimed = AngleToTarget < AimedThreshold;

            IsBarrelAtRest = false;
            IsBaseAtRest = false;
        }
    }

    public void UpdateTarget()
    {
        Targeted = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(GlobalHelper.FactionNames[(int)FireTarget]);

        if (enemies.Length == 0)
        {
            Targeted = null;
            IsAimed = false;
            return;
        }

        float shortest_distance = Mathf.Infinity;
        GameObject nearest_enemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distance_to_enemy = Vector3.Distance(transform.position, enemy.transform.position);
            Vector2 anglesToEnemy = CalcuateRelativeAngles(enemy.transform);
            if (anglesToEnemy.y > MaxElevation || anglesToEnemy.y < -MaxDepression)
            {
                continue;
            }

            if (HasLimitedTraverse)
            {
                if (anglesToEnemy.x > RightLimit || anglesToEnemy.x < -LeftLimit)
                {
                    continue;
                }
            }

            if (distance_to_enemy < ActiveRange.y && distance_to_enemy < shortest_distance)
            {
                shortest_distance = distance_to_enemy;
                nearest_enemy = enemy;
            }
        }

        if (nearest_enemy != null)
        {
            Targeted = nearest_enemy.transform;
        }
        else
        {
            IsAimed = false;
            Targeted = null;
        }
    }
}
