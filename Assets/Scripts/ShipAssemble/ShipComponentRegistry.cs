using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ComponentRegistry", menuName = "Ship/Component Registry")]
public class ShipComponentRegistry : ScriptableObject
{
    [SerializeField]
    private List<ShipComponentData> allComponents = new List<ShipComponentData>();
    
    private Dictionary<string, ShipComponentData> componentLookup;
    
    public void Initialize()
    {
        componentLookup = new Dictionary<string, ShipComponentData>();
        
        foreach (var component in allComponents)
        {
            if (component != null && !string.IsNullOrEmpty(component.name))
            {
                componentLookup[component.name] = component;
            }
        }
    }
    
    public ShipComponentData GetComponentById(string id)
    {
        if (componentLookup == null)
            Initialize();
        
        if (componentLookup.TryGetValue(id, out ShipComponentData data))
            return data;
        
        Debug.LogWarning($"Component not found in registry: {id}");
        return null;
    }
    
    public string GetComponentId(ShipComponentData data)
    {
        return data != null ? data.name : null;
    }
    
    public void RegisterComponent(ShipComponentData component)
    {
        if (!allComponents.Contains(component))
        {
            allComponents.Add(component);
            
            if (componentLookup != null)
                componentLookup[component.name] = component;
        }
    }
    
#if UNITY_EDITOR
    [ContextMenu("Auto-Populate From Catalog")]
    private void AutoPopulateFromCatalog()
    {
        // Find all ShipComponentData assets in the project
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ShipComponentData");
        
        allComponents.Clear();
        
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ShipComponentData data = UnityEditor.AssetDatabase.LoadAssetAtPath<ShipComponentData>(path);
            
            if (data != null)
                allComponents.Add(data);
        }
        
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"Registered {allComponents.Count} components");
    }
#endif
}