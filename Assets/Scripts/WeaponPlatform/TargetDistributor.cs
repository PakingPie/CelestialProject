using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages target distribution across multiple weapons to prevent overkill.
/// Attach to the same GameObject as CombatManager or a parent ship.
/// </summary>
public class TargetDistributor : MonoBehaviour
{
    public static TargetDistributor Instance { get; private set; }

    [Header("Distribution Settings")]
    [Tooltip("Maximum number of weapons that can target the same enemy")]
    public int MaxWeaponsPerTarget = 2;
    
    [Tooltip("How much to reduce target score for each weapon already targeting it")]
    public float OverlapPenalty = 500f;
    
    [Tooltip("Enable target distribution system")]
    public bool EnableDistribution = true;

    // Track which weapons are targeting which enemies
    // Key: Target instance ID, Value: List of weapons targeting it
    private Dictionary<int, List<WeaponBase>> _targetAssignments = new Dictionary<int, List<WeaponBase>>(128);
    
    // Reverse lookup: Weapon -> Target
    private Dictionary<WeaponBase, Transform> _weaponTargets = new Dictionary<WeaponBase, Transform>(256);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Called when a weapon wants to update its target assignment
    /// </summary>
    public void UpdateWeaponTarget(WeaponBase weapon, Transform newTarget)
    {
        if (!EnableDistribution)
            return;

        // Get previous target
        _weaponTargets.TryGetValue(weapon, out Transform previousTarget);

        // If target hasn't changed, do nothing
        if (previousTarget == newTarget)
            return;

        // Unregister from previous target
        if (previousTarget != null)
        {
            int prevId = previousTarget.GetInstanceID();
            if (_targetAssignments.TryGetValue(prevId, out var prevList))
            {
                prevList.Remove(weapon);
                if (prevList.Count == 0)
                    _targetAssignments.Remove(prevId);
            }
        }

        // Register to new target
        if (newTarget != null)
        {
            int newId = newTarget.GetInstanceID();
            if (!_targetAssignments.TryGetValue(newId, out var newList))
            {
                newList = new List<WeaponBase>(4);
                _targetAssignments[newId] = newList;
            }
            newList.Add(weapon);
            _weaponTargets[weapon] = newTarget;
        }
        else
        {
            _weaponTargets.Remove(weapon);
        }
    }

    /// <summary>
    /// Unregister a weapon completely (call when weapon is disabled/destroyed)
    /// </summary>
    public void UnregisterWeapon(WeaponBase weapon)
    {
        if (_weaponTargets.TryGetValue(weapon, out Transform target))
        {
            if (target != null)
            {
                int targetId = target.GetInstanceID();
                if (_targetAssignments.TryGetValue(targetId, out var list))
                {
                    list.Remove(weapon);
                    if (list.Count == 0)
                        _targetAssignments.Remove(targetId);
                }
            }
            _weaponTargets.Remove(weapon);
        }
    }

    /// <summary>
    /// Get how many weapons are currently targeting this enemy
    /// </summary>
    public int GetWeaponCountOnTarget(Transform target)
    {
        if (target == null)
            return 0;

        int targetId = target.GetInstanceID();
        if (_targetAssignments.TryGetValue(targetId, out var list))
            return list.Count;
        
        return 0;
    }

    /// <summary>
    /// Check if a target can accept more weapons
    /// </summary>
    public bool CanTargetAcceptMoreWeapons(Transform target)
    {
        return GetWeaponCountOnTarget(target) < MaxWeaponsPerTarget;
    }

    /// <summary>
    /// Get the score penalty for targeting this enemy (based on how many weapons already target it)
    /// </summary>
    public float GetTargetOverlapPenalty(Transform target)
    {
        if (!EnableDistribution)
            return 0f;

        int weaponCount = GetWeaponCountOnTarget(target);
        return weaponCount * OverlapPenalty;
    }

    /// <summary>
    /// Check if the specified weapon is already targeting this target
    /// </summary>
    public bool IsWeaponTargeting(WeaponBase weapon, Transform target)
    {
        if (_weaponTargets.TryGetValue(weapon, out Transform currentTarget))
            return currentTarget == target;
        return false;
    }

    /// <summary>
    /// Clean up null references (call periodically or when enemies are destroyed)
    /// </summary>
    public void CleanupNullReferences()
    {
        // Clean up weapon targets
        var weaponsToRemove = new List<WeaponBase>();
        foreach (var kvp in _weaponTargets)
        {
            if (kvp.Key == null || kvp.Value == null)
                weaponsToRemove.Add(kvp.Key);
        }
        foreach (var weapon in weaponsToRemove)
        {
            UnregisterWeapon(weapon);
        }

        // Clean up target assignments
        var targetsToRemove = new List<int>();
        foreach (var kvp in _targetAssignments)
        {
            kvp.Value.RemoveAll(w => w == null);
            if (kvp.Value.Count == 0)
                targetsToRemove.Add(kvp.Key);
        }
        foreach (var targetId in targetsToRemove)
        {
            _targetAssignments.Remove(targetId);
        }
    }

    void LateUpdate()
    {
        // Periodic cleanup every few seconds
        if (Time.frameCount % 300 == 0)
            CleanupNullReferences();
    }

#if UNITY_EDITOR
    [Header("Debug")]
    public bool ShowDebugInfo = false;

    void OnGUI()
    {
        if (!ShowDebugInfo) return;

        GUILayout.BeginArea(new Rect(320, 10, 300, 200));
        GUILayout.Label($"[Target Distributor]");
        GUILayout.Label($"Active Targets: {_targetAssignments.Count}");
        GUILayout.Label($"Registered Weapons: {_weaponTargets.Count}");
        GUILayout.Label($"Max Weapons/Target: {MaxWeaponsPerTarget}");
        GUILayout.EndArea();
    }
#endif
}
