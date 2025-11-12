using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


public class AAFire : MonoBehaviour
{
    public enum FireMode
    {
        Manual,
        Automatic
    };
    public FireMode _fireMode = FireMode.Automatic;
    public Vector2 ActiveRange = new Vector2(5f, 300f);
    private AALauncher[] _allLaunchers;
    private Transform _targetPoint;
    public float FireInterval = 1.0f;
    private float _fireTimer = 0f;

    private void Start()
    {
        _allLaunchers = GetComponentsInChildren<AALauncher>();
        InvokeRepeating("UpdateTarget", 0f, 1.0f / 60f);
    }

    private void Update()
    {
        if (_fireMode == FireMode.Manual)
        {
            ManualFireUpdate();
        }
        else if (_fireMode == FireMode.Automatic)
        {
            AutomaticFireUpdate();
        }


        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            _targetPoint = null;
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out hit))
            {
                _targetPoint = hit.transform;
            }
        }
    }

    private void AutomaticFireUpdate()
    {
        if (_targetPoint == null)
        {
            return;
        }
        float distanceToTarget = Vector3.Distance(transform.position, _targetPoint.position);
        if (distanceToTarget < ActiveRange.x || distanceToTarget > ActiveRange.y)
        {
            return;
        }
        FireWeapon();

    }

    private void ManualFireUpdate()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (_targetPoint == null)
            {
                return;
            }
            float distanceToTarget = Vector3.Distance(transform.position, _targetPoint.position);
            if (distanceToTarget < ActiveRange.x || distanceToTarget > ActiveRange.y)
            {
                return;
            }

            FireWeapon();
        }
    }

    private void FireWeapon()
    {
        for (int i = 0; i < _allLaunchers.Length; i++)
        {
            AALauncher launcher = _allLaunchers[i];
            // Get the target point's angle relative to the launcher. If it's within the launcher's FOV, fire.
            Vector3 directionToTarget = (_targetPoint.position - launcher.transform.position).normalized;
            float angleToTarget = Vector3.Angle(launcher.transform.forward, directionToTarget);
            if (angleToTarget > launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone / 2f)
            {
                continue;
            }

            if(launcher.missilePrefabToLaunch.GetComponent<AAMissile>().ActiveRange < Vector3.Distance(transform.position, _targetPoint.position))
            {
                continue;
            }

            if (_fireMode == FireMode.Manual)
            {
                if (launcher.MagazineCount > 0 && _targetPoint != null)
                {
                    launcher.Launch(_targetPoint);
                    _fireTimer = 0f;
                    break;
                }
            }
            else if (_fireMode == FireMode.Automatic)
            {
                if (launcher.MagazineCount > 0 && _targetPoint != null && _fireTimer >= FireInterval)
                {
                    launcher.Launch(_targetPoint);
                    _fireTimer = 0f;
                }
            }
        }
        _fireTimer += Time.deltaTime;
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

    void OnDrawGizmos()
    {
        // Draw a line heading to the target.
        if (_targetPoint != null)
        {
            Gizmos.color = Color.orangeRed;
            Gizmos.DrawLine(transform.position, _targetPoint.position);
        }
    }

}