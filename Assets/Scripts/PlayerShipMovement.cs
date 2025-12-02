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
    public float thrustAcceleration = 100f;
    [Tooltip("Current thrust level")]
    [SerializeField] private float currentThrust = 0f;

    [Header("Rotation Settings")]
    [Tooltip("Pitch speed (up/down tilt)")]
    public float pitchSpeed = 45f;
    [Tooltip("Yaw speed (left/right turn)")]
    public float yawSpeed = 30f;
    [Tooltip("Roll speed (barrel roll)")]
    public float rollSpeed = 60f;

    [Header("Stabilization")]
    [Tooltip("Automatically level roll when no input")]
    public bool autoLevelRoll = true;
    [Tooltip("Speed of auto-leveling")]
    public float autoLevelSpeed = 2f;

    [Header("Vertical Movement")]
    [Tooltip("Enable direct vertical thrust (spaceship style)")]
    public bool enableVerticalThrust = true;
    [Tooltip("Vertical thrust speed")]
    public float verticalThrustSpeed = 50f;

    [Header("Strafe Movement")]
    [Tooltip("Enable horizontal strafing")]
    public bool enableStrafe = true;
    [Tooltip("Strafe speed")]
    public float strafeSpeed = 30f;

    [Header("Debug")]
    [SerializeField] private Vector3 currentInputs = Vector3.zero;

    // Input values
    private float pitchInput = 0f;
    private float yawInput = 0f;
    private float rollInput = 0f;
    private float verticalInput = 0f;
    private float strafeInput = 0f;
    private float throttleInput = 0f;

    // Properties for external access
    public float CurrentThrust => currentThrust;
    public float ThrustPercent => Mathf.InverseLerp(minThrust, maxThrust, currentThrust);
    public Vector3 Velocity => transform.forward * currentThrust;

    private void Awake()
    {
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<PlayerMovementController>();
            if (cameraController == null)
                Debug.LogWarning(name + ": PlayerShipMovement - No PlayerMovementController found. Camera will not follow ship.");
        }
    }

    private void Update()
    {
        ReadInput();
        ApplyThrust();
        ApplyRotation();
        ApplyMovement();
    }

    private void ReadInput()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        // Reset inputs
        pitchInput = 0f;
        yawInput = 0f;
        rollInput = 0f;
        verticalInput = 0f;
        strafeInput = 0f;
        throttleInput = 0f;

        if (gp != null)
        {
            // Gamepad controls
            // Left stick: Pitch and Yaw
            pitchInput = -gp.leftStick.y.ReadValue(); // Inverted: push forward to pitch down
            yawInput = gp.leftStick.x.ReadValue();

            // Bumpers: Roll
            rollInput = (gp.rightShoulder.isPressed ? 1f : 0f) - (gp.leftShoulder.isPressed ? 1f : 0f);

            // Triggers: Throttle
            throttleInput = gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue();

            // Right stick: Vertical and Strafe
            if (enableVerticalThrust)
                verticalInput = gp.rightStick.y.ReadValue();
            if (enableStrafe)
                strafeInput = gp.rightStick.x.ReadValue();
        }
        
        if (kb != null)
        {
            // Keyboard controls (can combine with gamepad)
            
            // WASD: Pitch and Yaw
            if (kb.wKey.isPressed) pitchInput = -1f; // W pitches down (nose down)
            if (kb.sKey.isPressed) pitchInput = 1f;  // S pitches up (nose up)
            if (kb.aKey.isPressed) yawInput = -1f;   // A turns left
            if (kb.dKey.isPressed) yawInput = 1f;    // D turns right

            // Q/E: Roll
            if (kb.qKey.isPressed) rollInput = 1f;   // Q rolls left
            if (kb.eKey.isPressed) rollInput = -1f;  // E rolls right

            // Shift/Ctrl: Throttle
            if (kb.leftShiftKey.isPressed) throttleInput = 1f;
            if (kb.leftCtrlKey.isPressed) throttleInput = -1f;

            // Space/C: Vertical thrust (spaceship mode)
            if (enableVerticalThrust)
            {
                if (kb.spaceKey.isPressed) verticalInput = 1f;
                if (kb.leftAltKey.isPressed) verticalInput = -1f;
            }

            // Z/X: Strafe (optional)
            if (enableStrafe)
            {
                if (kb.zKey.isPressed) strafeInput = -1f;
                if (kb.xKey.isPressed) strafeInput = 1f;
            }
        }

        currentInputs = new Vector3(pitchInput, yawInput, rollInput);
    }

    private void ApplyThrust()
    {
        // Gradually change thrust based on input
        if (Mathf.Abs(throttleInput) > 0.01f)
        {
            currentThrust += throttleInput * thrustAcceleration * Time.deltaTime;
        }
        else
        {
            // Optional: slowly decay thrust when no input (like drag)
            // currentThrust = Mathf.MoveTowards(currentThrust, 0f, thrustDecay * Time.deltaTime);
        }

        // Clamp thrust to limits
        currentThrust = Mathf.Clamp(currentThrust, minThrust, maxThrust);
    }

    private void ApplyRotation()
    {
        // Calculate rotation this frame
        float pitch = pitchInput * pitchSpeed * Time.deltaTime;
        float yaw = yawInput * yawSpeed * Time.deltaTime;
        float roll = rollInput * rollSpeed * Time.deltaTime;

        // Apply rotation
        transform.Rotate(pitch, yaw, roll, Space.Self);

        // Auto-level roll if enabled and no roll input
        if (autoLevelRoll && Mathf.Abs(rollInput) < 0.01f)
        {
            // Get current roll angle
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            
            if (flatForward.sqrMagnitude > 0.01f)
            {
                flatForward.Normalize();
                
                // Calculate target rotation with zero roll
                Quaternion targetRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                
                // Only correct roll, preserve pitch and yaw
                Vector3 currentEuler = transform.eulerAngles;
                Vector3 targetEuler = targetRotation.eulerAngles;
                
                float correctedRoll = Mathf.LerpAngle(currentEuler.z, 0f, autoLevelSpeed * Time.deltaTime);
                transform.eulerAngles = new Vector3(currentEuler.x, currentEuler.y, correctedRoll);
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 movement = Vector3.zero;

        // Forward/backward thrust
        movement += transform.forward * currentThrust * Time.deltaTime;

        // Vertical thrust
        if (enableVerticalThrust && Mathf.Abs(verticalInput) > 0.01f)
        {
            movement += transform.up * verticalInput * verticalThrustSpeed * Time.deltaTime;
        }

        // Strafe
        if (enableStrafe && Mathf.Abs(strafeInput) > 0.01f)
        {
            movement += transform.right * strafeInput * strafeSpeed * Time.deltaTime;
        }

        // Apply movement
        transform.position += movement;
    }

    // Public methods for external control (AI, cutscenes, etc.)
    public void SetThrust(float thrust)
    {
        currentThrust = Mathf.Clamp(thrust, minThrust, maxThrust);
    }

    public void AddThrust(float amount)
    {
        currentThrust = Mathf.Clamp(currentThrust + amount, minThrust, maxThrust);
    }

    public void StopThrust()
    {
        currentThrust = 0f;
    }
}