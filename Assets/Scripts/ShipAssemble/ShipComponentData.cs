using UnityEngine;

// Define component types
public enum ShipComponentType
{
    Body,
    Engine,
    Bridge,
    DeckGun
}

// Base component data
[CreateAssetMenu(fileName = "ShipComponent", menuName = "Ship/Component")]
public class ShipComponentData : ScriptableObject
{
    public string componentName;
    public ShipComponentType componentType;
    public GameObject prefab;
    public Sprite uiIcon;
    
    // Stats this component contributes
    public float hullPoints;
    public float weight;
}