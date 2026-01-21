using UnityEngine;
public class AttachmentPoint : MonoBehaviour
{
    public ShipComponentType[] acceptedTypes; // What can attach here
    public bool isOccupied;
    public ShipComponent attachedComponent;
    
    // Visual feedback
    public void Highlight(bool canAttach)
    {
        // Show green/red indicator
    }
    
    void OnDrawGizmos()
    {
        // Visualize in editor
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}