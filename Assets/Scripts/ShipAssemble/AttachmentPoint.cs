using UnityEngine;

public enum AttachmentDirection
{
    Forward,
    Backward,
    Left,
    Right,
    Top,
    Bottom
}

public class AttachmentPoint : MonoBehaviour
{
    [Header("Configuration")]
    public ShipComponentType[] acceptedTypes;
    public AttachmentDirection direction;
    
    [Header("State")]
    public bool isOccupied;
    public ShipComponent attachedComponent;
    public AttachmentPoint connectedTo;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject highlightObject;
    [SerializeField] private MeshRenderer highlightRenderer;
    
    // Materials for different states
    private Material validMaterial;
    private Material invalidMaterial;
    private Material hoverMaterial;
    
    private bool isHighlighted;
    private bool isHovered;
    
    void Awake()
    {
        CreateHighlightVisual();
    }
    
    private void CreateHighlightVisual()
    {
        // Create highlight sphere if not assigned
        if (highlightObject == null)
        {
            highlightObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            highlightObject.name = "HighlightVisual";
            highlightObject.transform.SetParent(transform);
            highlightObject.transform.localPosition = Vector3.zero;
            highlightObject.transform.localScale = Vector3.one * 0.5f;
            
            // Remove collider from visual
            Destroy(highlightObject.GetComponent<Collider>());
            
            highlightRenderer = highlightObject.GetComponent<MeshRenderer>();
        }
        
        // Create materials
        validMaterial = CreateMaterial(new Color(0f, 1f, 0f, 0.5f));
        invalidMaterial = CreateMaterial(new Color(1f, 0f, 0f, 0.5f));
        hoverMaterial = CreateMaterial(new Color(1f, 1f, 0f, 0.7f));
        
        highlightObject.SetActive(false);
    }
    
    private Material CreateMaterial(Color color)
    {
        // Using URP Lit shader - adjust if using different render pipeline
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        
        // Fallback for Built-in RP
        if (mat.shader == null)
            mat = new Material(Shader.Find("Standard"));
        
        mat.color = color;
        
        // Enable transparency
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0); // Alpha
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        
        return mat;
    }
    
    /// <summary>
    /// Show highlight indicating if a component can be attached here
    /// </summary>
    public void ShowHighlight(bool canAttach)
    {
        if (highlightObject == null) return;
        
        isHighlighted = true;
        highlightObject.SetActive(true);
        highlightRenderer.material = canAttach ? validMaterial : invalidMaterial;
    }
    
    /// <summary>
    /// Show hover state (mouse over)
    /// </summary>
    public void SetHovered(bool hovered)
    {
        if (highlightObject == null) return;
        
        isHovered = hovered;
        
        if (hovered && isHighlighted)
        {
            highlightRenderer.material = hoverMaterial;
            highlightObject.transform.localScale = Vector3.one * 0.6f; // Slightly larger
        }
        else if (isHighlighted)
        {
            highlightObject.transform.localScale = Vector3.one * 0.5f;
        }
    }
    
    /// <summary>
    /// Hide the highlight
    /// </summary>
    public void HideHighlight()
    {
        if (highlightObject == null) return;
        
        isHighlighted = false;
        isHovered = false;
        highlightObject.SetActive(false);
    }
    
    /// <summary>
    /// Check if this point can accept the given component type
    /// </summary>
    public bool CanAccept(ShipComponentType type)
    {
        if (isOccupied) return false;
        
        foreach (var acceptedType in acceptedTypes)
        {
            if (acceptedType == type) return true;
        }
        return false;
    }
    
    public static AttachmentDirection GetOppositeDirection(AttachmentDirection dir)
    {
        return dir switch
        {
            AttachmentDirection.Forward => AttachmentDirection.Backward,
            AttachmentDirection.Backward => AttachmentDirection.Forward,
            AttachmentDirection.Left => AttachmentDirection.Right,
            AttachmentDirection.Right => AttachmentDirection.Left,
            AttachmentDirection.Top => AttachmentDirection.Bottom,
            AttachmentDirection.Bottom => AttachmentDirection.Top,
            _ => dir
        };
    }

    void OnDrawGizmos()
    {
        Gizmos.color = direction switch
        {
            AttachmentDirection.Forward => Color.blue,
            AttachmentDirection.Backward => Color.blue * 0.5f,
            AttachmentDirection.Left => Color.red,
            AttachmentDirection.Right => Color.red * 0.5f,
            AttachmentDirection.Top => Color.green,
            AttachmentDirection.Bottom => Color.green * 0.5f,
            _ => Color.cyan
        };
        
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.5f);
    }
}