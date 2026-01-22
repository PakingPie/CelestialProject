using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ShipSaveData
{
    public string shipName;
    public string saveDate;
    public string version = "1.0";
    
    public List<BodySegmentSaveData> bodySegments = new List<BodySegmentSaveData>();
    public AttachedComponentSaveData engine;
    public AttachedComponentSaveData bridge;
    public List<AttachedComponentSaveData> deckGuns = new List<AttachedComponentSaveData>();
    
    // Metadata
    public float totalHull;
    public float totalArmor;
    public float totalShield;
    public float totalHullRegen;
    public float totalArmorRegen;
    public float totalShieldRegen;
    public float totalWeight;
    public float totalPowerConsumption;
    public float totalPowerGeneration;
    public string thumbnailBase64; // Optional: store thumbnail as base64
}

[Serializable]
public class BodySegmentSaveData
{
    public string componentId; // Unique ID to identify the ScriptableObject
    public int segmentIndex;   // Order in the body chain
    
    // Transform data
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
    
    // Connection data - which connections are used
    public List<ConnectionSaveData> connections = new List<ConnectionSaveData>();
}

[Serializable]
public class ConnectionSaveData
{
    public AttachmentDirection direction;
    public int connectedToSegmentIndex; // -1 if not connected to another body
    public AttachmentDirection connectedToDirection;
}

[Serializable]
public class AttachedComponentSaveData
{
    public string componentId;
    public int parentSegmentIndex;      // Which body segment this is attached to
    public int attachmentPointIndex;    // Index of attachment point on that segment
    public string attachmentPointName;  // Fallback: name of attachment point
    
    public SerializableVector3 localPosition;
    public SerializableQuaternion localRotation;
}

// Serializable versions of Unity structs
[Serializable]
public struct SerializableVector3
{
    public float x, y, z;
    
    public SerializableVector3(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
    
    public static implicit operator SerializableVector3(Vector3 v) => new SerializableVector3(v);
    public static implicit operator Vector3(SerializableVector3 v) => v.ToVector3();
}

[Serializable]
public struct SerializableQuaternion
{
    public float x, y, z, w;
    
    public SerializableQuaternion(Quaternion q)
    {
        x = q.x;
        y = q.y;
        z = q.z;
        w = q.w;
    }
    
    public Quaternion ToQuaternion()
    {
        return new Quaternion(x, y, z, w);
    }
    
    public static implicit operator SerializableQuaternion(Quaternion q) => new SerializableQuaternion(q);
    public static implicit operator Quaternion(SerializableQuaternion q) => q.ToQuaternion();
}