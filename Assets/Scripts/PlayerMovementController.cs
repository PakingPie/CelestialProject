using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbit camera controller for ship. Camera orbits around the ship
/// while the ship moves independently via keyboard.
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField]
    [Tooltip("Transform of the ship the camera follows")]
    private Transform ship = null;

    [SerializeField]
    [Tooltip("Reference to ship movement for velocity-based effects")]
    private PlayerShipMovement shipMovement = null;

    [Header("Orbit Settings")]
    [SerializeField] private float orbitDistance = 30f;
    [SerializeField] private float minOrbitDistance = 10f;
    [SerializeField] private float maxOrbitDistance = 100f;
    [SerializeField] private float zoomSpeed = 20f;
    [SerializeField] private float zoomSmoothness = 8f;
    private float targetOrbitDistance;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadSensitivity = 100f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Follow Settings")]
    [SerializeField] private float positionSmoothness = 10f;
    [SerializeField] private float rotationSmoothness = 12f;

    [Header("Velocity-Based Effects")]
    [Tooltip("Camera pulls back when moving fast")]
    [SerializeField] private bool enableSpeedZoom = true;
    [SerializeField] private float speedZoomMultiplier = 0.1f;
    [SerializeField] private float maxSpeedZoomOffset = 15f;

    [Tooltip("Camera leads slightly in movement direction")]
    [SerializeField] private bool enableLeadOffset = true;
    [SerializeField] private float leadOffsetAmount = 5f;
    [SerializeField] private float leadOffsetSmoothness = 3f;

    [Tooltip("Subtle camera shake at high speed")]
    [SerializeField] private bool enableSpeedShake = true;
    [SerializeField] private float shakeThreshold = 50f;
    [SerializeField] private float shakeIntensity = 0.1f;

    [Header("Reset View")]
    [SerializeField] private Key resetViewKey = Key.R;
    [SerializeField] private float resetSpeed = 5f;
    [Tooltip("Automatically reset view when not looking around")]
    [SerializeField] private bool autoResetView = false;
    [SerializeField] private float autoResetDelay = 3f;
    [SerializeField] private float autoResetSpeed = 2f;

    [Header("Collision")]
    [SerializeField] private bool enableCameraCollision = true;
    [SerializeField] private float collisionRadius = 0.5f;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionSmoothness = 15f;

    [Header("Aiming")]
    [SerializeField] private float aimDistance = 500f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Camera reference
    private Camera cam;

    // Orbit state
    private float orbitYaw = 0f;
    private float orbitPitch = 20f;
    private float currentOrbitDistance;
    private bool isResettingView = false;
    private float timeSinceLookInput = 0f;

    // Smooth follow state
    private Vector3 smoothFollowPosition;
    private Vector3 currentLeadOffset;
    private Quaternion smoothRotation;

    // Collision state
    private float collisionAdjustedDistance;

    public Vector3 MouseAimPos => transform.position + transform.forward * aimDistance;
    public Vector3 BoresightPos => ship != null ? ship.position + ship.forward * aimDistance : MouseAimPos;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            cam = GetComponentInChildren<Camera>();
        }

        if (ship == null)
        {
            Debug.LogError(name + ": PlayerMovementController - No ship transform assigned!");
            return;
        }

        if (shipMovement == null)
        {
            shipMovement = ship.GetComponent<PlayerShipMovement>();
        }

        // Initialize state
        smoothFollowPosition = ship.position;
        currentLeadOffset = Vector3.zero;
        targetOrbitDistance = orbitDistance;
        currentOrbitDistance = orbitDistance;
        collisionAdjustedDistance = orbitDistance;

        // Initialize orbit angles behind the ship
        orbitYaw = ship.eulerAngles.y + 180f;
        orbitPitch = 20f;

        smoothRotation = transform.rotation;

        UpdateCameraTransform();
    }

    private void LateUpdate()
    {
        if (ship == null) return;

        // Skip updates when game is paused
        if (Time.timeScale == 0f) return;

        // Reset NaN values if they occur
        if (float.IsNaN(orbitYaw)) orbitYaw = ship.eulerAngles.y + 180f;
        if (float.IsNaN(orbitPitch)) orbitPitch = 20f;
        if (float.IsNaN(currentOrbitDistance)) currentOrbitDistance = orbitDistance;
        if (float.IsNaN(collisionAdjustedDistance)) collisionAdjustedDistance = orbitDistance;
        if (float.IsNaN(smoothFollowPosition.x)) smoothFollowPosition = ship.position;
        if (float.IsNaN(currentLeadOffset.x)) currentLeadOffset = Vector3.zero;

        HandleInput();
        HandleZoom();
        HandleResetView();
        HandleAutoReset();
        UpdateVelocityEffects();
        HandleCollision();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (isResettingView) return;

        Vector2 lookInput = Vector2.zero;

        // Mouse input
        var mouse = Mouse.current;
        if (mouse != null)
        {
            lookInput.x = mouse.delta.x.ReadValue() * mouseSensitivity;
            lookInput.y = mouse.delta.y.ReadValue() * mouseSensitivity;
        }

        // Gamepad right stick
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            lookInput.x += stick.x * gamepadSensitivity * Time.deltaTime;
            lookInput.y += stick.y * gamepadSensitivity * Time.deltaTime;
        }

        // Track time since input for auto-reset
        if (lookInput.sqrMagnitude > 0.01f)
        {
            timeSinceLookInput = 0f;
        }
        else
        {
            timeSinceLookInput += Time.deltaTime;
        }

        // Apply input to orbit angles
        orbitYaw += lookInput.x;
        orbitPitch += lookInput.y * (invertY ? 1f : -1f);

        // Clamp pitch
        orbitPitch = Mathf.Clamp(orbitPitch, minVerticalAngle, maxVerticalAngle);

        // Wrap yaw
        orbitYaw = Mathf.Repeat(orbitYaw, 360f);
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetOrbitDistance -= scroll * zoomSpeed * Time.deltaTime;
            targetOrbitDistance = Mathf.Clamp(targetOrbitDistance, minOrbitDistance, maxOrbitDistance);
        }

        // Smooth zoom
        currentOrbitDistance = Mathf.Lerp(currentOrbitDistance, targetOrbitDistance, zoomSmoothness * Time.deltaTime);
    }

    private void HandleResetView()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[resetViewKey].isPressed)
        {
            isResettingView = true;
            ResetViewToShipBehind(resetSpeed);
        }
        else
        {
            isResettingView = false;
        }
    }

    private void HandleAutoReset()
    {
        if (!autoResetView || isResettingView) return;

        if (timeSinceLookInput > autoResetDelay)
        {
            ResetViewToShipBehind(autoResetSpeed);
        }
    }

    private void ResetViewToShipBehind(float speed)
    {
        float targetYaw = ship.eulerAngles.y + 180f;
        float targetPitch = 20f;

        orbitYaw = Mathf.LerpAngle(orbitYaw, targetYaw, speed * Time.deltaTime);
        orbitPitch = Mathf.Lerp(orbitPitch, targetPitch, speed * Time.deltaTime);
    }

    private void UpdateVelocityEffects()
    {
        if (shipMovement == null) return;

        Vector3 velocity = shipMovement.Velocity;
        float speed = velocity.magnitude;

        // Speed-based zoom out
        if (enableSpeedZoom)
        {
            float speedZoomOffset = Mathf.Clamp(speed * speedZoomMultiplier, 0f, maxSpeedZoomOffset);
            currentOrbitDistance = Mathf.Lerp(
                currentOrbitDistance,
                targetOrbitDistance + speedZoomOffset,
                zoomSmoothness * Time.deltaTime
            );
        }

        // Lead offset in velocity direction
        if (enableLeadOffset)
        {
            Vector3 targetLead = Vector3.zero;
            if (speed > 1f)
            {
                // Lead in the direction of movement, but in camera-relative space
                Vector3 velocityDir = velocity.normalized;
                targetLead = velocityDir * leadOffsetAmount * Mathf.Clamp01(speed / 50f);
            }
            currentLeadOffset = Vector3.Lerp(currentLeadOffset, targetLead, leadOffsetSmoothness * Time.deltaTime);
        }
    }

    private void HandleCollision()
    {
        if (!enableCameraCollision)
        {
            collisionAdjustedDistance = currentOrbitDistance;
            return;
        }

        // Validate currentOrbitDistance first
        if (float.IsNaN(currentOrbitDistance) || float.IsInfinity(currentOrbitDistance))
        {
            currentOrbitDistance = orbitDistance;
        }

        Vector3 targetPosition = smoothFollowPosition + currentLeadOffset;
        Vector3 directionFromTarget = CalculateOrbitDirection();

        // Validate direction
        if (directionFromTarget.sqrMagnitude < 0.0001f)
        {
            collisionAdjustedDistance = currentOrbitDistance;
            return;
        }

        float desiredDistance = currentOrbitDistance;
        float adjustedDistance = desiredDistance;

        // Raycast from ship to camera position
        if (Physics.SphereCast(
            targetPosition,
            collisionRadius,
            directionFromTarget,
            out RaycastHit hit,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            // Validate hit distance
            if (!float.IsNaN(hit.distance) && !float.IsInfinity(hit.distance))
            {
                adjustedDistance = hit.distance - collisionRadius * 0.5f;
                adjustedDistance = Mathf.Max(adjustedDistance, minOrbitDistance * 0.5f);
            }
        }

        // Validate before Lerp
        if (float.IsNaN(collisionAdjustedDistance) || float.IsInfinity(collisionAdjustedDistance))
        {
            collisionAdjustedDistance = adjustedDistance;
        }
        else
        {
            collisionAdjustedDistance = Mathf.Lerp(
                collisionAdjustedDistance,
                adjustedDistance,
                collisionSmoothness * Time.deltaTime
            );
        }

        // Final validation
        if (float.IsNaN(collisionAdjustedDistance))
        {
            collisionAdjustedDistance = orbitDistance;
        }
    }

    private Vector3 CalculateOrbitDirection()
    {
        float pitchRad = orbitPitch * Mathf.Deg2Rad;
        float yawRad = orbitYaw * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        );
    }

    private void UpdateCameraTransform()
    {
        // Smoothly follow ship position
        smoothFollowPosition = Vector3.Lerp(
            smoothFollowPosition,
            ship.position,
            positionSmoothness * Time.deltaTime
        );

        // Calculate look target with lead offset
        Vector3 lookTarget = smoothFollowPosition + currentLeadOffset;

        // Calculate camera position on orbit sphere
        Vector3 orbitDirection = CalculateOrbitDirection();
        float finalDistance = enableCameraCollision ? collisionAdjustedDistance : currentOrbitDistance;
        Vector3 orbitOffset = orbitDirection * finalDistance;

        // Apply speed shake
        Vector3 shakeOffset = Vector3.zero;
        if (enableSpeedShake && shipMovement != null)
        {
            float speed = shipMovement.Velocity.magnitude;
            if (speed > shakeThreshold)
            {
                float shakeAmount = (speed - shakeThreshold) * shakeIntensity * 0.01f;
                shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 20f, 0f) - 0.5f) * shakeAmount,
                    (Mathf.PerlinNoise(0f, Time.time * 20f) - 0.5f) * shakeAmount,
                    0f
                );
            }
        }

        // Calculate target camera position
        Vector3 targetCameraPosition = lookTarget + orbitOffset + shakeOffset;

        // // Debug: Find the source of NaN
        // if (float.IsNaN(targetCameraPosition.x) || float.IsNaN(targetCameraPosition.y) || float.IsNaN(targetCameraPosition.z))
        // {
        //     Debug.LogWarning($"NaN detected!" +
        //         $"\n  smoothFollowPosition: {smoothFollowPosition}" +
        //         $"\n  currentLeadOffset: {currentLeadOffset}" +
        //         $"\n  lookTarget: {lookTarget}" +
        //         $"\n  orbitDirection: {orbitDirection}" +
        //         $"\n  finalDistance: {finalDistance}" +
        //         $"\n  orbitOffset: {orbitOffset}" +
        //         $"\n  shakeOffset: {shakeOffset}" +
        //         $"\n  orbitYaw: {orbitYaw}" +
        //         $"\n  orbitPitch: {orbitPitch}" +
        //         $"\n  currentOrbitDistance: {currentOrbitDistance}" +
        //         $"\n  collisionAdjustedDistance: {collisionAdjustedDistance}");
        //     return;
        // }

        transform.position = targetCameraPosition;

        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = -ship.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

        // if (float.IsNaN(targetRotation.x) || float.IsNaN(targetRotation.y) || float.IsNaN(targetRotation.z) || float.IsNaN(targetRotation.w))
        // {
        //     Debug.LogWarning("Invalid rotation detected, skipping rotation update");
        //     return;
        // }

        smoothRotation = Quaternion.Slerp(smoothRotation, targetRotation, rotationSmoothness * Time.deltaTime);
        transform.rotation = smoothRotation;
    }

    /// <summary>
    /// Immediately snap camera behind ship
    /// </summary>
    public void SnapBehindShip()
    {
        orbitYaw = ship.eulerAngles.y + 180f;
        orbitPitch = 20f;
        smoothFollowPosition = ship.position;
        currentLeadOffset = Vector3.zero;
        UpdateCameraTransform();
    }

    /// <summary>
    /// Set orbit distance directly
    /// </summary>
    public void SetOrbitDistance(float distance)
    {
        targetOrbitDistance = Mathf.Clamp(distance, minOrbitDistance, maxOrbitDistance);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo || ship == null) return;

        // Draw orbit sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ship.position, currentOrbitDistance);

        // Draw collision-adjusted sphere
        if (enableCameraCollision)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(ship.position, collisionAdjustedDistance);
        }

        // Draw lead offset
        if (enableLeadOffset)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(ship.position, ship.position + currentLeadOffset);
            Gizmos.DrawWireSphere(ship.position + currentLeadOffset, 1f);
        }

        // Draw boresight
        Gizmos.color = Color.white;
        Gizmos.DrawLine(ship.position, BoresightPos);
        Gizmos.DrawWireSphere(BoresightPos, 5f);

        // Draw aim point
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, MouseAimPos);
        Gizmos.DrawWireSphere(MouseAimPos, 5f);
    }
}