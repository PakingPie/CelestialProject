using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShipMovement : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private PlayerMovementController cameraController = null;

    [Header("Movement Settings")]
    [Tooltip("Maximum forward/backward thrust")]
    public float maxThrust = 100f;
    [Tooltip("Minimum thrust (can be negative for reverse)")]
    public float minThrust = -50f;
    [Tooltip("How quickly thrust changes")]
    public float thrustAcceleration = 50f;
    [Tooltip("How quickly thrust decays when no input")]
    public float thrustDecay = 20f;

    [Header("Throttle")]
    [Tooltip("When true, throttle stays at current value. When false, throttle declines automatically.")]
    public bool MaintainThrottle = true;

    [Header("Vertical Movement")]
    [Tooltip("Enable direct vertical thrust (spaceship style)")]
    public bool enableVerticalThrust = true;
    [Tooltip("Maximum vertical thrust speed")]
    public float maxVerticalThrust = 60f;
    [Tooltip("Vertical thrust acceleration")]
    public float verticalAcceleration = 80f;
    [Tooltip("Vertical thrust decay")]
    public float verticalDecay = 40f;

    [Header("Strafe Movement")]
    [Tooltip("Enable horizontal strafing")]
    public bool enableStrafe = true;
    [Tooltip("Maximum strafe speed")]
    public float maxStrafeThrust = 40f;
    [Tooltip("Strafe acceleration")]
    public float strafeAcceleration = 60f;
    [Tooltip("Strafe decay")]
    public float strafeDecay = 30f;

    [Header("Rotation Settings")]
    [Tooltip("Pitch speed (up/down tilt)")]
    public float pitchSpeed = 45f;
    [Tooltip("Yaw speed (left/right turn)")]
    public float yawSpeed = 30f;
    [Tooltip("Roll speed (barrel roll)")]
    public float rollSpeed = 60f;

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

    [Header("Debug")]
    [SerializeField] private Vector3 currentVelocity = Vector3.zero;
    [SerializeField] private Vector3 currentInputs = Vector3.zero;

    // Current thrust values (momentum-based)
    private float currentForwardThrust = 0f;
    private float currentVerticalThrust = 0f;
    private float currentStrafeThrust = 0f;

    // Smoothed input values
    private float smoothedPitchInput = 0f;
    private float smoothedYawInput = 0f;
    private float smoothedRollInput = 0f;
    private float smoothedThrottleInput = 0f;
    private float smoothedVerticalInput = 0f;
    private float smoothedStrafeInput = 0f;

    // Raw input values
    private float rawPitchInput = 0f;
    private float rawYawInput = 0f;
    private float rawRollInput = 0f;
    private float rawVerticalInput = 0f;
    private float rawStrafeInput = 0f;
    private float rawThrottleInput = 0f;

    // Auto-level timer
    private float timeSinceRollInput = 0f;

    // Properties for external access
    public float CurrentThrust => currentForwardThrust;
    public float ThrustPercent => Mathf.InverseLerp(minThrust, maxThrust, currentForwardThrust);
    public Vector3 Velocity => currentVelocity;

    private void Awake()
    {
        if (cameraController == null)
        {
            cameraController = FindAnyObjectByType<PlayerMovementController>();
            if (cameraController == null)
                Debug.LogWarning(name + ": PlayerShipMovement - No PlayerMovementController found.");
        }
    }

    private void Update()
    {
        ReadRawInput();
        SmoothInput();
        ApplyThrust();
        ApplyRotation();
        ApplyMovement();
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
        }

        if (kb != null)
        {
            // WASD: Pitch and Yaw
            if (kb.wKey.isPressed) rawPitchInput = -1f;
            if (kb.sKey.isPressed) rawPitchInput = 1f;
            if (kb.aKey.isPressed) rawYawInput = -1f;
            if (kb.dKey.isPressed) rawYawInput = 1f;

            // Q/E: Roll
            if (kb.qKey.isPressed) rawRollInput = 1f;
            if (kb.eKey.isPressed) rawRollInput = -1f;

            // Shift/Ctrl: Throttle
            if (kb.leftShiftKey.isPressed) rawThrottleInput = 1f;
            if (kb.leftCtrlKey.isPressed) rawThrottleInput = -1f;

            // Space/Alt: Vertical thrust
            if (enableVerticalThrust)
            {
                if (kb.spaceKey.isPressed) rawVerticalInput = 1f;
                if (kb.leftAltKey.isPressed) rawVerticalInput = -1f;
            }

            // Z/X: Strafe
            if (enableStrafe)
            {
                if (kb.zKey.isPressed) rawStrafeInput = -1f;
                if (kb.xKey.isPressed) rawStrafeInput = 1f;
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
    }

    private void ApplyThrust()
    {
        // Forward thrust with momentum
        if (Mathf.Abs(smoothedThrottleInput) > 0.01f)
        {
            currentForwardThrust += smoothedThrottleInput * thrustAcceleration * Time.deltaTime;
        }
        else if (!MaintainThrottle)
        {
            // Only decay thrust if MaintainThrottle is false
            currentForwardThrust = Mathf.MoveTowards(currentForwardThrust, 0f, thrustDecay * Time.deltaTime);
        }
        // When MaintainThrottle is true and no input, thrust stays at current value

        currentForwardThrust = Mathf.Clamp(currentForwardThrust, minThrust, maxThrust);

        // Vertical thrust with momentum
        if (enableVerticalThrust)
        {
            if (Mathf.Abs(smoothedVerticalInput) > 0.01f)
            {
                currentVerticalThrust += smoothedVerticalInput * verticalAcceleration * Time.deltaTime;
            }
            else
            {
                currentVerticalThrust = Mathf.MoveTowards(currentVerticalThrust, 0f, verticalDecay * Time.deltaTime);
            }
            currentVerticalThrust = Mathf.Clamp(currentVerticalThrust, -maxVerticalThrust, maxVerticalThrust);
        }

        // Strafe thrust with momentum
        if (enableStrafe)
        {
            if (Mathf.Abs(smoothedStrafeInput) > 0.01f)
            {
                currentStrafeThrust += smoothedStrafeInput * strafeAcceleration * Time.deltaTime;
            }
            else
            {
                currentStrafeThrust = Mathf.MoveTowards(currentStrafeThrust, 0f, strafeDecay * Time.deltaTime);
            }
            currentStrafeThrust = Mathf.Clamp(currentStrafeThrust, -maxStrafeThrust, maxStrafeThrust);
        }
    }

    private void ApplyRotation()
    {
        float pitch = smoothedPitchInput * pitchSpeed * Time.deltaTime;
        float yaw = smoothedYawInput * yawSpeed * Time.deltaTime;
        float roll = smoothedRollInput * rollSpeed * Time.deltaTime;

        transform.Rotate(pitch, yaw, roll, Space.Self);

        // Track time since roll input for delayed auto-level
        if (Mathf.Abs(rawRollInput) > 0.01f)
        {
            timeSinceRollInput = 0f;
        }
        else
        {
            timeSinceRollInput += Time.deltaTime;
        }

        // Auto-level roll with delay
        if (autoLevelRoll && timeSinceRollInput > autoLevelDelay)
        {
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.01f)
            {
                Vector3 currentEuler = transform.eulerAngles;
                float correctedRoll = Mathf.LerpAngle(currentEuler.z, 0f, autoLevelSpeed * Time.deltaTime);
                transform.eulerAngles = new Vector3(currentEuler.x, currentEuler.y, correctedRoll);
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 movement = Vector3.zero;

        movement += transform.forward * currentForwardThrust * Time.deltaTime;
        movement += transform.up * currentVerticalThrust * Time.deltaTime;
        movement += transform.right * currentStrafeThrust * Time.deltaTime;

        transform.position += movement;
        currentVelocity = movement / Time.deltaTime;
    }

    public void SetThrust(float thrust)
    {
        currentForwardThrust = Mathf.Clamp(thrust, minThrust, maxThrust);
    }

    public void AddThrust(float amount)
    {
        currentForwardThrust = Mathf.Clamp(currentForwardThrust + amount, minThrust, maxThrust);
    }

    public void StopAllThrust()
    {
        currentForwardThrust = 0f;
        currentVerticalThrust = 0f;
        currentStrafeThrust = 0f;
    }
}