using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


public class AAFire : MonoBehaviour
{
    public Vector2 ActiveRange = new Vector2(5f, 300f);
    private AALauncher[] _allLaunchers;
    private Transform _targetPoint;

    private void Start()
    {
        _allLaunchers = GetComponentsInChildren<AALauncher>();
        InvokeRepeating("UpdateTarget", 0f, 1.0f / 60f);
    }

    private void Update()
    {
        // if (Mouse.current.leftButton.wasPressedThisFrame)
        // {
        // }
        if(_targetPoint != null)
            FireWeapon();
        
        if(Mouse.current.rightButton.wasPressedThisFrame)
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

    private void FireWeapon()
    {
        foreach (AALauncher launcher in _allLaunchers)
        {
            if (launcher.missileCount > 0)
            {
                if(_targetPoint != null)
                {
                    launcher.Launch(_targetPoint);
                    break;
                }
                // else
                // {
                //     launcher.Launch(null);
                //     break;
                // }                
            }
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