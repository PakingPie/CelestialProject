using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

[CreateAssetMenu(menuName = "Boids/Target Priority Matrix")]
public class TargetPriorityMatrix : ScriptableObject
{
    [System.Serializable]
    public struct TypeMatchup
    {
        public VehicleType attackerType;
        public VehicleType targetType;
        [Tooltip("Priority multiplier applied to target score. >1 = preferred, <1 = avoided.")]
        public float priorityMultiplier;
    }

    [Tooltip("Per-type target priority overrides. Entries not listed default to 1.0.")]
    public List<TypeMatchup> matchups = new List<TypeMatchup>();

    // Lookup cache built on first query
    private Dictionary<long, float> _cache;

    /// <summary>
    /// Returns the priority multiplier for the given attacker→target pair.
    /// Returns 1.0 if no specific entry exists.
    /// </summary>
    public float GetPriority(VehicleType attacker, VehicleType target)
    {
        if (_cache == null)
            RebuildCache();

        long key = MakeKey(attacker, target);
        if (_cache.TryGetValue(key, out float mult))
            return mult;

        return 1f;
    }

    private void RebuildCache()
    {
        _cache = new Dictionary<long, float>(matchups.Count);
        for (int i = 0; i < matchups.Count; i++)
        {
            long key = MakeKey(matchups[i].attackerType, matchups[i].targetType);
            _cache[key] = matchups[i].priorityMultiplier;
        }
    }

    private static long MakeKey(VehicleType attacker, VehicleType target)
    {
        return ((long)(int)attacker << 32) | (uint)(int)target;
    }

    void OnValidate()
    {
        // Invalidate cache when designer edits in inspector
        _cache = null;
    }
}
