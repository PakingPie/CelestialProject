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

    [Header("Ordnance Reservations")]
    [Tooltip("Maximum number of active missile or torpedo reservations allowed on the same target")]
    public int MaxOrdnancePerTarget = 1;

    [Tooltip("If false, missiles and torpedoes will hold or keep flying instead of overflowing onto saturated targets")]
    public bool AllowOrdnanceOverflow = false;

    [Tooltip("How long a missile or torpedo launch reserves a target for global distribution")]
    public float OrdnanceReservationSeconds = 8f;

    // Track which weapons are targeting which enemies
    // Key: Target instance ID, Value: List of weapons targeting it
    private Dictionary<int, List<WeaponBase>> _targetAssignments = new Dictionary<int, List<WeaponBase>>(128);
    
    // Reverse lookup: Weapon -> Target
    private Dictionary<WeaponBase, Transform> _weaponTargets = new Dictionary<WeaponBase, Transform>(256);

    // Track short-lived reservations for launched ordnance so multiple launchers can coordinate.
    private Dictionary<int, List<float>> _ordnanceReservations = new Dictionary<int, List<float>>(128);

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
    /// Get the projected weapon count if the specified weapon targets this enemy.
    /// </summary>
    public int GetProjectedWeaponCountOnTarget(WeaponBase weapon, Transform target)
    {
        if (target == null)
            return 0;

        int currentCount = GetWeaponCountOnTarget(target);
        if (weapon == null)
            return currentCount;

        return IsWeaponTargeting(weapon, target) ? currentCount : currentCount + 1;
    }

    /// <summary>
    /// Check whether the specified weapon can target this enemy without exceeding the cap.
    /// </summary>
    public bool CanWeaponTargetWithoutOverflow(WeaponBase weapon, Transform target)
    {
        if (!EnableDistribution)
            return true;

        return GetProjectedWeaponCountOnTarget(weapon, target) <= MaxWeaponsPerTarget;
    }

    /// <summary>
    /// Get the score penalty for targeting this enemy (based on how many weapons already target it)
    /// </summary>
    public float GetTargetOverlapPenalty(Transform target)
    {
        if (!EnableDistribution)
            return 0f;

        int weaponCount = GetWeaponCountOnTarget(target);
        return Mathf.Max(0, weaponCount - 1) * OverlapPenalty;
    }

    /// <summary>
    /// Get the projected score penalty if the specified weapon targets this enemy.
    /// </summary>
    public float GetProjectedOverlapPenalty(WeaponBase weapon, Transform target)
    {
        if (!EnableDistribution)
            return 0f;

        int projectedCount = GetProjectedWeaponCountOnTarget(weapon, target);
        return Mathf.Max(0, projectedCount - 1) * OverlapPenalty;
    }

    /// <summary>
    /// Register a short-lived reservation for launched ordnance so other launchers can prefer different targets.
    /// </summary>
    public void RegisterOrdnanceReservation(Transform target, float durationSeconds = -1f)
    {
        if (!EnableDistribution || target == null)
            return;

        float duration = durationSeconds > 0f ? durationSeconds : OrdnanceReservationSeconds;
        if (duration <= 0f)
            return;

        int targetId = target.GetInstanceID();
        if (!_ordnanceReservations.TryGetValue(targetId, out var expirations))
        {
            expirations = new List<float>(4);
            _ordnanceReservations[targetId] = expirations;
        }

        expirations.Add(Time.time + duration);
    }

    /// <summary>
    /// Get the number of active ordnance reservations on this target.
    /// </summary>
    public int GetReservedOrdnanceCount(Transform target)
    {
        if (!EnableDistribution || target == null)
            return 0;

        int targetId = target.GetInstanceID();
        CleanupExpiredOrdnanceReservations(targetId);

        if (_ordnanceReservations.TryGetValue(targetId, out var expirations))
            return expirations.Count;

        return 0;
    }

    /// <summary>
    /// Check whether the target can accept another missile or torpedo reservation without overflowing.
    /// </summary>
    public bool CanReserveOrdnance(Transform target)
    {
        if (!EnableDistribution)
            return true;

        return GetReservedOrdnanceCount(target) < MaxOrdnancePerTarget;
    }

    private void CleanupExpiredOrdnanceReservations(int targetId)
    {
        if (!_ordnanceReservations.TryGetValue(targetId, out var expirations))
            return;

        float now = Time.time;
        for (int i = expirations.Count - 1; i >= 0; i--)
        {
            if (expirations[i] <= now)
                expirations.RemoveAt(i);
        }

        if (expirations.Count == 0)
            _ordnanceReservations.Remove(targetId);
    }

    private void CleanupExpiredOrdnanceReservations()
    {
        if (_ordnanceReservations.Count == 0)
            return;

        var targetIds = new List<int>(_ordnanceReservations.Keys);
        foreach (int targetId in targetIds)
        {
            CleanupExpiredOrdnanceReservations(targetId);
        }
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

        CleanupExpiredOrdnanceReservations();
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
