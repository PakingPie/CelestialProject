using UnityEngine;
using System;
using System.Collections.Generic;

public class ShipBuilderInputHandler : MonoBehaviour
{
    [Header("References")]
    public ShipAssemblyManager assemblyManager;
    public Camera builderCamera;
    public LayerMask attachmentPointLayer;
    public LayerMask shipLayer;
    
    [Header("Preview Settings")]
    public Material previewValidMaterial;
    public Material previewInvalidMaterial;
    
    [Header("State")]
    [SerializeField] private ShipComponentData selectedComponent;
    [SerializeField] private BuildMode currentMode = BuildMode.None;
    
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
    
    private void HandleRaycast()
    {
        Ray ray = builderCamera.ScreenPointToRay(Input.mousePosition);
        
        // Check for attachment points
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, attachmentPointLayer))
        {
            AttachmentPoint point = hit.collider.GetComponent<AttachmentPoint>();
            if (point == null)
                point = hit.collider.GetComponentInParent<AttachmentPoint>();
            
            if (point != hoveredPoint)
            {
                // Unhover previous
                if (hoveredPoint != null)
                    hoveredPoint.SetHovered(false);
                
                hoveredPoint = point;
                
                // Hover new
                if (hoveredPoint != null)
                {
                    hoveredPoint.SetHovered(true);
                    OnAttachmentPointHovered?.Invoke(hoveredPoint);
                }
            }
        }
        else
        {
            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }
        }
    }
    
    private void HandleInput()
    {
        // Left click to place
        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceComponent();
        }
        
        // Right click to cancel
        if (Input.GetMouseButtonDown(1))
        {
            DeselectComponent();
        }
        
        // Escape to cancel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            DeselectComponent();
        }
    }
    
    private void TryPlaceComponent()
    {
        if (selectedComponent == null) return;
        
        // Special case: placing first body segment
        if (currentMode == BuildMode.PlacingBody && assemblyManager.bodySegments.Count == 0)
        {
            assemblyManager.AddInitialBodySegment(selectedComponent);
            OnPlacementComplete?.Invoke();
            
            // Continue placing or deselect based on preference
            // DeselectComponent();
            
            // Or allow placing more bodies
            HighlightAvailableBodyConnections();
            return;
        }
        
        // Need a valid hovered point
        if (hoveredPoint == null || hoveredPoint.isOccupied) return;
        
        if (currentMode == BuildMode.PlacingBody)
        {
            if (hoveredPoint.CanAccept(ShipComponentType.Body) || IsBodyConnectionPoint(hoveredPoint))
            {
                assemblyManager.AttachBodySegment(selectedComponent, hoveredPoint);
                OnPlacementComplete?.Invoke();
                
                // Refresh highlights for additional placements
                ClearHighlights();
                HighlightAvailableBodyConnections();
            }
        }
        else if (currentMode == BuildMode.PlacingComponent)
        {
            if (hoveredPoint.CanAccept(selectedComponent.ComponentType))
            {
                assemblyManager.AttachComponent(selectedComponent, hoveredPoint);
                OnPlacementComplete?.Invoke();
                
                // Refresh highlights
                ClearHighlights();
                HighlightAvailableAttachmentPoints(selectedComponent.ComponentType);
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
            // For body segments, calculate proper alignment
            ShipComponent previewComponent = previewObject.GetComponent<ShipComponent>();
            if (previewComponent != null)
            {
                AttachmentDirection oppositeDir = AttachmentPoint.GetOppositeDirection(point.direction);
                AttachmentPoint previewConnection = previewComponent.GetBodyConnection(oppositeDir);
                
                if (previewConnection != null)
                {
                    // Align rotation
                    Vector3 existingForward = point.transform.forward;
                    Vector3 previewPointLocalForward = previewObject.transform.InverseTransformDirection(previewConnection.transform.forward);
                    
                    Quaternion targetRotation = Quaternion.LookRotation(-existingForward, point.transform.up);
                    Quaternion pointLocalRotation = Quaternion.LookRotation(previewPointLocalForward, Vector3.up);
                    
                    previewObject.transform.rotation = targetRotation * Quaternion.Inverse(pointLocalRotation);
                    
                    // Align position
                    Vector3 offset = point.transform.position - previewConnection.transform.position;
                    previewObject.transform.position += offset;
                }
            }
        }
        else
        {
            // For other components, just parent to attachment point
            previewObject.transform.position = point.transform.position;
            previewObject.transform.rotation = point.transform.rotation;
        }
    }
    
    private void PositionPreviewAtMouseGround()
    {
        Ray ray = builderCamera.ScreenPointToRay(Input.mousePosition);
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
}