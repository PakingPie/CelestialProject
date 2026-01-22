using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class ShipSaveLoadManager : MonoBehaviour
{
    [Header("References")]
    public ShipAssemblyManager assemblyManager;
    public ShipComponentRegistry componentRegistry;
    
    [Header("Save Settings")]
    public string saveFolder = "ShipSaves";
    public string fileExtension = ".ship";
    
    [Header("Thumbnail Settings")]
    public Camera thumbnailCamera;
    public int thumbnailWidth = 256;
    public int thumbnailHeight = 256;
    
    private string SavePath => Path.Combine(Application.persistentDataPath, saveFolder);
    
    public event Action<string> OnSaveComplete;
    public event Action<string> OnLoadComplete;
    public event Action<string> OnError;
    
    void Awake()
    {
        // Ensure save directory exists
        if (!Directory.Exists(SavePath))
        {
            Directory.CreateDirectory(SavePath);
        }
        
        componentRegistry?.Initialize();
    }
    
    /// <summary>
    /// Save the current ship configuration
    /// </summary>
    public bool SaveShip(string shipName)
    {
        try
        {
            ShipSaveData saveData = CreateSaveData(shipName);
            string json = JsonUtility.ToJson(saveData, true);
            
            string fileName = SanitizeFileName(shipName) + fileExtension;
            string filePath = Path.Combine(SavePath, fileName);
            
            File.WriteAllText(filePath, json);
            
            Debug.Log($"Ship saved to: {filePath}");
            OnSaveComplete?.Invoke(shipName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save ship: {e.Message}");
            OnError?.Invoke(e.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Load a ship configuration by name
    /// </summary>
    public bool LoadShip(string shipName)
    {
        try
        {
            string fileName = SanitizeFileName(shipName) + fileExtension;
            string filePath = Path.Combine(SavePath, fileName);
            
            if (!File.Exists(filePath))
            {
                Debug.LogError($"Save file not found: {filePath}");
                OnError?.Invoke("Save file not found");
                return false;
            }
            
            string json = File.ReadAllText(filePath);
            ShipSaveData saveData = JsonUtility.FromJson<ShipSaveData>(json);
            
            ReconstructShip(saveData);
            
            Debug.Log($"Ship loaded from: {filePath}");
            OnLoadComplete?.Invoke(shipName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load ship: {e.Message}");
            OnError?.Invoke(e.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Get list of all saved ships
    /// </summary>
    public List<ShipSaveInfo> GetSavedShips()
    {
        List<ShipSaveInfo> ships = new List<ShipSaveInfo>();
        
        if (!Directory.Exists(SavePath)) return ships;
        
        string[] files = Directory.GetFiles(SavePath, "*" + fileExtension);
        
        foreach (string filePath in files)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                ShipSaveData data = JsonUtility.FromJson<ShipSaveData>(json);
                
                ships.Add(new ShipSaveInfo
                {
                    shipName = data.shipName,
                    saveDate = data.saveDate,
                    filePath = filePath,
                    bodyCount = data.bodySegments.Count,
                    totalHull = data.totalHull,
                    totalWeight = data.totalWeight,
                    thumbnailBase64 = data.thumbnailBase64
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to read save file {filePath}: {e.Message}");
            }
        }
        
        // Sort by date (newest first)
        ships.Sort((a, b) => string.Compare(b.saveDate, a.saveDate, StringComparison.Ordinal));
        
        return ships;
    }
    
    /// <summary>
    /// Delete a saved ship
    /// </summary>
    public bool DeleteShip(string shipName)
    {
        try
        {
            string fileName = SanitizeFileName(shipName) + fileExtension;
            string filePath = Path.Combine(SavePath, fileName);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"Deleted save file: {filePath}");
                return true;
            }
            
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save: {e.Message}");
            OnError?.Invoke(e.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Check if a ship name already exists
    /// </summary>
    public bool SaveExists(string shipName)
    {
        string fileName = SanitizeFileName(shipName) + fileExtension;
        string filePath = Path.Combine(SavePath, fileName);
        return File.Exists(filePath);
    }
    
    private ShipSaveData CreateSaveData(string shipName)
    {
        ShipSaveData saveData = new ShipSaveData
        {
            shipName = shipName,
            saveDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            version = "1.0"
        };
        
        // Save body segments
        for (int i = 0; i < assemblyManager.bodySegments.Count; i++)
        {
            ShipComponent segment = assemblyManager.bodySegments[i];
            BodySegmentSaveData segmentData = CreateBodySegmentData(segment, i);
            saveData.bodySegments.Add(segmentData);
        }
        
        // Save engine
        if (assemblyManager.currentEngine != null)
        {
            saveData.engine = CreateAttachedComponentData(assemblyManager.currentEngine);
        }
        
        // Save bridge
        if (assemblyManager.currentBridge != null)
        {
            saveData.bridge = CreateAttachedComponentData(assemblyManager.currentBridge);
        }
        
        // Save deck guns
        foreach (var gun in assemblyManager.deckGuns)
        {
            saveData.deckGuns.Add(CreateAttachedComponentData(gun));
        }
        
        // Calculate totals
        CalculateTotals(saveData);
        
        // Generate thumbnail
        saveData.thumbnailBase64 = GenerateThumbnail();
        
        return saveData;
    }
    
    private BodySegmentSaveData CreateBodySegmentData(ShipComponent segment, int index)
    {
        BodySegmentSaveData data = new BodySegmentSaveData
        {
            componentId = componentRegistry.GetComponentId(segment.Data),
            segmentIndex = index,
            position = segment.transform.position,
            rotation = segment.transform.rotation
        };
        
        // Save connection info
        SaveConnectionData(data, segment.ForwardConnection, AttachmentDirection.Forward);
        SaveConnectionData(data, segment.BackwardConnection, AttachmentDirection.Backward);
        SaveConnectionData(data, segment.LeftConnection, AttachmentDirection.Left);
        SaveConnectionData(data, segment.RightConnection, AttachmentDirection.Right);
        SaveConnectionData(data, segment.TopConnection, AttachmentDirection.Top);
        SaveConnectionData(data, segment.BottomConnection, AttachmentDirection.Bottom);
        
        return data;
    }
    
    private void SaveConnectionData(BodySegmentSaveData data, AttachmentPoint point, AttachmentDirection direction)
    {
        if (point == null || !point.isOccupied || point.connectedTo == null) return;
        
        // Find which segment the connected point belongs to
        int connectedIndex = -1;
        AttachmentDirection connectedDir = AttachmentDirection.Forward;
        
        for (int i = 0; i < assemblyManager.bodySegments.Count; i++)
        {
            ShipComponent segment = assemblyManager.bodySegments[i];
            
            if (segment.ForwardConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Forward; break; }
            if (segment.BackwardConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Backward; break; }
            if (segment.LeftConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Left; break; }
            if (segment.RightConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Right; break; }
            if (segment.TopConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Top; break; }
            if (segment.BottomConnection == point.connectedTo) { connectedIndex = i; connectedDir = AttachmentDirection.Bottom; break; }
        }
        
        if (connectedIndex >= 0)
        {
            data.connections.Add(new ConnectionSaveData
            {
                direction = direction,
                connectedToSegmentIndex = connectedIndex,
                connectedToDirection = connectedDir
            });
        }
    }
    
    private AttachedComponentSaveData CreateAttachedComponentData(ShipComponent component)
    {
        AttachedComponentSaveData data = new AttachedComponentSaveData
        {
            componentId = componentRegistry.GetComponentId(component.Data),
            localPosition = component.transform.localPosition,
            localRotation = component.transform.localRotation
        };
        
        // Find the attachment point that holds this component by searching all body segments
        foreach (var segment in assemblyManager.bodySegments)
        {
            for (int i = 0; i < segment.AttachmentPoints.Length; i++)
            {
                AttachmentPoint point = segment.AttachmentPoints[i];
                if (point.attachedComponent == component)
                {
                    data.parentSegmentIndex = assemblyManager.bodySegments.IndexOf(segment);
                    data.attachmentPointIndex = i;
                    data.attachmentPointName = point.name;
                    return data;
                }
            }
        }
        
        Debug.LogWarning($"Could not find attachment point for component: {component.name}");
        return data;
    }
    
    private void CalculateTotals(ShipSaveData saveData)
    {
        foreach (var segment in assemblyManager.bodySegments)
        {
            if (segment.Data != null)
            {
                saveData.totalHull += segment.Data.HullPoints;
                saveData.totalWeight += segment.Data.Weight;
            }
        }
        
        if (assemblyManager.currentEngine?.Data != null)
        {
            saveData.totalHull += assemblyManager.currentEngine.Data.HullPoints;
            saveData.totalWeight += assemblyManager.currentEngine.Data.Weight;
        }
        
        if (assemblyManager.currentBridge?.Data != null)
        {
            saveData.totalHull += assemblyManager.currentBridge.Data.HullPoints;
            saveData.totalWeight += assemblyManager.currentBridge.Data.Weight;
        }
        
        foreach (var gun in assemblyManager.deckGuns)
        {
            if (gun.Data != null)
            {
                saveData.totalHull += gun.Data.HullPoints;
                saveData.totalWeight += gun.Data.Weight;
            }
        }
    }
    
    private void ReconstructShip(ShipSaveData saveData)
    {
        // Clear existing ship
        assemblyManager.ClearShip();
        
        // Dictionary to map segment indices to reconstructed components
        Dictionary<int, ShipComponent> reconstructedSegments = new Dictionary<int, ShipComponent>();
        
        // Reconstruct body segments
        foreach (var segmentData in saveData.bodySegments)
        {
            ShipComponentData componentData = componentRegistry.GetComponentById(segmentData.componentId);
            
            if (componentData == null)
            {
                Debug.LogError($"Failed to find component: {segmentData.componentId}");
                continue;
            }
            
            // Instantiate segment
            GameObject instance = Instantiate(componentData.Prefab, assemblyManager.shipRoot);
            instance.transform.position = segmentData.position;
            instance.transform.rotation = segmentData.rotation;
            
            ShipComponent segment = instance.GetComponent<ShipComponent>();
            assemblyManager.bodySegments.Add(segment);
            reconstructedSegments[segmentData.segmentIndex] = segment;
        }
        
        // Restore connections between segments
        foreach (var segmentData in saveData.bodySegments)
        {
            if (!reconstructedSegments.TryGetValue(segmentData.segmentIndex, out ShipComponent segment))
                continue;
            
            foreach (var connection in segmentData.connections)
            {
                if (!reconstructedSegments.TryGetValue(connection.connectedToSegmentIndex, out ShipComponent connectedSegment))
                    continue;
                
                AttachmentPoint fromPoint = segment.GetBodyConnection(connection.direction);
                AttachmentPoint toPoint = connectedSegment.GetBodyConnection(connection.connectedToDirection);
                
                if (fromPoint != null && toPoint != null)
                {
                    fromPoint.isOccupied = true;
                    fromPoint.connectedTo = toPoint;
                    fromPoint.attachedComponent = connectedSegment;
                }
            }
        }
        
        // Reconstruct attached components
        if (saveData.engine != null && !string.IsNullOrEmpty(saveData.engine.componentId))
        {
            ReconstructAttachedComponent(saveData.engine, reconstructedSegments, ref assemblyManager.currentEngine);
        }
        
        if (saveData.bridge != null && !string.IsNullOrEmpty(saveData.bridge.componentId))
        {
            ReconstructAttachedComponent(saveData.bridge, reconstructedSegments, ref assemblyManager.currentBridge);
        }
        
        foreach (var gunData in saveData.deckGuns)
        {
            if (gunData == null || string.IsNullOrEmpty(gunData.componentId))
                continue;
                
            ShipComponent gun = null;
            ReconstructAttachedComponent(gunData, reconstructedSegments, ref gun);
            if (gun != null)
                assemblyManager.deckGuns.Add(gun);
        }
    }
    
    private void ReconstructAttachedComponent(
        AttachedComponentSaveData data, 
        Dictionary<int, ShipComponent> segments,
        ref ShipComponent resultComponent)
    {
        ShipComponentData componentData = componentRegistry.GetComponentById(data.componentId);
        
        if (componentData == null)
        {
            Debug.LogError($"Failed to find component: {data.componentId}");
            return;
        }
        
        if (!segments.TryGetValue(data.parentSegmentIndex, out ShipComponent parentSegment))
        {
            Debug.LogError($"Failed to find parent segment: {data.parentSegmentIndex}");
            return;
        }
        
        // Find attachment point
        AttachmentPoint attachPoint = null;
        
        // Try by index first
        if (data.attachmentPointIndex >= 0 && data.attachmentPointIndex < parentSegment.AttachmentPoints.Length)
        {
            attachPoint = parentSegment.AttachmentPoints[data.attachmentPointIndex];
        }
        
        // Fallback to name
        if (attachPoint == null && !string.IsNullOrEmpty(data.attachmentPointName))
        {
            foreach (var point in parentSegment.AttachmentPoints)
            {
                if (point.name == data.attachmentPointName)
                {
                    attachPoint = point;
                    break;
                }
            }
        }
        
        if (attachPoint == null)
        {
            Debug.LogError($"Failed to find attachment point for component: {data.componentId}");
            return;
        }
        
        // Instantiate component - parent to the body segment (same as during normal attachment)
        GameObject instance = Instantiate(componentData.Prefab, parentSegment.transform);
        instance.transform.localPosition = data.localPosition;
        instance.transform.localRotation = data.localRotation;
        
        resultComponent = instance.GetComponent<ShipComponent>();
        if (resultComponent != null)
        {
            resultComponent.Data = componentData; // Assign the component data
        }
        
        attachPoint.isOccupied = true;
        attachPoint.attachedComponent = resultComponent;
    }
    
    private string GenerateThumbnail()
    {
        if (thumbnailCamera == null) return null;
        
        try
        {
            // Create render texture
            RenderTexture rt = new RenderTexture(thumbnailWidth, thumbnailHeight, 24);
            thumbnailCamera.targetTexture = rt;
            
            // Render
            thumbnailCamera.Render();
            
            // Read pixels
            RenderTexture.active = rt;
            Texture2D texture = new Texture2D(thumbnailWidth, thumbnailHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, thumbnailWidth, thumbnailHeight), 0, 0);
            texture.Apply();
            
            // Cleanup
            thumbnailCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(rt);
            
            // Convert to base64
            byte[] bytes = texture.EncodeToPNG();
            Destroy(texture);
            
            return Convert.ToBase64String(bytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to generate thumbnail: {e.Message}");
            return null;
        }
    }
    
    private string SanitizeFileName(string name)
    {
        // Remove invalid characters
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}

/// <summary>
/// Info about a saved ship (for UI display)
/// </summary>
[Serializable]
public class ShipSaveInfo
{
    public string shipName;
    public string saveDate;
    public string filePath;
    public int bodyCount;
    public float totalHull;
    public float totalWeight;
    public string thumbnailBase64;
    
    public Texture2D GetThumbnail()
    {
        if (string.IsNullOrEmpty(thumbnailBase64)) return null;
        
        try
        {
            byte[] bytes = Convert.FromBase64String(thumbnailBase64);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(bytes);
            return texture;
        }
        catch
        {
            return null;
        }
    }
}