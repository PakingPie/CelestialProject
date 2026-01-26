using UnityEngine;

// Define component types
public enum ShipComponentType
{
    Bow,
    Body,
    Stern,
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
    
    [Header("Defense Stats")]
    public float HullPoints;
    public float ArmorPoints;
    public float ShieldPoints;
    
    [Header("Regeneration Stats")]
    public float HullRegenRate;
    public float ArmorRegenRate;
    public float ShieldRegenRate;
    
    [Header("Physical Stats")]
    public float Weight;
    public float PowerConsumption;
    public float PowerGeneration;
}