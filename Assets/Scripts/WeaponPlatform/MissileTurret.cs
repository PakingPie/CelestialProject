using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    }

    void Update()
    {
        // base.Update();
        if (!IsAimed)
            IsFiring = false;
        else
            IsFiring = true;

        if (IsFiring && Targeted != null)
        {
            // _fireTimer += Time.deltaTime;
            // if (_fireTimer < FireInterval)
            //     return;
            // _fireTimer = 0.0f;

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
