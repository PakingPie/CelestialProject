using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


public class AAFire : WeaponBase
{
    public float FireInterval = 10.0f;
    private float _fireTimer = 0f;
    private AALauncher _launcher;

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
            if(distanceToTarget < ActiveRange.y && _fireTimer <= 0.0f)
            {
                _launcher.Launch(Targeted);
                _fireTimer = FireInterval;
            }

            if (_fireTimer > 0.0f)
                _fireTimer -= Time.deltaTime;
        }
    }
}