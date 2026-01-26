using UnityEngine;
using UnityEngine.InputSystem;

public class ShipBuilderCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = Vector3.zero;

    [Header("Orbit Settings")]
    public float orbitSpeed = 5f;
    public float minVerticalAngle = -80f;
    public float maxVerticalAngle = 80f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minDistance = 3f;
    public float maxDistance = 30f;
    public float defaultDistance = 10f;

    [Header("Pan Settings")]
    public float panSpeed = 0.5f;
    public float maxPanDistance = 20f;

    [Header("Smoothing")]
    public float orbitSmoothing = 10f;
    public float zoomSmoothing = 10f;
    public float panSmoothing = 10f;

    [Header("Input Settings")]
    public bool invertY = false;

    [Header("Auto Rotation")]
    public bool autoRotateWhenIdle = false;
    public float autoRotateSpeed = 10f;
    public float idleTimeBeforeAutoRotate = 3f;

    // Current state
    private float currentHorizontalAngle = 0f;
    private float currentVerticalAngle = 30f;
    private float currentDistance;
    private Vector3 currentPanOffset = Vector3.zero;

    // Target state (for smoothing)
    private float targetHorizontalAngle;
    private float targetVerticalAngle;
    private float targetDistance;
    private Vector3 targetPanOffset;

    // Idle tracking
    private float lastInputTime;
    private bool isUserControlling;

    // Focus animation
    private bool isFocusing;
    private Vector3 focusTargetPosition;
    private float focusTargetDistance;
    private float focusProgress;
    private float focusDuration = 0.5f;
    private Vector3 focusStartPan;
    private float focusStartDistance;

    void Start()
    {
        // Initialize distances
        currentDistance = defaultDistance;
        targetDistance = defaultDistance;

        targetHorizontalAngle = currentHorizontalAngle;
        targetVerticalAngle = currentVerticalAngle;
        targetPanOffset = currentPanOffset;

        lastInputTime = Time.time;

        // If no target set, create one at origin
        if (target == null)
        {
            GameObject targetObj = new GameObject("CameraTarget");
            target = targetObj.transform;
            target.position = Vector3.zero;
        }

        UpdateCameraPosition();
    }

    void Update()
    {
        if (isFocusing)
        {
            UpdateFocusAnimation();
        }
        else
        {
            HandleInput();
            HandleAutoRotation();
        }

        SmoothValues();
        UpdateCameraPosition();
    }

    private void HandleInput()
    {
        bool inputReceived = false;

        // Orbit (Right Mouse Button)
        if (Mouse.current.rightButton.isPressed)
        {
            float horizontal = Mouse.current.delta.ReadValue().x * orbitSpeed;
            float vertical = Mouse.current.delta.ReadValue().y * orbitSpeed * (invertY ? 1f : -1f);

            targetHorizontalAngle += horizontal;
            targetVerticalAngle = Mathf.Clamp(targetVerticalAngle + vertical, minVerticalAngle, maxVerticalAngle);

            inputReceived = true;
        }

        // Pan (Middle Mouse Button)
        if (Mouse.current.middleButton.isPressed)
        {
            float horizontal = -Mouse.current.delta.ReadValue().x * panSpeed;
            float vertical = -Mouse.current.delta.ReadValue().y * panSpeed;

            // Pan relative to camera orientation
            Vector3 right = transform.right * horizontal;
            Vector3 up = transform.up * vertical;

            targetPanOffset += right + up;

            // Clamp pan distance
            if (targetPanOffset.magnitude > maxPanDistance)
            {
                targetPanOffset = targetPanOffset.normalized * maxPanDistance;
            }

            inputReceived = true;
        }

        // Zoom (Scroll Wheel)
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * zoomSpeed * (targetDistance * 0.3f); // Scale zoom by distance
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

            inputReceived = true;
        }

        // Reset view (Home key or double-click middle mouse)
        if (Keyboard.current.homeKey.wasPressedThisFrame)
        {
            ResetView();
            inputReceived = true;
        }

        if (inputReceived)
        {
            lastInputTime = Time.time;
            isUserControlling = true;
        }
        else
        {
            isUserControlling = false;
        }
    }

    private void HandleAutoRotation()
    {
        if (!autoRotateWhenIdle) return;

        if (Time.time - lastInputTime > idleTimeBeforeAutoRotate)
        {
            targetHorizontalAngle += autoRotateSpeed * Time.deltaTime;
        }
    }

    private void SmoothValues()
    {
        float deltaTime = Time.deltaTime;

        currentHorizontalAngle = Mathf.LerpAngle(currentHorizontalAngle, targetHorizontalAngle, orbitSmoothing * deltaTime);
        currentVerticalAngle = Mathf.Lerp(currentVerticalAngle, targetVerticalAngle, orbitSmoothing * deltaTime);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomSmoothing * deltaTime);
        currentPanOffset = Vector3.Lerp(currentPanOffset, targetPanOffset, panSmoothing * deltaTime);
    }

    private void UpdateCameraPosition()
    {
        if (target == null) return;

        // Calculate position on sphere
        float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
        float verticalRad = currentVerticalAngle * Mathf.Deg2Rad;

        Vector3 direction = new Vector3(
            Mathf.Sin(horizontalRad) * Mathf.Cos(verticalRad),
            Mathf.Sin(verticalRad),
            Mathf.Cos(horizontalRad) * Mathf.Cos(verticalRad)
        );

        Vector3 targetPosition = target.position + targetOffset + currentPanOffset;
        Vector3 cameraPosition = targetPosition + direction * currentDistance;

        transform.position = cameraPosition;
        transform.LookAt(targetPosition);
    }

    /// <summary>
    /// Reset camera to default view
    /// </summary>
    public void ResetView()
    {
        targetHorizontalAngle = 0f;
        targetVerticalAngle = 30f;
        targetDistance = defaultDistance;
        targetPanOffset = Vector3.zero;
    }

    /// <summary>
    /// Focus on a specific world position
    /// </summary>
    public void FocusOn(Vector3 position, float distance = -1f)
    {
        if (distance < 0) distance = defaultDistance * 0.5f;

        isFocusing = true;
        focusProgress = 0f;
        focusTargetPosition = position;
        focusTargetDistance = distance;
        focusStartPan = currentPanOffset;
        focusStartDistance = currentDistance;
    }

    /// <summary>
    /// Focus on a specific transform
    /// </summary>
    public void FocusOn(Transform focusTarget, float distance = -1f)
    {
        if (focusTarget == null) return;
        FocusOn(focusTarget.position, distance);
    }

    /// <summary>
    /// Focus on a ship component
    /// </summary>
    public void FocusOnComponent(ShipComponent component, float distance = -1f)
    {
        if (component == null) return;

        // Calculate bounds center if component has renderers
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            // Auto-calculate distance based on bounds
            if (distance < 0)
            {
                distance = bounds.size.magnitude * 1.5f;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }

            FocusOn(bounds.center, distance);
        }
        else
        {
            FocusOn(component.transform.position, distance);
        }
    }

    private void UpdateFocusAnimation()
    {
        focusProgress += Time.deltaTime / focusDuration;

        if (focusProgress >= 1f)
        {
            focusProgress = 1f;
            isFocusing = false;
        }

        // Ease out
        float t = 1f - Mathf.Pow(1f - focusProgress, 3f);

        // Calculate new pan offset
        Vector3 newPanOffset = focusTargetPosition - (target.position + targetOffset);

        currentPanOffset = Vector3.Lerp(focusStartPan, newPanOffset, t);
        targetPanOffset = currentPanOffset;

        currentDistance = Mathf.Lerp(focusStartDistance, focusTargetDistance, t);
        targetDistance = currentDistance;
    }

    /// <summary>
    /// Set the orbit center target
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetPanOffset = Vector3.zero;
        currentPanOffset = Vector3.zero;
    }

    /// <summary>
    /// Frame all objects in view (auto-zoom to fit)
    /// </summary>
    public void FrameAll(Bounds bounds)
    {
        // Calculate required distance to fit bounds
        float boundSize = bounds.size.magnitude;
        float fov = Camera.main.fieldOfView * Mathf.Deg2Rad;
        float requiredDistance = (boundSize * 0.5f) / Mathf.Tan(fov * 0.5f);

        requiredDistance = Mathf.Clamp(requiredDistance * 1.2f, minDistance, maxDistance);

        FocusOn(bounds.center, requiredDistance);
    }

    /// <summary>
    /// Frame the entire ship
    /// </summary>
    public void FrameShip(ShipAssemblyManager assemblyManager)
    {
        if (assemblyManager.bodySegments.Count == 0) return;

        // Calculate combined bounds
        Bounds? combinedBounds = null;

        foreach (var segment in assemblyManager.bodySegments)
        {
            Renderer[] renderers = segment.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (combinedBounds == null)
                    combinedBounds = renderer.bounds;
                else
                    combinedBounds.Value.Encapsulate(renderer.bounds);
            }
        }

        if (combinedBounds.HasValue)
        {
            FrameAll(combinedBounds.Value);
        }
    }

    // Add to ShipBuilderCamera class

    /// <summary>
    /// Set view angles directly
    /// </summary>
    public void SetViewAngles(float horizontal, float vertical)
    {
        targetHorizontalAngle = horizontal;
        targetVerticalAngle = Mathf.Clamp(vertical, minVerticalAngle, maxVerticalAngle);
    }

    /// <summary>
    /// Set zoom distance directly
    /// </summary>
    public void SetZoom(float distance)
    {
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    // Public properties for external access
    public float HorizontalAngle => currentHorizontalAngle;
    public float VerticalAngle => currentVerticalAngle;
    public float Distance => currentDistance;
    public Vector3 PanOffset => currentPanOffset;
    public bool IsUserControlling => isUserControlling;
}