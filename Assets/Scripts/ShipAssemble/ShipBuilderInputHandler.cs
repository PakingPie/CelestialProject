using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class ShipBuilderInputHandler : MonoBehaviour
{
    [Header("References")]
    public ShipAssemblyManager assemblyManager;
    public Camera builderCamera;

    [Header("Preview Settings")]
    public Material previewValidMaterial;
    public Material previewInvalidMaterial;

    [Header("State")]
    [SerializeField] private ShipComponentData selectedComponent;
    [SerializeField] private BuildMode currentMode = BuildMode.None;
    [SerializeField] private int rotationStep = 0; // 0, 1, 2, 3 = 0°, 90°, 180°, 270°

    private float PlacementRotationAngle => rotationStep * 90f;
    private GameObject previewObject;
    private AttachmentPoint hoveredPoint;
    private AttachmentPoint lastHoveredPoint;
    private List<AttachmentPoint> highlightedPoints = new List<AttachmentPoint>();

    public enum BuildMode
    {
        None,
        PlacingBody,
        PlacingComponent,
        Removing
    }

    // Events for UI updates
    public event Action<ShipComponentData> OnComponentSelected;
    public event Action OnComponentDeselected;
    public event Action<AttachmentPoint> OnAttachmentPointHovered;
    public event Action OnPlacementComplete;

    void Start()
    {
        if (builderCamera == null)
            builderCamera = Camera.main;
    }

    void Update()
    {
        if (currentMode == BuildMode.None) return;

        HandleRaycast();
        HandleInput();
        UpdatePreview();
    }

    /// <summary>
    /// Select a component to place
    /// </summary>
    public void SelectComponent(ShipComponentData component)
    {
        // Clear previous selection
        DeselectComponent();

        selectedComponent = component;

        if (component.ComponentType == ShipComponentType.Body)
        {
            currentMode = BuildMode.PlacingBody;

            // If no body segments exist, we're placing the first one
            if (assemblyManager.bodySegments.Count == 0)
            {
                HighlightShipRoot();
            }
            else
            {
                HighlightAvailableBodyConnections();
            }
        }
        else
        {
            currentMode = BuildMode.PlacingComponent;
            HighlightAvailableAttachmentPoints(component.ComponentType);
        }

        CreatePreviewObject();
        OnComponentSelected?.Invoke(component);
    }

    /// <summary>
    /// Deselect current component
    /// </summary>
    public void DeselectComponent()
    {
        selectedComponent = null;
        currentMode = BuildMode.None;
        rotationStep = 0;

        ClearHighlights();
        DestroyPreview();

        OnComponentDeselected?.Invoke();
    }

    /// <summary>
    /// Enter remove mode
    /// </summary>
    public void EnterRemoveMode()
    {
        DeselectComponent();
        currentMode = BuildMode.Removing;
    }

    [Header("Detection Settings")]
    public float attachmentPointScreenRadius = 30f; // Pixel radius for detection

    private void HandleRaycast()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        AttachmentPoint closestPoint = null;
        float closestDistance = attachmentPointScreenRadius; // Max detection radius in pixels

        // Get all available attachment points based on current mode
        List<AttachmentPoint> pointsToCheck = GetRelevantAttachmentPoints();

        foreach (var point in pointsToCheck)
        {
            if (point == null || point.isOccupied) continue;

            // Convert world position to screen position
            Vector3 screenPos = builderCamera.WorldToScreenPoint(point.transform.position);

            // Skip if behind camera
            if (screenPos.z < 0) continue;

            // Calculate distance in screen space (2D)
            float distance = Vector2.Distance(mousePos, new Vector2(screenPos.x, screenPos.y));

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        // Update hovered point
        if (closestPoint != hoveredPoint)
        {
            // Unhover previous
            if (hoveredPoint != null)
                hoveredPoint.SetHovered(false);

            hoveredPoint = closestPoint;

            // Hover new
            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(true);
                OnAttachmentPointHovered?.Invoke(hoveredPoint);
            }
        }
    }

    private List<AttachmentPoint> GetRelevantAttachmentPoints()
    {
        List<AttachmentPoint> points = new List<AttachmentPoint>();

        if (currentMode == BuildMode.PlacingBody)
        {
            // Get all available body connection points
            points.AddRange(assemblyManager.GetAllAvailableBodyConnections());
        }
        else if (currentMode == BuildMode.PlacingComponent && selectedComponent != null)
        {
            // Get all points that accept this component type
            points.AddRange(assemblyManager.GetAllAttachmentPointsForType(selectedComponent.ComponentType));
        }
        else if (currentMode == BuildMode.Removing)
        {
            // For removing, we might want all occupied points
            foreach (var segment in assemblyManager.bodySegments)
            {
                foreach (var point in segment.AttachmentPoints)
                {
                    if (point.isOccupied)
                        points.Add(point);
                }
            }
        }

        return points;
    }

    private void HandleInput()
    {
        // Left click to place
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceComponent();
        }

        // Right click to cancel
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            DeselectComponent();
        }

        // Escape to cancel
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeselectComponent();
        }

        // R to rotate clockwise
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            RotatePlacement(1);
        }

        // Q to rotate counter-clockwise (optional)
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            RotatePlacement(-1);
        }

        // Scroll wheel to rotate (optional alternative)
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            RotatePlacement(scroll > 0 ? 1 : -1);
        }
    }

    private void RotatePlacement(int direction)
    {
        if (currentMode == BuildMode.None || selectedComponent == null) return;

        // Only allow rotation for non-body components (optional: you can allow for bodies too)
        if (currentMode == BuildMode.PlacingBody) return;

        rotationStep = (rotationStep + direction + 4) % 4; // Keep in range 0-3

        // Debug.Log($"Rotation: {PlacementRotationAngle}°");
    }

    private void TryPlaceComponent()
    {
        if (selectedComponent == null) return;

        Debug.Log($"TryPlaceComponent: Mode={currentMode}, " +
                  $"ComponentType={selectedComponent.ComponentType}, " +
                  $"Rotation={PlacementRotationAngle}°");

        // Special case: placing first body segment
        if (currentMode == BuildMode.PlacingBody && assemblyManager.bodySegments.Count == 0)
        {
            assemblyManager.AddInitialBodySegment(selectedComponent);
            OnPlacementComplete?.Invoke();

            ClearHighlights();
            HighlightAvailableBodyConnections();

            DestroyPreview();
            CreatePreviewObject();
            return;
        }

        if (hoveredPoint == null || hoveredPoint.isOccupied)
        {
            Debug.Log("No valid attachment point hovered.");
            return;
        }

        if (currentMode == BuildMode.PlacingBody)
        {
            if (hoveredPoint.CanAccept(ShipComponentType.Body) || IsBodyConnectionPoint(hoveredPoint))
            {
                assemblyManager.AttachBodySegment(selectedComponent, hoveredPoint);
                OnPlacementComplete?.Invoke();

                ClearHighlights();
                HighlightAvailableBodyConnections();

                DestroyPreview();
                CreatePreviewObject();
            }
        }
        else if (currentMode == BuildMode.PlacingComponent)
        {
            if (hoveredPoint.CanAccept(selectedComponent.ComponentType))
            {
                // Pass rotation to assembly manager
                assemblyManager.AttachComponent(selectedComponent, hoveredPoint, PlacementRotationAngle);
                OnPlacementComplete?.Invoke();

                ClearHighlights();
                HighlightAvailableAttachmentPoints(selectedComponent.ComponentType);

                DestroyPreview();
                CreatePreviewObject();

                // Optionally reset rotation after placement, or keep it for next placement
                // rotationStep = 0;
            }
        }
    }

    private bool IsBodyConnectionPoint(AttachmentPoint point)
    {
        // Check if this is one of the 6-direction body connections
        foreach (var segment in assemblyManager.bodySegments)
        {
            if (segment.ForwardConnection == point ||
                segment.BackwardConnection == point ||
                segment.LeftConnection == point ||
                segment.RightConnection == point ||
                segment.TopConnection == point ||
                segment.BottomConnection == point)
            {
                return true;
            }
        }
        return false;
    }

    private void HighlightShipRoot()
    {
        // For first body placement, we might show a ground indicator
        // This is optional - you could also just place at origin
        Debug.Log("Ready to place first body segment. Click anywhere to place.");
    }

    private void HighlightAvailableBodyConnections()
    {
        ClearHighlights();

        var available = assemblyManager.GetAllAvailableBodyConnections();
        foreach (var point in available)
        {
            point.ShowHighlight(true);
            highlightedPoints.Add(point);
        }
    }

    private void HighlightAvailableAttachmentPoints(ShipComponentType type)
    {
        ClearHighlights();

        var available = assemblyManager.GetAllAttachmentPointsForType(type);
        foreach (var point in available)
        {
            point.ShowHighlight(true);
            highlightedPoints.Add(point);
        }

        // Also show invalid points dimmed
        foreach (var segment in assemblyManager.bodySegments)
        {
            foreach (var point in segment.AttachmentPoints)
            {
                if (!available.Contains(point) && !point.isOccupied)
                {
                    point.ShowHighlight(false);
                    highlightedPoints.Add(point);
                }
            }
        }
    }

    private void ClearHighlights()
    {
        foreach (var point in highlightedPoints)
        {
            if (point != null)
                point.HideHighlight();
        }
        highlightedPoints.Clear();
    }

    private void CreatePreviewObject()
    {
        if (selectedComponent == null || selectedComponent.Prefab == null) return;

        previewObject = Instantiate(selectedComponent.Prefab);
        previewObject.name = "PlacementPreview";

        // Disable all functional components
        DisablePreviewFunctionality(previewObject);

        // Apply preview material
        ApplyPreviewMaterial(previewObject, true);

        // Initially hide until we have a valid position
        previewObject.SetActive(false);
    }

    private void DisablePreviewFunctionality(GameObject obj)
    {
        // Disable colliders
        foreach (var col in obj.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        // Disable scripts except transform-related
        foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!(mb is Transform))
                mb.enabled = false;
        }

        // Disable rigidbodies
        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
        }
    }

    private void ApplyPreviewMaterial(GameObject obj, bool isValid)
    {
        Material mat = isValid ? previewValidMaterial : previewInvalidMaterial;

        if (mat == null)
        {
            // Create default preview materials if not assigned
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = isValid ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);

            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
        }

        foreach (var renderer in obj.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] mats = new Material[renderer.materials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = mat;
            }
            renderer.materials = mats;
        }
    }

    private void UpdatePreview()
    {
        if (previewObject == null) return;

        if (hoveredPoint != null && !hoveredPoint.isOccupied && CanPlaceAt(hoveredPoint))
        {
            previewObject.SetActive(true);
            PositionPreviewAt(hoveredPoint);
            ApplyPreviewMaterial(previewObject, true);
        }
        else if (currentMode == BuildMode.PlacingBody && assemblyManager.bodySegments.Count == 0)
        {
            // First body segment - follow mouse on ground plane
            previewObject.SetActive(true);
            PositionPreviewAtMouseGround();
            ApplyPreviewMaterial(previewObject, true);
        }
        else
        {
            previewObject.SetActive(false);
        }
    }

    private bool CanPlaceAt(AttachmentPoint point)
    {
        if (selectedComponent == null) return false;

        if (currentMode == BuildMode.PlacingBody)
        {
            return IsBodyConnectionPoint(point);
        }
        else
        {
            return point.CanAccept(selectedComponent.ComponentType);
        }
    }

    private void PositionPreviewAt(AttachmentPoint point)
    {
        if (currentMode == BuildMode.PlacingBody)
        {
            // Body segment positioning (unchanged)
            ShipComponent previewComponent = previewObject.GetComponent<ShipComponent>();
            if (previewComponent != null)
            {
                AttachmentDirection oppositeDir = AttachmentPoint.GetOppositeDirection(point.direction);
                AttachmentPoint previewConnection = previewComponent.GetBodyConnection(oppositeDir);

                if (previewConnection != null)
                {
                    previewObject.transform.position = Vector3.zero;
                    previewObject.transform.rotation = Quaternion.identity;

                    Vector3 connectionOffset = previewConnection.transform.position;
                    previewObject.transform.position = point.transform.position - connectionOffset;
                }
            }
        }
        else
        {
            // For weapons, engines, bridges - use full rotation alignment with player rotation
            ShipComponent previewComponent = previewObject.GetComponent<ShipComponent>();
            AttachmentPoint componentPoint = null;

            if (previewComponent != null && previewComponent.AttachmentPoints.Length > 0)
            {
                foreach (var p in previewComponent.AttachmentPoints)
                {
                    if (p.direction == AttachmentDirection.Bottom)
                    {
                        componentPoint = p;
                        break;
                    }
                }
                if (componentPoint == null)
                    componentPoint = previewComponent.AttachmentPoints[0];
            }

            if (componentPoint != null)
            {
                // Reset to origin
                previewObject.transform.position = Vector3.zero;
                previewObject.transform.rotation = Quaternion.identity;

                // Get mount point's local orientation
                Vector3 mountForward = componentPoint.transform.forward;
                Vector3 mountUp = componentPoint.transform.up;

                // Get target orientation
                Vector3 targetForward = point.transform.forward;
                Vector3 targetUp = point.transform.up;

                // Calculate base rotation alignment
                Quaternion mountCurrentRotation = Quaternion.LookRotation(mountForward, mountUp);
                Quaternion mountDesiredRotation = Quaternion.LookRotation(-targetForward, targetUp);
                Quaternion baseRotation = mountDesiredRotation * Quaternion.Inverse(mountCurrentRotation);

                // Apply player's rotation offset around the attachment axis (target's forward)
                Quaternion playerRotation = Quaternion.AngleAxis(PlacementRotationAngle, -targetForward);
                previewObject.transform.rotation = playerRotation * baseRotation;

                // Position so attachment points overlap (recalculate after rotation)
                Vector3 offset = componentPoint.transform.position - previewObject.transform.position;
                previewObject.transform.position = point.transform.position - offset;
            }
            else
            {
                previewObject.transform.position = point.transform.position;
                previewObject.transform.rotation = point.transform.rotation;
            }
        }
    }

    private void PositionPreviewAtMouseGround()
    {
        Ray ray = builderCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            previewObject.transform.position = point;
        }
    }

    private void DestroyPreview()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }
    }

    void OnDisable()
    {
        DeselectComponent();
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || currentMode == BuildMode.None) return;

        var points = GetRelevantAttachmentPoints();

        foreach (var point in points)
        {
            if (point == null) continue;

            // Draw sphere at each attachment point
            Gizmos.color = point == hoveredPoint ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(point.transform.position, 0.2f);
        }
    }
}