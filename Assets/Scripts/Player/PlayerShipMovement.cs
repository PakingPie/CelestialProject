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
    [Tooltip("How quickly target speed changes when pressing Shift/Ctrl (units/s per second)")]
    public float speedChangeRate = 40f;

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
    [Tooltip("Optional space friction for game feel (not realistic but useful). Set 0 for true Newtonian.")]
    [Range(0f, 0.5f)]
    public float spaceFriction = 0f;

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
    [Tooltip("Enable flight assist (auto-counters lateral drift)")]
    public bool flightAssist = false;
    [Tooltip("Flight assist strength")]
    [Range(0f, 1f)]
    public float flightAssistStrength = 0.5f;

    [Header("Velocity Coupling")]
    [Tooltip("How much velocity rotates with the ship (0 = full drift, 1 = full coupling)")]
    [Range(0f, 1f)]
    public float velocityCoupling = 0.8f;

    [Header("Mouse Aim (Instructor)")]
    [Tooltip("Ship auto-steers toward mouse cursor position when no WASD input")]
    public bool mouseAimEnabled = true;
    [Tooltip("Response strength - higher values make the ship track the mouse faster")]
    [Range(0.01f, 0.3f)]
    public float mouseAimResponse = 0.1f;
    [Tooltip("Angular deadzone in degrees - errors below this are ignored")]
    public float mouseAimDeadzone = 0.5f;

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

    // Target speed model
    private float targetSpeed = 0f;
    private float autoThrottleAmount = 0f; // -1 to 1, computed each frame for VFX/HUD
    private bool isFullStopping = false;

    // Mouse aim state
    private Vector3 mouseAimWorldPos = Vector3.zero;


    // Properties for external access
    public Vector3 Velocity => velocity;
    public float Speed => velocity.magnitude;
    public float SpeedLimit => speedLimit;
    public float TargetSpeed => targetSpeed;
    public float ThrottlePercent => autoThrottleAmount;
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
        ComputeMouseAimInput();
        SmoothInput();
        UpdateSpeedLimit(deltaTime);
        UpdateTargetSpeed(deltaTime);
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

            // Shift/Ctrl: Adjust target speed
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

            // B: Full stop (gradual deceleration to zero)
            if (kb.bKey.isPressed)
            {
                ApplyFullStop();
            }
            else if (kb.bKey.wasReleasedThisFrame)
            {
                isFullStopping = false;
            }
        }

        currentInputs = new Vector3(rawPitchInput, rawYawInput, rawRollInput);
    }

    private void ComputeMouseAimInput()
    {
        if (!mouseAimEnabled || cameraController == null) return;

        // Don't steer via mouse during freelook
        if (cameraController.IsFreelooking) return;

        // Only use mouse aim when no manual pitch/yaw input (QE roll is independent)
        bool hasManualPitchYaw = Mathf.Abs(rawPitchInput) > 0.01f ||
                                  Mathf.Abs(rawYawInput) > 0.01f;
        if (hasManualPitchYaw) return;

        mouseAimWorldPos = cameraController.GetMouseAimWorldPosition();
        Vector3 toAim = (mouseAimWorldPos - transform.position).normalized;
        Vector3 localAim = transform.InverseTransformDirection(toAim);

        // Angular errors in degrees
        float yawError = Mathf.Atan2(localAim.x, localAim.z) * Mathf.Rad2Deg;
        float pitchError = -Mathf.Atan2(localAim.y, localAim.z) * Mathf.Rad2Deg;

        // Apply deadzone
        if (Mathf.Abs(yawError) < mouseAimDeadzone) yawError = 0f;
        if (Mathf.Abs(pitchError) < mouseAimDeadzone) pitchError = 0f;

        // Convert error to input command (clamped to [-1, 1])
        rawPitchInput = Mathf.Clamp(pitchError * mouseAimResponse, -1f, 1f);
        rawYawInput = Mathf.Clamp(yawError * mouseAimResponse, -1f, 1f);
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

    private void UpdateTargetSpeed(float deltaTime)
    {
        // Throttle input adjusts target speed
        if (Mathf.Abs(smoothedThrottleInput) > 0.01f)
        {
            isFullStopping = false; // Manual input cancels full stop
            targetSpeed += smoothedThrottleInput * speedChangeRate * deltaTime;
        }

        // Clamp target speed: can go negative (reverse) up to reverse ratio of speed limit
        float maxReverse = speedLimit * reverseThrustRatio;
        targetSpeed = Mathf.Clamp(targetSpeed, -maxReverse, speedLimit);

        // Snap to zero if very close
        if (Mathf.Abs(targetSpeed) < 0.5f && Mathf.Abs(smoothedThrottleInput) < 0.01f)
        {
            targetSpeed = 0f;
        }
    }

    private void ApplyThrust(float deltaTime)
    {
        Vector3 thrustAcceleration = Vector3.zero;

        // Auto-throttle: compute thrust to reach target speed along ship forward
        float currentForwardSpeed = Vector3.Dot(velocity, transform.forward);
        float speedError = targetSpeed - currentForwardSpeed;

        if (Mathf.Abs(speedError) > 0.1f)
        {
            float thrustMagnitude;
            if (speedError > 0f)
            {
                // Need to speed up
                thrustMagnitude = Mathf.Min(speedError / deltaTime, maxThrustForce);
                autoThrottleAmount = thrustMagnitude / maxThrustForce;
            }
            else
            {
                // Need to slow down
                float maxBrake = maxThrustForce * reverseThrustRatio;
                // Full stop uses full braking power
                if (isFullStopping) maxBrake = maxThrustForce * 0.8f;
                thrustMagnitude = Mathf.Max(speedError / deltaTime, -maxBrake);
                autoThrottleAmount = thrustMagnitude / maxThrustForce;
            }
            thrustAcceleration += transform.forward * thrustMagnitude;
        }
        else
        {
            autoThrottleAmount = 0f;
        }

        // Vertical thrust (still direct manual control)
        if (enableVerticalThrust && Mathf.Abs(smoothedVerticalInput) > 0.01f)
        {
            thrustAcceleration += transform.up * smoothedVerticalInput * verticalThrustForce;
        }

        // Strafe thrust (still direct manual control)
        if (enableStrafe && Mathf.Abs(smoothedStrafeInput) > 0.01f)
        {
            thrustAcceleration += transform.right * smoothedStrafeInput * strafeThrustForce;
        }

        // Apply acceleration to velocity
        velocity += thrustAcceleration * deltaTime;

        // Absolute max speed cap (safety net)
        if (velocity.sqrMagnitude > 0.01f)
        {
            velocity = Vector3.ClampMagnitude(velocity, maxSpeedLimit * 2f);
        }

        // Update engine visuals based on auto-throttle effort
        if (EngineObjects != null)
        {
            float vfxThrottle = Mathf.Clamp01(autoThrottleAmount);
            foreach (var engine in EngineObjects)
            {
                var vfx = engine.GetComponent<UnityEngine.VFX.VisualEffect>();
                if (vfx != null)
                {
                    vfx.SetFloat("EngineParticleLifeTime", Mathf.Lerp(0.1f, 1f, vfxThrottle));
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

        // Flight assist: kill lateral drift only
        // Forward speed is now managed by the target speed system

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
        throttlePercent = autoThrottleAmount;
    }

    private void ApplyFullStop()
    {
        // Gradual deceleration — handled by setting targetSpeed = 0
        // and letting auto-throttle brake. Also kill lateral velocity.
        targetSpeed = 0f;
        isFullStopping = true;

        // Also brake lateral velocity (not just forward)
        Vector3 forwardComponent = Vector3.Project(velocity, transform.forward);
        Vector3 lateralVelocity = velocity - forwardComponent;
        if (lateralVelocity.sqrMagnitude > 0.1f)
        {
            float brakeForce = maxThrustForce * 0.8f;
            velocity -= lateralVelocity.normalized * Mathf.Min(brakeForce * Time.deltaTime, lateralVelocity.magnitude);
        }

        // Snap to zero if very slow
        if (velocity.sqrMagnitude < 0.5f)
        {
            velocity = Vector3.zero;
            isFullStopping = false;
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
        if (targetSpeed <= currentSpeed) return float.PositiveInfinity;

        float acceleration = maxThrustForce;
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
        float h = 150f;
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
        GUI.Label(new Rect(x, y, w, lineH), $"Target: {targetSpeed:F1}", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Throttle: {autoThrottleAmount * 100f:F0}%", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), $"Flight Assist: {(flightAssist ? "ON" : "OFF")}", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), mouseAimEnabled ? "Mouse Aim: ON" : "Mouse Aim: OFF", labelStyle);
        y += lineH;
        GUI.Label(new Rect(x, y, w, lineH), isFullStopping ? "FULL STOP" : "", labelStyle);

        // Mouse aim debug crosshairs
        if (mouseAimEnabled)
        {
            Camera cam = Camera.main;
            var mouse = Mouse.current;

            if (cam != null && mouse != null)
            {
                // Green crosshair at mouse position (aim reticle)
                Vector2 mousePos = mouse.position.ReadValue();
                float mouseGuiY = Screen.height - mousePos.y;
                DrawDebugCrosshair(mousePos.x, mouseGuiY, Color.green, 15f);

                // Yellow crosshair at boresight (where ship nose points)
                Vector3 boresightWorld = transform.position + transform.forward * 500f;
                Vector3 boresightScreen = cam.WorldToScreenPoint(boresightWorld);
                if (boresightScreen.z > 0)
                {
                    float boreGuiY = Screen.height - boresightScreen.y;
                    DrawDebugCrosshair(boresightScreen.x, boreGuiY, Color.yellow, 10f);
                }
            }
        }
    }

    private void DrawDebugCrosshair(float x, float y, Color color, float size)
    {
        Color prevColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x - 1, y - size, 2, size * 2), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - size, y - 1, size * 2, 2), Texture2D.whiteTexture);
        GUI.color = prevColor;
    }
}