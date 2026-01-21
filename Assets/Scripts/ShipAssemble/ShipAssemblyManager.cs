using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ShipAssemblyManager : MonoBehaviour
{
    [Header("Current Build")]
    public List<ShipComponent> bodySegments = new List<ShipComponent>();
    public ShipComponent currentEngine;
    public ShipComponent currentBridge;
    public List<ShipComponent> deckGuns = new List<ShipComponent>();
    
    [Header("Build Settings")]
    public Transform shipRoot;
    public int maxBodySegments = 3;
    public int maxDeckGuns = 4;
    
    public event System.Action OnShipModified;
    
    /// <summary>
    /// Add the first body segment (no alignment needed)
    /// </summary>
    public ShipComponent AddInitialBodySegment(ShipComponentData componentData)
    {
        if (bodySegments.Count > 0)
        {
            Debug.LogWarning("Use AttachBodySegment for subsequent segments");
            return null;
        }
        
        if (componentData.ComponentType != ShipComponentType.Body)
        {
            Debug.LogError("Component must be of type Body");
            return null;
        }
        
        GameObject instance = Instantiate(componentData.Prefab, shipRoot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        
        ShipComponent component = instance.GetComponent<ShipComponent>();
        bodySegments.Add(component);
        
        OnShipModified?.Invoke();
        return component;
    }
    
    /// <summary>
    /// Attach a new body segment to an existing one at the specified attachment point
    /// </summary>
    public ShipComponent AttachBodySegment(ShipComponentData componentData, AttachmentPoint targetPoint)
    {
        if (bodySegments.Count >= maxBodySegments)
        {
            Debug.LogWarning("Maximum body segments reached");
            return null;
        }
        
        if (componentData.ComponentType != ShipComponentType.Body)
        {
            Debug.LogError("Component must be of type Body");
            return null;
        }
        
        if (targetPoint == null || targetPoint.isOccupied)
        {
            Debug.LogWarning("Target attachment point is null or occupied");
            return null;
        }
        
        // Instantiate the new segment
        GameObject instance = Instantiate(componentData.Prefab, shipRoot);
        ShipComponent newComponent = instance.GetComponent<ShipComponent>();
        
        // Find the opposite connection point on the new segment
        AttachmentDirection oppositeDir = AttachmentPoint.GetOppositeDirection(targetPoint.direction);
        AttachmentPoint newComponentConnection = newComponent.GetBodyConnection(oppositeDir);
        
        if (newComponentConnection == null)
        {
            Debug.LogError($"New component doesn't have a {oppositeDir} connection point");
            Destroy(instance);
            return null;
        }
        
        // Align the new segment
        AlignBodySegment(targetPoint, newComponentConnection, newComponent);
        
        // Mark both points as occupied and connected
        targetPoint.isOccupied = true;
        targetPoint.attachedComponent = newComponent;
        targetPoint.connectedTo = newComponentConnection;
        
        newComponentConnection.isOccupied = true;
        newComponentConnection.attachedComponent = targetPoint.GetComponentInParent<ShipComponent>();
        newComponentConnection.connectedTo = targetPoint;
        
        bodySegments.Add(newComponent);
        OnShipModified?.Invoke();
        
        return newComponent;
    }
    
    /// <summary>
    /// Align a new body segment to connect with an existing attachment point
    /// </summary>
    private void AlignBodySegment(AttachmentPoint existingPoint, AttachmentPoint newPoint, ShipComponent newComponent)
    {
        Transform newTransform = newComponent.transform;
        
        // Step 1: Rotate so the connection points face each other
        // The new segment's connection point should face opposite to the existing point
        Vector3 existingForward = existingPoint.transform.forward;
        Vector3 newPointLocalForward = newComponent.transform.InverseTransformDirection(newPoint.transform.forward);
        
        // Calculate the rotation needed to make new point face opposite to existing point
        Quaternion targetRotation = Quaternion.LookRotation(-existingForward, existingPoint.transform.up);
        Quaternion pointLocalRotation = Quaternion.LookRotation(newPointLocalForward, Vector3.up);
        
        newTransform.rotation = targetRotation * Quaternion.Inverse(pointLocalRotation);
        
        // Step 2: Position so the connection points overlap
        Vector3 offset = existingPoint.transform.position - newPoint.transform.position;
        newTransform.position += offset;
    }
    
    /// <summary>
    /// Attach a non-body component (engine, bridge, deck gun) to an attachment point
    /// </summary>
    public ShipComponent AttachComponent(ShipComponentData componentData, AttachmentPoint targetPoint)
    {
        if (targetPoint.isOccupied)
        {
            Debug.LogWarning("Target point is already occupied");
            return null;
        }
        
        if (!targetPoint.acceptedTypes.Contains(componentData.ComponentType))
        {
            Debug.LogWarning($"Target point doesn't accept {componentData.ComponentType}");
            return null;
        }
        
        // Check limits
        if (componentData.ComponentType == ShipComponentType.DeckGun && deckGuns.Count >= maxDeckGuns)
        {
            Debug.LogWarning("Maximum deck guns reached");
            return null;
        }
        
        GameObject instance = Instantiate(componentData.Prefab, targetPoint.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        
        ShipComponent component = instance.GetComponent<ShipComponent>();
        targetPoint.isOccupied = true;
        targetPoint.attachedComponent = component;
        
        switch (componentData.ComponentType)
        {
            case ShipComponentType.Engine:
                // Remove existing engine if any
                if (currentEngine != null)
                    RemoveComponent(currentEngine);
                currentEngine = component;
                break;
            case ShipComponentType.Bridge:
                // Remove existing bridge if any
                if (currentBridge != null)
                    RemoveComponent(currentBridge);
                currentBridge = component;
                break;
            case ShipComponentType.DeckGun:
                deckGuns.Add(component);
                break;
        }
        
        OnShipModified?.Invoke();
        return component;
    }
    
    /// <summary>
    /// Remove a component from the ship
    /// </summary>
    public void RemoveComponent(ShipComponent component)
    {
        if (component == null) return;
        
        // Find and clear the attachment point
        var allPoints = GetAllAttachmentPoints();
        foreach (var point in allPoints)
        {
            if (point.attachedComponent == component)
            {
                point.isOccupied = false;
                point.attachedComponent = null;
                point.connectedTo = null;
            }
        }
        
        // Remove from tracking lists
        if (component == currentEngine) currentEngine = null;
        if (component == currentBridge) currentBridge = null;
        deckGuns.Remove(component);
        bodySegments.Remove(component);
        
        Destroy(component.gameObject);
        OnShipModified?.Invoke();
    }
    
    /// <summary>
    /// Get all available attachment points across all body segments
    /// </summary>
    public List<AttachmentPoint> GetAllAvailableBodyConnections()
    {
        var points = new List<AttachmentPoint>();
        foreach (var segment in bodySegments)
        {
            points.AddRange(segment.GetAvailableBodyConnections());
        }
        return points;
    }
    
    /// <summary>
    /// Get all attachment points that accept a specific component type
    /// </summary>
    public List<AttachmentPoint> GetAllAttachmentPointsForType(ShipComponentType type)
    {
        var points = new List<AttachmentPoint>();
        foreach (var segment in bodySegments)
        {
            points.AddRange(segment.GetAttachmentPointsForType(type));
        }
        return points;
    }
    
    private List<AttachmentPoint> GetAllAttachmentPoints()
    {
        var points = new List<AttachmentPoint>();
        foreach (var segment in bodySegments)
        {
            points.AddRange(segment.AttachmentPoints);
        }
        return points;
    }
    
    /// <summary>
    /// Clear the entire ship build
    /// </summary>
    public void ClearShip()
    {
        foreach (var segment in bodySegments.ToList())
        {
            if (segment != null)
                Destroy(segment.gameObject);
        }
        
        bodySegments.Clear();
        deckGuns.Clear();
        currentEngine = null;
        currentBridge = null;
        
        OnShipModified?.Invoke();
    }
}