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

    [Header("Orbit Settings")]
    [SerializeField] private float orbitDistance = 30f;
    [SerializeField] private float minOrbitDistance = 10f;
    [SerializeField] private float maxOrbitDistance = 100f;
    [SerializeField] private float zoomSpeed = 20f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gamepadSensitivity = 100f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;

    [Header("Follow Settings")]
    [SerializeField] private float followSmoothness = 10f;

    [Header("Reset View")]
    [SerializeField] private Key resetViewKey = Key.R;
    [SerializeField] private float resetSpeed = 5f;

    [Header("Aiming")]
    [SerializeField] private float aimDistance = 500f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Camera reference (this transform IS the camera)
    private Camera cam;

    // Orbit state
    private float orbitYaw = 0f;
    private float orbitPitch = 20f;
    private bool isResettingView = false;
    private Vector3 smoothFollowPosition;

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

        // Initialize position
        smoothFollowPosition = ship.position;

        // Initialize orbit angles to start behind the ship
        orbitYaw = ship.eulerAngles.y + 180f;
        orbitPitch = 20f;

        // Apply initial position
        UpdateCameraTransform();
    }

    private void LateUpdate()
    {
        if (ship == null) return;

        HandleInput();
        HandleZoom();
        HandleResetView();
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

        // Apply input to orbit angles
        orbitYaw += lookInput.x;
        orbitPitch += lookInput.y * (invertY ? 1f : -1f);

        // Clamp pitch
        orbitPitch = Mathf.Clamp(orbitPitch, minVerticalAngle, maxVerticalAngle);

        // Wrap yaw
        if (orbitYaw > 360f) orbitYaw -= 360f;
        if (orbitYaw < 0f) orbitYaw += 360f;
    }

    private void HandleZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.y.ReadValue();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            orbitDistance -= scroll * zoomSpeed * Time.deltaTime;
            orbitDistance = Mathf.Clamp(orbitDistance, minOrbitDistance, maxOrbitDistance);
        }
    }

    private void HandleResetView()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Hold R to reset view behind ship
        if (kb[resetViewKey].isPressed)
        {
            isResettingView = true;

            float targetYaw = ship.eulerAngles.y + 180f;
            float targetPitch = 20f;

            orbitYaw = Mathf.LerpAngle(orbitYaw, targetYaw, resetSpeed * Time.deltaTime);
            orbitPitch = Mathf.Lerp(orbitPitch, targetPitch, resetSpeed * Time.deltaTime);
        }
        else
        {
            isResettingView = false;
        }
    }

    private void UpdateCameraTransform()
    {
        // Smoothly follow ship position
        smoothFollowPosition = Vector3.Lerp(
            smoothFollowPosition,
            ship.position,
            followSmoothness * Time.deltaTime
        );

        // Calculate camera position on orbit sphere
        float pitchRad = orbitPitch * Mathf.Deg2Rad;
        float yawRad = orbitYaw * Mathf.Deg2Rad;

        Vector3 orbitOffset = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ) * orbitDistance;

        // Position camera
        transform.position = smoothFollowPosition + orbitOffset;

        // Look at ship
        transform.LookAt(smoothFollowPosition);
    }

    private void OnDrawGizmos()
    {
        if (!showDebugInfo || ship == null) return;

        // Draw orbit sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ship.position, orbitDistance);

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