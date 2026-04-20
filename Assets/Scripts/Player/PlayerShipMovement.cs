using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShipMovement : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovementController cameraController = null;

    [Header("Thrust Settings")]
    [Tooltip("Maximum thrust force (acceleration in units/s²)")]
    public float maxThrustForce = 50f;
    [Tooltip("Reverse thrust multiplier (0.5 = 50% of forward thrust)")]
    [Range(0.1f, 1f)]
    public float reverseThrustRatio = 0.5f;

    [Header("Speed Limiter")]
    [Tooltip("Enable speed limiter (cruise control)")]
    public bool enableSpeedLimiter = true;
    [Tooltip("Current speed limit setting")]
    public float speedLimit = 100f;
    [Tooltip("Maximum possible speed limit")]
    public float maxSpeedLimit = 200f;
    [Tooltip("Minimum speed limit")]
    public float minSpeedLimit = 10f;
    [Tooltip("How quickly speed limit changes with input")]
    public float speedLimitAdjustRate = 20f;
    [Tooltip("Auto-brake when exceeding speed limit")]
    public bool autoBrakeAtLimit = true;
    [Tooltip("Auto-brake strength (multiplier of max thrust)")]
    [Range(0.1f, 1f)]
    public float autoBrakeStrength = 0.3f;

    [Header("Drag & Damping")]
    [Tooltip("Linear drag (0 = no drag, realistic space)")]
    [Range(0f, 1f)]
    public float linearDrag = 0f;
    [Tooltip("Optional space friction for game feel (not realistic but useful)")]
    [Range(0f, 0.5f)]
    public float spaceFriction = 0.02f;

    [Header("Vertical Thrust")]
    [Tooltip("Enable direct vertical thrust")]
    public bool enableVerticalThrust = true;
    [Tooltip("Vertical thrust force")]
    public float verticalThrustForce = 30f;

    [Header("Strafe Thrust")]
    [Tooltip("Enable horizontal strafing")]
    public bool enableStrafe = true;
    [Tooltip("Strafe thrust force")]
    public float strafeThrustForce = 30f;

    [Header("Rotation Settings")]
    [Tooltip("Pitch torque (degrees/s²)")]
    public float pitchTorque = 90f;
    [Tooltip("Yaw torque (degrees/s²)")]
    public float yawTorque = 60f;
    [Tooltip("Roll torque (degrees/s²)")]
    public float rollTorque = 120f;
    [Tooltip("Maximum rotation speed (degrees/s)")]
    public float maxRotationSpeed = 180f;
    [Tooltip("Rotational drag (how quickly rotation slows)")]
    [Range(0f, 5f)]
    public float rotationalDrag = 2f;

    [Header("Input Smoothing")]
    [Tooltip("How quickly input ramps up/down")]
    public float inputSmoothSpeed = 8f;

    [Header("Stabilization")]
    [Tooltip("Automatically level roll when no input")]
    public bool autoLevelRoll = true;
    [Tooltip("Speed of auto-leveling")]
    public float autoLevelSpeed = 1f;
    [Tooltip("Delay before auto-level kicks in")]
    public float autoLevelDelay = 0.5f;
    [Tooltip("Kill rotation automatically when no input")]
    public bool autoKillRotation = true;

    [Header("Flight Assist")]
    [Tooltip("Enable flight assist (auto-counters drift and maintains forward speed)")]
    public bool flightAssist = false;
    [Tooltip("Flight assist strength")]
    [Range(0f, 1f)]
    public float flightAssistStrength = 0.5f;
    [Tooltip("How aggressively flight assist maintains forward speed (units/s² as fraction of max thrust)")]
    [Range(0.1f, 1f)]
    public float speedMaintenanceStrength = 0.5f;

    [Header("Velocity Coupling")]
    [Tooltip("How much velocity rotates with the ship (0 = full drift, 1 = full coupling)")]
    [Range(0f, 1f)]
    public float velocityCoupling = 0.8f;

    [Header("Engine Visuals")]
    [Tooltip("List of engine objects that has a Visual Effect component attached for thrust visuals")]
    public List<GameObject> EngineObjects;

    [Header("Debug")]
    [SerializeField] private bool showDebugHUD = true;
    [SerializeField] private Vector3 currentVelocity = Vector3.zero;
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private Vector3 currentAngularVelocity = Vector3.zero;
    [SerializeField] private Vector3 currentInputs = Vector3.zero;
    [SerializeField] private float throttlePercent = 0f;

    // Physics state - this is the actual velocity, persists between frames
    private Vector3 velocity = Vector3.zero;
    private Vector3 angularVelocity = Vector3.zero; // degrees per second for each axis

    // Smoothed input values
    private float smoothedPitchInput = 0f;
    private float smoothedYawInput = 0f;
    private float smoothedRollInput = 0f;
    private float smoothedThrottleInput = 0f;
    private float smoothedVerticalInput = 0f;
    private float smoothedStrafeInput = 0f;
    private float smoothedSpeedLimitInput = 0f;

    // Raw input values
    private float rawPitchInput = 0f;
    private float rawYawInput = 0f;
    private float rawRollInput = 0f;
    private float rawVerticalInput = 0f;
    private float rawStrafeInput = 0f;
    private float rawThrottleInput = 0f;
    private float rawSpeedLimitInput = 0f;

    // Auto-level timer
    private float timeSinceRollInput = 0f;
    private float timeSinceRotationInput = 0f;
    
    private Quaternion previousRotation;

    // Flight assist: maintained forward speed
    private float maintainedForwardSpeed = 0f;


    // Properties for external access
    public Vector3 Velocity => velocity;
    public float Speed => velocity.magnitude;
    public float SpeedLimit => speedLimit;
    public float ThrottlePercent => smoothedThrottleInput;
    public Vector3 AngularVelocity => angularVelocity;

    // Useful for UI - shows velocity relative to ship orientation
    public float ForwardSpeed => Vector3.Dot(velocity, transform.forward);
    public float LateralSpeed => Vector3.Dot(velocity, transform.right);
    public float VerticalSpeed => Vector3.Dot(velocity, transform.up);

    private void Awake()
    {
        if (cameraController == null)
        {
            cameraController = FindAnyObjectByType<PlayerMovementController>();
            if (cameraController == null)
                Debug.LogWarning(name + ": PlayerShipMovement - No PlayerMovementController found.");
        }
        previousRotation = transform.rotation;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f) return;

        ReadRawInput();
        SmoothInput();
        UpdateSpeedLimit(deltaTime);
        ApplyThrust(deltaTime);
        ApplyRotationalPhysics(deltaTime);

        ApplyVelocityCoupling();

        ApplyDrag(deltaTime);
        ApplyFlightAssist(deltaTime);
        ApplySpeedLimiter(deltaTime);
        ApplyMovement(deltaTime);
        UpdateDebugValues();
    }

    private void ApplyVelocityCoupling()
    {
        if (velocityCoupling <= 0f) return;
        if (velocity.sqrMagnitude < 0.01f) return;

        // Calculate the rotation delta since last frame
        Quaternion rotationDelta = transform.rotation * Quaternion.Inverse(previousRotation);

        // Rotate velocity by a portion of ship's rotation
        Vector3 rotatedVelocity = rotationDelta * velocity;

        // Blend between old velocity (drift) and rotated velocity (coupled)
        velocity = Vector3.Lerp(velocity, rotatedVelocity, velocityCoupling);

        // Store for next frame
        previousRotation = transform.rotation;
    }

    private void ReadRawInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // Reset raw inputs
        rawPitchInput = 0f;
        rawYawInput = 0f;
        rawRollInput = 0f;
        rawVerticalInput = 0f;
        rawStrafeInput = 0f;
        rawThrottleInput = 0f;
        rawSpeedLimitInput = 0f;

        if (gp != null)
        {
            rawPitchInput = -gp.leftStick.y.ReadValue();
            rawYawInput = gp.leftStick.x.ReadValue();
            rawRollInput = (gp.rightShoulder.isPressed ? 1f : 0f) - (gp.leftShoulder.isPressed ? 1f : 0f);
            rawThrottleInput = gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();

            if (enableVerticalThrust)
                rawVerticalInput = gp.rightStick.y.ReadValue();
            if (enableStrafe)
                rawStrafeInput = gp.rightStick.x.ReadValue();

            // D-pad up/down for speed limit
            rawSpeedLimitInput = gp.dpad.y.ReadValue();
        }

        if (kb != null)
        {
            // WASD: Pitch and Yaw
            rawPitchInput += kb.sKey.isPressed ? 1f : 0f;
            rawPitchInput -= kb.wKey.isPressed ? 1f : 0f;
            rawPitchInput = Mathf.Clamp(rawPitchInput, -1f, 1f);

            rawYawInput += kb.dKey.isPressed ? 1f : 0f;
            rawYawInput -= kb.aKey.isPressed ? 1f : 0f;
            rawYawInput = Mathf.Clamp(rawYawInput, -1f, 1f);

            // Q/E: Roll
            rawRollInput += kb.qKey.isPressed ? 1f : 0f;
            rawRollInput -= kb.eKey.isPressed ? 1f : 0f;
            rawRollInput = Mathf.Clamp(rawRollInput, -1f, 1f);

            // Shift/Ctrl: Throttle (now controls thrust, not speed directly)
            rawThrottleInput += kb.leftShiftKey.isPressed ? 1f : 0f;
            rawThrottleInput -= kb.leftCtrlKey.isPressed ? 1f : 0f;
            rawThrottleInput = Mathf.Clamp(rawThrottleInput, -1f, 1f);

            // Space/C: Vertical thrust
            if (enableVerticalThrust)
            {
                rawVerticalInput += kb.spaceKey.isPressed ? 1f : 0f;
                rawVerticalInput -= kb.cKey.isPressed ? 1f : 0f;
                rawVerticalInput = Mathf.Clamp(rawVerticalInput, -1f, 1f);
            }

            // Z/X: Strafe
            if (enableStrafe)
            {
                rawStrafeInput += kb.xKey.isPressed ? 1f : 0f;
                rawStrafeInput -= kb.zKey.isPressed ? 1f : 0f;
                rawStrafeInput = Mathf.Clamp(rawStrafeInput, -1f, 1f);
            }

            // R/F: Speed limit adjustment
            rawSpeedLimitInput += kb.rKey.isPressed ? 1f : 0f;
            rawSpeedLimitInput -= kb.fKey.isPressed ? 1f : 0f;
            rawSpeedLimitInput = Mathf.Clamp(rawSpeedLimitInput, -1f, 1f);

            // V: Toggle flight assist
            if (kb.vKey.wasPressedThisFrame)
            {
                flightAssist = !flightAssist;
            }

            // B: Full stop (kill all velocity)
            if (kb.bKey.isPressed)
            {
                ApplyFullStop();
            }
        }

        currentInputs = new Vector3(rawPitchInput, rawYawInput, rawRollInput);
    }

    private void SmoothInput()
    {
        float smoothRate = inputSmoothSpeed * Time.deltaTime;

        smoothedPitchInput = Mathf.MoveTowards(smoothedPitchInput, rawPitchInput, smoothRate);
        smoothedYawInput = Mathf.MoveTowards(smoothedYawInput, rawYawInput, smoothRate);
        smoothedRollInput = Mathf.MoveTowards(smoothedRollInput, rawRollInput, smoothRate);
        smoothedThrottleInput = Mathf.MoveTowards(smoothedThrottleInput, rawThrottleInput, smoothRate);
        smoothedVerticalInput = Mathf.MoveTowards(smoothedVerticalInput, rawVerticalInput, smoothRate);
        smoothedStrafeInput = Mathf.MoveTowards(smoothedStrafeInput, rawStrafeInput, smoothRate);
        smoothedSpeedLimitInput = Mathf.MoveTowards(smoothedSpeedLimitInput, rawSpeedLimitInput, smoothRate);
    }

    private void UpdateSpeedLimit(float deltaTime)
    {
        if (Mathf.Abs(smoothedSpeedLimitInput) > 0.01f)
        {
            speedLimit += smoothedSpeedLimitInput * speedLimitAdjustRate * deltaTime;
            speedLimit = Mathf.Clamp(speedLimit, minSpeedLimit, maxSpeedLimit);
        }
    }

    private void ApplyThrust(float deltaTime)
    {
        Vector3 thrustAcceleration = Vector3.zero;

        // Forward/Backward thrust
        if (Mathf.Abs(smoothedThrottleInput) > 0.01f)
        {
            float thrustMagnitude;
            if (smoothedThrottleInput > 0f)
            {
                // Forward thrust
                thrustMagnitude = smoothedThrottleInput * maxThrustForce;
            }
            else
            {
                // Reverse thrust (reduced power)
                thrustMagnitude = smoothedThrottleInput * maxThrustForce * reverseThrustRatio;
            }
            thrustAcceleration += transform.forward * thrustMagnitude;
        }

        // Vertical thrust
        if (enableVerticalThrust && Mathf.Abs(smoothedVerticalInput) > 0.01f)
        {
            thrustAcceleration += transform.up * smoothedVerticalInput * verticalThrustForce;
        }

        // Strafe thrust
        if (enableStrafe && Mathf.Abs(smoothedStrafeInput) > 0.01f)
        {
            thrustAcceleration += transform.right * smoothedStrafeInput * strafeThrustForce;
        }

        // Apply acceleration to velocity (F = ma, assuming m = 1)
        // Projected thrust clamping: allow direction change at speed limit, block speed increase
        if (enableSpeedLimiter && velocity.magnitude >= speedLimit)
        {
            Vector3 deltaV = thrustAcceleration * deltaTime;
            Vector3 newVelocity = velocity + deltaV;
            // Only allow the component that doesn't increase speed
            if (newVelocity.magnitude > velocity.magnitude)
            {
                // Project deltaV onto velocity direction and remove the positive (speed-increasing) part
                Vector3 velocityDir = velocity.normalized;
                float speedIncreasing = Vector3.Dot(deltaV, velocityDir);
                if (speedIncreasing > 0f)
                {
                    deltaV -= velocityDir * speedIncreasing;
                }
            }
            velocity += deltaV;
            // Clamp to speed limit in case of floating point drift
            if (velocity.magnitude > speedLimit)
            {
                velocity = velocity.normalized * speedLimit;
            }
        }
        else
        {
            velocity += thrustAcceleration * deltaTime;
        }

        // Absolute max speed cap (safety net)
        if (velocity.sqrMagnitude > 0.01f)
        {
            velocity = Vector3.ClampMagnitude(velocity, maxSpeedLimit * 2f);
        }

        // Update engine visuals based on throttle, the VFX has EngineParticleLifeTime(float) and EngineParticleSize(Vector2)
        if (EngineObjects != null)
        {
            foreach (var engine in EngineObjects)
            {
                var vfx = engine.GetComponent<UnityEngine.VFX.VisualEffect>();
                if (vfx != null)
                {
                    vfx.SetFloat("EngineParticleLifeTime", Mathf.Lerp(0.1f, 1f, smoothedThrottleInput));
                    // vfx.SetVector2("EngineParticleSize", new Vector2(Mathf.Lerp(0.1f, 1f, smoothedThrottleInput), Mathf.Lerp(0.1f, 1f, smoothedThrottleInput)));
                }
            }
        }
    }

    private void ApplyRotationalPhysics(float deltaTime)
    {
        // Track time since rotation input
        bool hasRotationInput = Mathf.Abs(rawPitchInput) > 0.01f ||
                                Mathf.Abs(rawYawInput) > 0.01f ||
                                Mathf.Abs(rawRollInput) > 0.01f;

        if (hasRotationInput)
        {
            timeSinceRotationInput = 0f;
        }
        else
        {
            timeSinceRotationInput += deltaTime;
        }

        // Track roll input specifically for auto-level
        if (Mathf.Abs(rawRollInput) > 0.01f)
        {
            timeSinceRollInput = 0f;
        }
        else
        {
            timeSinceRollInput += deltaTime;
        }

        // Apply torque to angular velocity
        angularVelocity.x += smoothedPitchInput * pitchTorque * deltaTime;
        angularVelocity.y += smoothedYawInput * yawTorque * deltaTime;
        angularVelocity.z += smoothedRollInput * rollTorque * deltaTime;

        // Clamp angular velocity
        angularVelocity.x = Mathf.Clamp(angularVelocity.x, -maxRotationSpeed, maxRotationSpeed);
        angularVelocity.y = Mathf.Clamp(angularVelocity.y, -maxRotationSpeed, maxRotationSpeed);
        angularVelocity.z = Mathf.Clamp(angularVelocity.z, -maxRotationSpeed, maxRotationSpeed);

        // Apply rotational drag
        if (rotationalDrag > 0f)
        {
            // Apply more drag when no input (auto-kill rotation)
            float dragMultiplier = 1f;
            if (autoKillRotation && !hasRotationInput)
            {
                dragMultiplier = 3f;
            }

            float dragFactor = 1f - (rotationalDrag * dragMultiplier * deltaTime);
            dragFactor = Mathf.Max(dragFactor, 0f);
            angularVelocity *= dragFactor;
        }

        // Stop tiny rotations
        if (angularVelocity.sqrMagnitude < 0.01f)
        {
            angularVelocity = Vector3.zero;
        }

        // Apply rotation
        if (angularVelocity.sqrMagnitude > 0.0001f)
        {
            Quaternion pitchRot = Quaternion.AngleAxis(angularVelocity.x * deltaTime, Vector3.right);
            Quaternion yawRot = Quaternion.AngleAxis(angularVelocity.y * deltaTime, Vector3.up);
            Quaternion rollRot = Quaternion.AngleAxis(-angularVelocity.z * deltaTime, Vector3.forward);

            transform.rotation = transform.rotation * pitchRot * yawRot * rollRot;
        }

        // Auto-level roll
        if (autoLevelRoll && timeSinceRollInput > autoLevelDelay)
        {
            ApplyAutoLevel(deltaTime);
        }
    }

    private void ApplyAutoLevel(float deltaTime)
    {
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude > 0.1f)
        {
            flatForward.Normalize();

            Vector3 targetUp = Vector3.up - Vector3.Dot(Vector3.up, transform.forward) * transform.forward;

            if (targetUp.sqrMagnitude > 0.01f)
            {
                targetUp.Normalize();
                Vector3 newUp = Vector3.Slerp(transform.up, targetUp, autoLevelSpeed * deltaTime);

                if (Vector3.Cross(transform.forward, newUp).sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(transform.forward, newUp);

                    // Also reduce roll angular velocity when auto-leveling
                    angularVelocity.z *= (1f - autoLevelSpeed * deltaTime);
                }
            }
        }
    }

    private void ApplyDrag(float deltaTime)
    {
        // Linear drag (atmospheric style - not realistic for space but useful for game feel)
        if (linearDrag > 0f)
        {
            float dragFactor = 1f - (linearDrag * deltaTime);
            dragFactor = Mathf.Max(dragFactor, 0f);
            velocity *= dragFactor;
        }

        // Space friction (very subtle, helps prevent infinite drift)
        if (spaceFriction > 0f && velocity.sqrMagnitude > 0.01f)
        {
            float frictionForce = spaceFriction * velocity.sqrMagnitude * deltaTime;
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, frictionForce);
        }
    }

    private void ApplyFlightAssist(float deltaTime)
    {
        if (!flightAssist || flightAssistStrength <= 0f) return;

        // Flight assist: kill lateral drift + maintain forward speed

        Vector3 forwardVelocity = Vector3.Project(velocity, transform.forward);
        Vector3 lateralVelocity = velocity - forwardVelocity;

        // Counter lateral drift (only when player isn't intentionally strafing/thrusting vertically)
        if (Mathf.Abs(smoothedStrafeInput) < 0.1f)
        {
            Vector3 rightDrift = Vector3.Project(lateralVelocity, transform.right);
            velocity -= rightDrift * flightAssistStrength * deltaTime * 2f;
        }

        if (Mathf.Abs(smoothedVerticalInput) < 0.1f)
        {
            Vector3 upDrift = Vector3.Project(lateralVelocity, transform.up);
            velocity -= upDrift * flightAssistStrength * deltaTime * 2f;
        }

        // Update maintained speed: manual throttle input overrides the maintained speed
        if (Mathf.Abs(smoothedThrottleInput) > 0.1f)
        {
            // Player is actively thrusting — update maintained speed to current forward speed
            maintainedForwardSpeed = Vector3.Dot(velocity, transform.forward);
        }

        // Auto-thrust to maintain forward speed when no throttle input
        if (Mathf.Abs(smoothedThrottleInput) < 0.1f && Mathf.Abs(maintainedForwardSpeed) > 0.5f)
        {
            float currentForwardSpeed = Vector3.Dot(velocity, transform.forward);
            float speedError = maintainedForwardSpeed - currentForwardSpeed;
            float correctionForce = maxThrustForce * speedMaintenanceStrength;
            float correction = Mathf.Clamp(speedError, -correctionForce * deltaTime, correctionForce * deltaTime);
            velocity += transform.forward * correction;
        }
    }

    private void ApplySpeedLimiter(float deltaTime)
    {
        if (!enableSpeedLimiter) return;

        float currentSpeed = velocity.magnitude;

        if (currentSpeed > speedLimit)
        {
            if (autoBrakeAtLimit)
            {
                // Gradually reduce speed when over limit
                float brakeForce = maxThrustForce * autoBrakeStrength;
                float newSpeed = Mathf.MoveTowards(currentSpeed, speedLimit, brakeForce * deltaTime);
                velocity = velocity.normalized * newSpeed;
            }
            else
            {
                // Hard cap (less realistic but ensures limit)
                velocity = velocity.normalized * speedLimit;
            }
        }
    }

    private void ApplyMovement(float deltaTime)
    {
        transform.position += velocity * deltaTime;
    }

    private void UpdateDebugValues()
    {
        currentVelocity = velocity;
        currentSpeed = velocity.magnitude;
        currentAngularVelocity = angularVelocity;
        throttlePercent = smoothedThrottleInput;
    }

    private void ApplyFullStop()
    {
        // Reset maintained speed so flight assist doesn't re-accelerate
        maintainedForwardSpeed = 0f;

        // Apply braking thrust opposite to current velocity
        if (velocity.sqrMagnitude > 0.1f)
        {
            Vector3 brakeDirection = -velocity.normalized;
            float brakeForce = maxThrustForce * 0.8f;
            velocity += brakeDirection * brakeForce * Time.deltaTime;

            // Snap to zero if very slow
            if (velocity.sqrMagnitude < 1f)
            {
                velocity = Vector3.zero;
            }
        }
        else
        {
            velocity = Vector3.zero;
        }
    }

    // Public methods for external control
    public void SetVelocity(Vector3 newVelocity)
    {
        velocity = newVelocity;
    }

    public void AddVelocity(Vector3 deltaVelocity)
    {
        velocity += deltaVelocity;
    }

    public void SetSpeedLimit(float limit)
    {
        speedLimit = Mathf.Clamp(limit, minSpeedLimit, maxSpeedLimit);
    }

    public void StopAllMovement()
    {
        velocity = Vector3.zero;
        angularVelocity = Vector3.zero;
    }

    public void StopRotation()
    {
        angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Returns the time it would take to reach the speed limit at current throttle
    /// </summary>
    public float GetTimeToSpeedLimit()
    {
        float currentSpeed = velocity.magnitude;
        if (currentSpeed >= speedLimit) return 0f;
        if (smoothedThrottleInput <= 0f) return float.PositiveInfinity;

        float acceleration = smoothedThrottleInput * maxThrustForce;
        float remainingSpeed = speedLimit - currentSpeed;
        return remainingSpeed / acceleration;
    }

    /// <summary>
    /// Returns the distance needed to stop from current velocity
    /// </summary>
    public float GetStoppingDistance()
    {
        float currentSpeed = velocity.magnitude;
        if (currentSpeed < 0.1f) return 0f;

        // Using reverse thrust
        float brakeAcceleration = maxThrustForce * reverseThrustRatio;
        // v² = 2as, so s = v²/2a
        return (currentSpeed * currentSpeed) / (2f * brakeAcceleration);
    }

    private void OnGUI()
    {
        if (!showDebugHUD) return;

        float w = 260f;
        float h = 130f;
        float margin = 10f;
        Rect boxRect = new Rect(Screen.width - w - margin, margin, w, h);

        GUI.Box(boxRect, "");

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        float x = boxRect.x + 8f;
        float y = boxRect.y + 6f;
        float lineH = 20f;

        float speed = velocity.magnitude;
        float fwdSpeed = Vector3.Dot(velocity, transform.forward);

        GUI.Label(new Rect(x, y, w, lineH), $"Speed: {speed:F1} / {speedLimit:F0}", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Fwd Speed: {fwdSpeed:F1}", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Throttle: {smoothedThrottleInput * 100f:F0}%", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Flight Assist: {(flightAssist ? "ON" : "OFF")}", labelStyle);
        y += lineH;
        if (flightAssist && Mathf.Abs(maintainedForwardSpeed) > 0.5f)
            GUI.Label(new Rect(x, y, w, lineH), $"Cruise: {maintainedForwardSpeed:F1}", labelStyle);
        else
            GUI.Label(new Rect(x, y, w, lineH), "Cruise: --", labelStyle);
    }
}