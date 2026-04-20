using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraMode
{
    FreeOrbit,      // Full manual orbit control (capital ships)
    FollowBehind    // Auto-follows behind ship, right-click to freelook (fighters)
}

/// <summary>
/// Orbit camera controller for ship. Camera orbits around the ship
/// while the ship moves independently via keyboard.
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    [Header("Camera Mode")]
    [SerializeField]
    [Tooltip("FreeOrbit: full manual orbit (capital ships). FollowBehind: auto-follows ship rear, right-click to freelook (fighters).")]
    private CameraMode cameraMode = CameraMode.FollowBehind;

    [Header("Follow Behind Settings")]
    [Tooltip("How quickly camera returns behind ship (higher = snappier)")]
    [SerializeField] private float followBehindSpeed = 6f;
    [Tooltip("Default pitch angle when following behind ship")]
    [SerializeField] private float followBehindPitch = 15f;

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
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float zoomSmoothness = 12f;
    private float targetOrbitDistance;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 0.15f;
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
    [SerializeField] private float speedZoomMultiplier = 0.05f;
    [SerializeField] private float maxSpeedZoomOffset = 10f;
    [SerializeField] private float speedZoomSmoothness = 3f;

    [Tooltip("Camera leads slightly in movement direction")]
    [SerializeField] private bool enableLeadOffset = true;
    [SerializeField] private float leadOffsetAmount = 3f;
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
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionSmoothness = 20f;
    [SerializeField] private float collisionPadding = 1f;
    [Tooltip("Layers to ignore for collision (set your Player layer here)")]
    [SerializeField] private LayerMask ignoreCollisionLayers;

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

    // Speed zoom state (separate from manual zoom)
    private float currentSpeedZoomOffset = 0f;

    // Freelook state (FollowBehind mode)
    private bool isFreelooking = false;

    public Vector3 MouseAimPos => transform.position + transform.forward * aimDistance;
    public Vector3 BoresightPos => ship != null ? ship.position + ship.forward * aimDistance : MouseAimPos;
    public bool IsFreelooking => isFreelooking;

    /// <summary>
    /// World position where the mouse cursor is pointing, projected from the camera.
    /// </summary>
    public Vector3 GetMouseAimWorldPosition()
    {
        if (cam == null || Mouse.current == null)
            return ship != null ? ship.position + ship.forward * aimDistance : transform.position + transform.forward * aimDistance;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mouseScreenPos);
        return ray.GetPoint(aimDistance);
    }

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

        // Auto-setup ignore layers to include player
        SetupCollisionLayers();

        // Initialize state
        smoothFollowPosition = ship.position;
        currentLeadOffset = Vector3.zero;
        targetOrbitDistance = orbitDistance;
        currentOrbitDistance = orbitDistance;
        collisionAdjustedDistance = orbitDistance;

        // Initialize orbit angles behind the ship
        orbitYaw = ship.eulerAngles.y;
        orbitPitch = 20f;

        smoothRotation = transform.rotation;

        UpdateCameraTransform();
    }

    private void SetupCollisionLayers()
    {
        // Automatically exclude the player's layer from collision checks
        if (ship != null)
        {
            int playerLayer = ship.gameObject.layer;
            ignoreCollisionLayers |= (1 << playerLayer);
        }

        // Remove ignored layers from collision layers
        collisionLayers &= ~ignoreCollisionLayers;
    }

    private void LateUpdate()
    {
        if (ship == null) return;

        // Skip updates when game is paused
        if (Time.timeScale == 0f) return;

        // Reset NaN values if they occur
        ValidateAndResetNaN();

        HandleInput();
        HandleZoom();
        HandleResetView();
        HandleAutoReset();
        UpdateVelocityEffects();
        HandleCollision();
        UpdateCameraTransform();
    }

    private void ValidateAndResetNaN()
    {
        if (float.IsNaN(orbitYaw)) orbitYaw = ship.eulerAngles.y;
        if (float.IsNaN(orbitPitch)) orbitPitch = 20f;
        if (float.IsNaN(currentOrbitDistance)) currentOrbitDistance = orbitDistance;
        if (float.IsNaN(collisionAdjustedDistance)) collisionAdjustedDistance = orbitDistance;
        if (float.IsNaN(smoothFollowPosition.x)) smoothFollowPosition = ship.position;
        if (float.IsNaN(currentLeadOffset.x)) currentLeadOffset = Vector3.zero;
        if (float.IsNaN(currentSpeedZoomOffset)) currentSpeedZoomOffset = 0f;
    }

    private void HandleInput()
    {
        if (isResettingView) return;

        // In FollowBehind mode, only accept look input when right mouse is held
        if (cameraMode == CameraMode.FollowBehind)
        {
            var mouse = Mouse.current;
            isFreelooking = mouse != null && mouse.rightButton.isPressed;

            if (!isFreelooking)
            {
                // Not freelooking — auto-follow handles orbit angles
                timeSinceLookInput += Time.deltaTime;
                return;
            }
        }

        Vector2 lookInput = Vector2.zero;

        // Mouse input
        var mouseDev = Mouse.current;
        if (mouseDev != null)
        {
            lookInput.x = mouseDev.delta.x.ReadValue() * mouseSensitivity;
            lookInput.y = mouseDev.delta.y.ReadValue() * mouseSensitivity;
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
            // Scroll is already a delta, don't multiply by Time.deltaTime
            // Normalize scroll value (it can be large like 120)
            float normalizedScroll = Mathf.Clamp(scroll / 120f, -1f, 1f);
            targetOrbitDistance -= normalizedScroll * zoomSpeed;
            targetOrbitDistance = Mathf.Clamp(targetOrbitDistance, minOrbitDistance, maxOrbitDistance);
        }

        // Smooth zoom (base distance without speed effects)
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
        // In FollowBehind mode, always auto-follow when not freelooking
        if (cameraMode == CameraMode.FollowBehind)
        {
            if (!isFreelooking && !isResettingView)
            {
                ResetViewToShipBehind(followBehindSpeed);
            }
            return;
        }

        // FreeOrbit mode: optional auto-reset after delay
        if (!autoResetView || isResettingView) return;

        if (timeSinceLookInput > autoResetDelay)
        {
            ResetViewToShipBehind(autoResetSpeed);
        }
    }

    private void ResetViewToShipBehind(float speed)
    {
        float targetYaw = ship.eulerAngles.y;
        float targetPitch = cameraMode == CameraMode.FollowBehind ? followBehindPitch : 20f;

        orbitYaw = Mathf.LerpAngle(orbitYaw, targetYaw, speed * Time.deltaTime);
        orbitPitch = Mathf.Lerp(orbitPitch, targetPitch, speed * Time.deltaTime);
    }

    private void UpdateVelocityEffects()
    {
        if (shipMovement == null) return;

        Vector3 velocity = shipMovement.Velocity;
        float speed = velocity.magnitude;

        // Speed-based zoom offset (separate from base distance)
        if (enableSpeedZoom)
        {
            float targetSpeedZoom = Mathf.Clamp(speed * speedZoomMultiplier, 0f, maxSpeedZoomOffset);
            currentSpeedZoomOffset = Mathf.Lerp(currentSpeedZoomOffset, targetSpeedZoom, speedZoomSmoothness * Time.deltaTime);
        }
        else
        {
            currentSpeedZoomOffset = 0f;
        }

        // Lead offset in velocity direction
        if (enableLeadOffset)
        {
            Vector3 targetLead = Vector3.zero;
            if (speed > 1f)
            {
                Vector3 velocityDir = velocity.normalized;
                targetLead = velocityDir * leadOffsetAmount * Mathf.Clamp01(speed / 50f);
            }
            currentLeadOffset = Vector3.Lerp(currentLeadOffset, targetLead, leadOffsetSmoothness * Time.deltaTime);
        }
    }

    private void HandleCollision()
    {
        float desiredDistance = currentOrbitDistance + currentSpeedZoomOffset;

        if (!enableCameraCollision)
        {
            collisionAdjustedDistance = desiredDistance;
            return;
        }

        Vector3 targetPosition = smoothFollowPosition + currentLeadOffset;
        Vector3 directionFromTarget = CalculateOrbitDirection();

        if (directionFromTarget.sqrMagnitude < 0.0001f)
        {
            collisionAdjustedDistance = desiredDistance;
            return;
        }

        float adjustedDistance = desiredDistance;

        RaycastHit[] hits = Physics.SphereCastAll(
            targetPosition,
            collisionRadius,
            directionFromTarget,
            desiredDistance + collisionPadding,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        float closestValidHit = desiredDistance;

        foreach (var hit in hits)
        {
            // Skip anything that's part of the player ship
            if (hit.transform == ship || hit.transform.IsChildOf(ship))
                continue;

            if (hit.distance < closestValidHit)
                closestValidHit = hit.distance;
        }

        if (closestValidHit < desiredDistance)
        {
            adjustedDistance = closestValidHit - collisionPadding;
            adjustedDistance = Mathf.Max(adjustedDistance, minOrbitDistance * 0.5f);
        }

        // Asymmetric smoothing
        float smoothSpeed = adjustedDistance < collisionAdjustedDistance
            ? collisionSmoothness * 2f
            : collisionSmoothness * 0.5f;

        collisionAdjustedDistance = Mathf.Lerp(
            collisionAdjustedDistance,
            adjustedDistance,
            smoothSpeed * Time.deltaTime
        );
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
        float finalDistance = collisionAdjustedDistance;
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

        transform.position = targetCameraPosition;

        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = -ship.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);

        smoothRotation = Quaternion.Slerp(smoothRotation, targetRotation, rotationSmoothness * Time.deltaTime);
        transform.rotation = smoothRotation;
    }

    /// <summary>
    /// Immediately snap camera behind ship
    /// </summary>
    public void SnapBehindShip()
    {
        orbitYaw = ship.eulerAngles.y;
        orbitPitch = 20f;
        smoothFollowPosition = ship.position;
        currentLeadOffset = Vector3.zero;
        currentSpeedZoomOffset = 0f;
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

        // Draw base orbit sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ship.position, currentOrbitDistance);

        // Draw orbit with speed zoom
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(ship.position, currentOrbitDistance + currentSpeedZoomOffset);

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
    }
}