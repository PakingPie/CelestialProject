using UnityEngine;

// Define component types
public enum ShipComponentType
{
    Body,
    Engine,
    Bridge,
    Weapon
}

// Base component data
[CreateAssetMenu(fileName = "ShipComponent", menuName = "Ship/Component")]
public class ShipComponentData : ScriptableObject
{
    public string ComponentName;
    public ShipComponentType ComponentType;
    public GameObject Prefab;
    public Sprite UiIcon;
    
    // Stats this component contributes
    public float HullPoints;
    public float Weight;
}