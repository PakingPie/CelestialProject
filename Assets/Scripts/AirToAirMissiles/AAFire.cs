using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


public class AAFire : WeaponBase
{
    [Header("Missile Fire Settings")]
    [Tooltip("The target to fire at.")]
    public float FireInterval = 10.0f;
    private float _fireTimer = 0f;
    private AALauncher _launcher;

    [Header("Debug Settings")]
    public bool EnableDebugGizmos = false;
    public float TestSeekerCone = 30f;

    private void Start()
    {
        _launcher = GetComponentInChildren<AALauncher>();
    }

    private void Update()
    {
        if (Targeted != null)
        {
            // Get Relative angles to target 
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);
            // Get seeker cone angle from launcher's prebab
            float seekerCone = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone;
            if (Mathf.Abs(relativeAngles.x) > seekerCone / 2f || Mathf.Abs(relativeAngles.y) > seekerCone / 2f)
                return;

            // Distance
            float distanceToTarget = Vector3.Distance(transform.position, Targeted.position);
            if (distanceToTarget < ActiveRange.y && _fireTimer <= 0.0f)
            {
                _launcher.Launch(Targeted);
                
                // Check if using salvo mode (FireOnFullyReload)
                AAHardpoint hardpoint = _launcher as AAHardpoint;
                if (hardpoint != null && hardpoint.FireOnFullyReload)
                {
                    // In salvo mode: use SalvoInterval between shots
                    // Reload timing is handled internally by hardpoint's reloadTime
                    _fireTimer = hardpoint.SalvoInterval;
                }
                else
                {
                    // Normal mode: use FireInterval between shots
                    _fireTimer = FireInterval;
                }
            }

            if (_fireTimer > 0.0f)
                _fireTimer -= Time.deltaTime;
        }
    }

    public void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (Targeted != null && EnableDebugGizmos)
        {
            // Draw line to test target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, Targeted.position);
            // Calculate relative angles using the new firing direction method
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);

            // Get seeker cone if launcher is available
            float seekerCone = TestSeekerCone;
            if (_launcher != null && _launcher.missilePrefabToLaunch != null)
            {
                var missile = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>();
                if (missile != null)
                    seekerCone = missile.seekerCone;
            }

            // Check if target is within seeker cone
            bool withinCone = Mathf.Abs(relativeAngles.x) <= seekerCone / 2f &&
                              Mathf.Abs(relativeAngles.y) <= seekerCone / 2f;

            // Draw sphere at target - green if in cone, red if outside
            Gizmos.color = withinCone ? Color.green : Color.red;
            Gizmos.DrawWireSphere(Targeted.position, 1f);

            // Draw firing direction (green)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, transform.forward * 20f);

            // Draw transform.forward (blue) for comparison
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, transform.forward * 15f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(Targeted.position + Vector3.up * 2f,
                $"Azimuth: {relativeAngles.x:F1}°  Elev: {relativeAngles.y:F1}°\n" +
                $"Seeker Cone: {seekerCone}°  In Cone: {withinCone}\n");
#endif
        }
    }
}