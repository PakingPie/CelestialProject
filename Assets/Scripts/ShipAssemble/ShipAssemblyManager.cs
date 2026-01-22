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
    public int maxBodySegments = 10;
    public int maxDeckGuns = 100;

    public event System.Action OnShipModified;

    void Awake()
    {
        // Clear any inspector-assigned entries to ensure clean state
        if (bodySegments == null)
            bodySegments = new List<ShipComponent>();
        else
            bodySegments.Clear();

        if (deckGuns == null)
            deckGuns = new List<ShipComponent>();
        else
            deckGuns.Clear();

        currentEngine = null;
        currentBridge = null;
    }

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

        if (!IsHullComponentType(componentData.ComponentType))
        {
            Debug.LogError("Component must be of type Bow, Body, or Stern");
            return null;
        }

        GameObject instance = Instantiate(componentData.Prefab, shipRoot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        ShipComponent component = instance.GetComponent<ShipComponent>();
        component.Data = componentData; // Assign the actual component data used
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

        if (!IsHullComponentType(componentData.ComponentType))
        {
            Debug.LogError("Component must be of type Bow, Body, or Stern");
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
        newComponent.Data = componentData; // Assign the actual component data used

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

        // Step 1: Reset the new component to origin with no rotation
        newTransform.position = Vector3.zero;
        newTransform.rotation = Quaternion.identity;

        // Step 2: Now newPoint.transform.position IS its offset from the component center
        // (because component is at origin with identity rotation)
        Vector3 connectionOffset = newPoint.transform.position;

        // Step 3: Position the component so newPoint aligns with existingPoint
        // newComponent.position + connectionOffset = existingPoint.position
        // Therefore: newComponent.position = existingPoint.position - connectionOffset
        newTransform.position = existingPoint.transform.position - connectionOffset;

        // Debug.Log($"Aligned new body: existingPoint at {existingPoint.transform.position}, " +
        //           $"connectionOffset {connectionOffset}, final position {newTransform.position}");
    }

    /// <summary>
    /// Attach a non-body component (engine, bridge, deck gun) to an attachment point
    /// </summary>
    /// <summary>
    /// Attach a component (weapon, engine, etc.) to an attachment point with optional rotation
    /// </summary>
    public GameObject AttachComponent(ShipComponentData componentData, AttachmentPoint targetPoint, float rotationAngle = 0f)
    {
        if (targetPoint == null || targetPoint.isOccupied)
        {
            Debug.LogWarning("Target point is null or occupied!");
            return null;
        }

        if (!targetPoint.CanAccept(componentData.ComponentType))
        {
            Debug.LogWarning($"Target point doesn't accept {componentData.ComponentType}!");
            return null;
        }

        // Instantiate the component
        GameObject componentObj = Instantiate(componentData.Prefab);
        componentObj.name = componentData.name;

        ShipComponent component = componentObj.GetComponent<ShipComponent>();
        if (component == null)
        {
            Debug.LogError("Component prefab missing ShipComponent!");
            Destroy(componentObj);
            return null;
        }
        component.Data = componentData; // Assign the actual component data used

        // Find the component's mount point (usually Bottom direction)
        AttachmentPoint mountPoint = null;
        foreach (var point in component.AttachmentPoints)
        {
            if (point.direction == AttachmentDirection.Bottom)
            {
                mountPoint = point;
                break;
            }
        }
        if (mountPoint == null && component.AttachmentPoints.Length > 0)
        {
            mountPoint = component.AttachmentPoints[0];
        }

        // Align component to target with rotation
        AlignComponentToTarget(targetPoint, mountPoint, componentObj.transform, rotationAngle);

        // Parent to target's body segment
        componentObj.transform.SetParent(targetPoint.transform.parent, true);

        // Mark point as occupied
        targetPoint.isOccupied = true;
        // Track the component
        targetPoint.attachedComponent = component;

        // Track component in the appropriate list based on type
        switch (componentData.ComponentType)
        {
            case ShipComponentType.Engine:
                currentEngine = component;
                break;
            case ShipComponentType.Bridge:
                currentBridge = component;
                break;
            case ShipComponentType.Weapon:
                deckGuns.Add(component);
                break;
        }

        OnShipModified?.Invoke();
        // Debug.Log($"Attached {componentData.name} to {targetPoint.name} with {rotationAngle}° rotation");

        return componentObj;
    }

    /// <summary>
    /// Align a component with optional rotation offset
    /// </summary>
    private void AlignComponentToTarget(AttachmentPoint targetPoint, AttachmentPoint componentPoint, Transform componentTransform, float rotationAngle = 0f)
    {
        if (componentPoint == null)
        {
            componentTransform.position = targetPoint.transform.position;
            componentTransform.rotation = targetPoint.transform.rotation;
            return;
        }

        // Step 1: Reset component to origin with identity rotation
        componentTransform.position = Vector3.zero;
        componentTransform.rotation = Quaternion.identity;

        // Step 2: Get the mount point's local orientation
        Vector3 mountForward = componentPoint.transform.forward;
        Vector3 mountUp = componentPoint.transform.up;

        // Step 3: Get the target point's orientation
        Vector3 targetForward = targetPoint.transform.forward;
        Vector3 targetUp = targetPoint.transform.up;

        // Step 4: Calculate base rotation alignment
        Quaternion mountCurrentRotation = Quaternion.LookRotation(mountForward, mountUp);
        Quaternion mountDesiredRotation = Quaternion.LookRotation(-targetForward, targetUp);
        Quaternion baseRotation = mountDesiredRotation * Quaternion.Inverse(mountCurrentRotation);

        // Step 5: Apply player's rotation offset around the attachment axis
        Quaternion playerRotation = Quaternion.AngleAxis(rotationAngle, -targetForward);
        componentTransform.rotation = playerRotation * baseRotation;

        // Step 6: Position so attachment points overlap (recalculate after rotation)
        Vector3 componentPointWorldPos = componentPoint.transform.position;
        Vector3 offset = componentPointWorldPos - componentTransform.position;
        componentTransform.position = targetPoint.transform.position - offset;

        // Debug.Log($"Aligned component with {rotationAngle}° rotation");
    }

    /// <summary>
    /// Find the attachment point on a component that should connect to the body
    /// </summary>
    private AttachmentPoint FindComponentMountPoint(ShipComponent component)
    {
        // For non-body components, find the first/primary attachment point
        if (component.AttachmentPoints != null && component.AttachmentPoints.Length > 0)
        {
            // Prefer Bottom attachment point for mounting (most common for weapons/bridges)
            foreach (var point in component.AttachmentPoints)
            {
                if (point.direction == AttachmentDirection.Bottom)
                    return point;
            }
            // Fallback to first attachment point
            return component.AttachmentPoints[0];
        }
        return null;
    }

    /// <summary>
    /// Align a component so its attachment point connects properly to the target point
    /// </summary>
    /// <summary>
    /// Align a component so its attachment point connects properly to the target point
    /// </summary>
    private void AlignComponentToTarget(AttachmentPoint targetPoint, AttachmentPoint componentPoint, Transform componentTransform)
    {
        if (componentPoint == null)
        {
            // No attachment point on component, just place at target position/rotation
            componentTransform.position = targetPoint.transform.position;
            componentTransform.rotation = targetPoint.transform.rotation;
            return;
        }

        // Step 1: Reset component to origin with identity rotation
        componentTransform.position = Vector3.zero;
        componentTransform.rotation = Quaternion.identity;

        // Step 2: Get the mount point's local orientation (when component is at identity)
        Vector3 mountForward = componentPoint.transform.forward;
        Vector3 mountUp = componentPoint.transform.up;

        // Step 3: Get the target point's orientation
        Vector3 targetForward = targetPoint.transform.forward;
        Vector3 targetUp = targetPoint.transform.up;

        // Step 4: Calculate rotation that aligns mount point to face opposite of target
        // Mount forward should face -targetForward, mount up should align with targetUp
        Quaternion mountCurrentRotation = Quaternion.LookRotation(mountForward, mountUp);
        Quaternion mountDesiredRotation = Quaternion.LookRotation(-targetForward, targetUp);

        // Apply the rotation difference to the component
        componentTransform.rotation = mountDesiredRotation * Quaternion.Inverse(mountCurrentRotation);

        // Step 5: Position so attachment points overlap (recalculate after rotation)
        Vector3 componentPointWorldPos = componentPoint.transform.position;
        Vector3 offset = componentPointWorldPos - componentTransform.position;
        componentTransform.position = targetPoint.transform.position - offset;

        // Debug.Log($"Aligned component: mountForward={mountForward}, mountUp={mountUp}, " +
        //           $"targetForward={targetForward}, targetUp={targetUp}, " +
        //           $"finalRotation={componentTransform.rotation.eulerAngles}");
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

    /// <summary>
    /// Check if the component type is a hull segment (Bow, Body, or Stern)
    /// </summary>
    public static bool IsHullComponentType(ShipComponentType type)
    {
        return type == ShipComponentType.Bow || 
               type == ShipComponentType.Body || 
               type == ShipComponentType.Stern;
    }
}