using UnityEngine;
public class ShipComponent : MonoBehaviour
{
    public ShipComponentData data;
    public AttachmentPoint[] attachmentPoints;
    
    // For body segments that chain together
    public AttachmentPoint forwardConnection;
    public AttachmentPoint aftConnection;
    
    void Awake()
    {
        attachmentPoints = GetComponentsInChildren<AttachmentPoint>();
    }
}