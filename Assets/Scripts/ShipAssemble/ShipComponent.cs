using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ShipComponent : MonoBehaviour
{
    public ShipComponentData Data;
    public AttachmentPoint[] AttachmentPoints;
    
    // Body connection points (6 directions)
    public AttachmentPoint ForwardConnection;
    public AttachmentPoint BackwardConnection;
    public AttachmentPoint LeftConnection;
    public AttachmentPoint RightConnection;
    public AttachmentPoint TopConnection;
    public AttachmentPoint BottomConnection;
    
    void Awake()
    {
        AttachmentPoints = GetComponentsInChildren<AttachmentPoint>();
    }
    
    /// <summary>
    /// Get the body connection point for a specific direction
    /// </summary>
    public AttachmentPoint GetBodyConnection(AttachmentDirection direction)
    {
        return direction switch
        {
            AttachmentDirection.Forward => ForwardConnection,
            AttachmentDirection.Backward => BackwardConnection,
            AttachmentDirection.Left => LeftConnection,
            AttachmentDirection.Right => RightConnection,
            AttachmentDirection.Top => TopConnection,
            AttachmentDirection.Bottom => BottomConnection,
            _ => null
        };
    }
    
    /// <summary>
    /// Get all available (unoccupied) body connection points
    /// </summary>
    public List<AttachmentPoint> GetAvailableBodyConnections()
    {
        var connections = new List<AttachmentPoint>();
        
        if (ForwardConnection != null && !ForwardConnection.isOccupied)
            connections.Add(ForwardConnection);
        if (BackwardConnection != null && !BackwardConnection.isOccupied)
            connections.Add(BackwardConnection);
        if (LeftConnection != null && !LeftConnection.isOccupied)
            connections.Add(LeftConnection);
        if (RightConnection != null && !RightConnection.isOccupied)
            connections.Add(RightConnection);
        if (TopConnection != null && !TopConnection.isOccupied)
            connections.Add(TopConnection);
        if (BottomConnection != null && !BottomConnection.isOccupied)
            connections.Add(BottomConnection);
            
        return connections;
    }
    
    /// <summary>
    /// Get all attachment points that accept a specific component type
    /// </summary>
    public List<AttachmentPoint> GetAttachmentPointsForType(ShipComponentType type)
    {
        return AttachmentPoints
            .Where(p => !p.isOccupied && p.acceptedTypes.Contains(type))
            .ToList();
    }
}