using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ComponentCatalog", menuName = "Ship/Component Catalog")]
public class ShipComponentCatalog : ScriptableObject
{
    public List<ShipComponentData> bowComponents = new List<ShipComponentData>();
    public List<ShipComponentData> bodyComponents = new List<ShipComponentData>();
    public List<ShipComponentData> sternComponents = new List<ShipComponentData>();
    public List<ShipComponentData> engineComponents = new List<ShipComponentData>();
    public List<ShipComponentData> bridgeComponents = new List<ShipComponentData>();
    public List<ShipComponentData> deckGunComponents = new List<ShipComponentData>();
    
    public List<ShipComponentData> GetComponentsByType(ShipComponentType type)
    {
        return type switch
        {
            ShipComponentType.Bow => bowComponents,
            ShipComponentType.Body => bodyComponents,
            ShipComponentType.Stern => sternComponents,
            ShipComponentType.Engine => engineComponents,
            ShipComponentType.Bridge => bridgeComponents,
            ShipComponentType.Weapon => deckGunComponents,
            _ => new List<ShipComponentData>()
        };
    }
    
    public List<ShipComponentData> GetAllComponents()
    {
        var all = new List<ShipComponentData>();
        all.AddRange(bowComponents);
        all.AddRange(bodyComponents);
        all.AddRange(sternComponents);
        all.AddRange(engineComponents);
        all.AddRange(bridgeComponents);
        all.AddRange(deckGunComponents);
        return all;
    }
}