using UnityEngine;
using System.Collections.Generic;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }
    
    [Header("Spatial Partitioning")]
    [Tooltip("Size of each grid cell. Should be roughly equal to your typical weapon range.")]
    [SerializeField] private float _cellSize = 50f;
    
    [Header("Turret Updates")]
    [Tooltip("Maximum turret target updates per frame. Increase if turrets feel unresponsive.")]
    [SerializeField] private int _maxTurretUpdatesPerFrame = 30;
    
    private List<WeaponBase> _allTurrets = new List<WeaponBase>(500);
    private int _turretIndex = 0;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        CombatRegistry.Initialize(_cellSize);
    }
    
    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            CombatRegistry.Clear();
        }
    }
    
    public void RegisterTurret(WeaponBase turret)
    {
        if (turret != null && !_allTurrets.Contains(turret))
            _allTurrets.Add(turret);
    }
    
    public void UnregisterTurret(WeaponBase turret)
    {
        _allTurrets.Remove(turret);
    }
    
    void LateUpdate()
    {
        // Update spatial grid once per frame
        CombatRegistry.UpdateSpatialGrid();
        
        // Update turrets in round-robin
        UpdateTurrets();
    }
    
    private void UpdateTurrets()
    {
        if (_allTurrets.Count == 0) return;
        
        int updatesThisFrame = Mathf.Min(_maxTurretUpdatesPerFrame, _allTurrets.Count);
        
        for (int i = 0; i < updatesThisFrame; i++)
        {
            if (_allTurrets.Count == 0) break;
            
            _turretIndex = _turretIndex % _allTurrets.Count;
            WeaponBase turret = _allTurrets[_turretIndex];
            
            if (turret == null)
            {
                _allTurrets.RemoveAt(_turretIndex);
                continue;
            }
            
            turret.ManagedUpdateTarget();
            _turretIndex++;
        }
    }
    
    #if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = false;
    
    void OnGUI()
    {
        if (!_showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 100));
        GUILayout.Label($"Registered Turrets: {_allTurrets.Count}");
        GUILayout.Label($"Updates/Frame: {_maxTurretUpdatesPerFrame}");
        GUILayout.Label($"Update Cycle Time: {(_allTurrets.Count / (float)_maxTurretUpdatesPerFrame):F2} frames");
        GUILayout.EndArea();
    }
    #endif
}