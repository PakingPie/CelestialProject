using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MissileTurret : WeaponBase
{
    private AALauncher _launcher;

    public float FireInterval = 2.0f;

    void Start()
    {
        _launcher = GetComponentInChildren<AALauncher>();
    }

    void Update()
    {
        if (IsAimed && Targeted != null)
        {
            // Get Relative angles to target 
            Vector2 relativeAngles = CalcuateRelativeAngles(Targeted);
            // Get seeker cone angle from launcher's prebab
            float seekerCone = _launcher.missilePrefabToLaunch.GetComponent<AAMissile>().seekerCone;
            if (Mathf.Abs(relativeAngles.x) > seekerCone / 2f || Mathf.Abs(relativeAngles.y) > seekerCone / 2f)
                return;
            
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
}
