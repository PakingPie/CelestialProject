// TargetPriorityConfig.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using static GlobalHelper;

[CreateAssetMenu(fileName = "TargetPriorityConfig", menuName = "Combat/Target Priority Config")]
public class TargetPriorityConfig : ScriptableObject
{
    [Serializable]
    public class PriorityEntry
    {
        public VehicleType TargetType;
        [Range(0, 100)] public int Priority = 50;
        [Tooltip("Maximum range to engage this target type. 0 = use weapon's default range")]
        public float MaxEngagementRange = 0f;
        [Tooltip("Minimum range to engage this target type")]
        public float MinEngagementRange = 0f;
        [Tooltip("If true, this target type will be ignored")]
        public bool Ignore = false;
    }

    [Header("Priority Settings")]
    [Tooltip("Higher priority targets are preferred")]
    public List<PriorityEntry> Priorities = new List<PriorityEntry>();

    [Tooltip("Default priority for vehicle types not in the list")]
    public int DefaultPriority = 10;

    [Header("Behavior")]
    [Tooltip("How much distance affects priority (0 = ignore distance, 1 = heavily favor close targets)")]
    [Range(0f, 1f)] public float DistanceWeight = 0.3f;

    [Tooltip("How much current target health affects priority (0 = ignore, 1 = heavily favor damaged targets)")]
    [Range(0f, 1f)] public float DamageWeight = 0.2f;

    [Tooltip("Stick with current target unless new target priority exceeds by this amount")]
    [Range(0f, 50f)] public float TargetStickinessBonus = 10f;

    // Cache for quick lookup
    private Dictionary<VehicleType, PriorityEntry> _priorityLookup;

    public void Initialize()
    {
        _priorityLookup = new Dictionary<VehicleType, PriorityEntry>();
        foreach (var entry in Priorities)
        {
            _priorityLookup[entry.TargetType] = entry;
        }
    }

    public PriorityEntry GetPriorityEntry(VehicleType type)
    {
        if (_priorityLookup == null)
            Initialize();

        if (_priorityLookup.TryGetValue(type, out var entry))
            return entry;

        return null;
    }

    public int GetPriority(VehicleType type)
    {
        var entry = GetPriorityEntry(type);
        return entry?.Priority ?? DefaultPriority;
    }

    public bool ShouldIgnore(VehicleType type)
    {
        var entry = GetPriorityEntry(type);
        // Only ignore if explicitly set to ignore
        return entry?.Ignore ?? false;
    }
}