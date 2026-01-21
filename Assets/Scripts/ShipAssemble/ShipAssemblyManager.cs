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
    
    public void AddBodySegment(ShipComponentData componentData)
    {
        if (bodySegments.Count >= maxBodySegments) return;
        
        GameObject instance = Instantiate(componentData.prefab, shipRoot);
        ShipComponent component = instance.GetComponent<ShipComponent>();
        
        // Position based on previous segment
        if (bodySegments.Count > 0)
        {
            ShipComponent lastSegment = bodySegments[^1];
            // Align aft of last segment to forward of new segment
            AlignComponents(lastSegment.aftConnection, component.forwardConnection);
        }
        
        bodySegments.Add(component);
        OnShipModified?.Invoke();
    }
    
    public void AttachComponent(ShipComponentData componentData, AttachmentPoint targetPoint)
    {
        if (targetPoint.isOccupied) return;
        if (!targetPoint.acceptedTypes.Contains(componentData.componentType)) return;
        
        GameObject instance = Instantiate(componentData.prefab, targetPoint.transform);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        
        ShipComponent component = instance.GetComponent<ShipComponent>();
        targetPoint.isOccupied = true;
        targetPoint.attachedComponent = component;
        
        // Track by type
        switch (componentData.componentType)
        {
            case ShipComponentType.Engine:
                currentEngine = component;
                break;
            case ShipComponentType.Bridge:
                currentBridge = component;
                break;
            case ShipComponentType.DeckGun:
                deckGuns.Add(component);
                break;
        }
        
        OnShipModified?.Invoke();
    }
    
    private void AlignComponents(AttachmentPoint from, AttachmentPoint to)
    {
        // Calculate offset to align attachment points
        Vector3 offset = from.transform.position - to.transform.position;
        to.transform.parent.position += offset;
    }
    
    public event System.Action OnShipModified;
}