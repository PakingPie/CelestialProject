using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyPredictionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _player;
    [SerializeField] private Canvas _hudCanvas;
    
    [Header("Prediction Settings")]
    [SerializeField] private float _displayRange = 1000f;
    [SerializeField] private float _predictionTime = 1f;
    [Tooltip("If assigned, uses bullet speed to calculate prediction time based on distance")]
    [SerializeField] private Gun _referenceGun;
    
    [Header("Indicator")]
    [SerializeField] private GameObject _predictionIndicatorPrefab;
    [SerializeField] private Color _indicatorColor = Color.red;
    
    [Header("Update Settings")]
    [SerializeField] private float _updateInterval = 0.05f;
    
    private List<EnemyVehicle> _trackedEnemies = new List<EnemyVehicle>();
    private Dictionary<EnemyVehicle, PredictionIndicator> _enemyIndicators = new Dictionary<EnemyVehicle, PredictionIndicator>();
    private Queue<PredictionIndicator> _indicatorPool = new Queue<PredictionIndicator>();
    
    private float _lastUpdateTime;
    
    private void Start()
    {
        if (_playerCamera == null)
            _playerCamera = Camera.main;
            
        if (_player == null)
            _player = _playerCamera.transform;
    }
    
    private void Update()
    {
        if (Time.time - _lastUpdateTime < _updateInterval)
            return;
            
        _lastUpdateTime = Time.time;
        UpdateAllIndicators();
    }
    
    private void UpdateAllIndicators()
    {
        foreach (var enemy in _trackedEnemies)
        {
            if (enemy == null)
                continue;
                
            float distance = Vector3.Distance(_player.position, enemy.transform.position);
            bool inRange = distance <= _displayRange;
            
            if (inRange)
            {
                // Get or create indicator
                if (!_enemyIndicators.TryGetValue(enemy, out var indicator))
                {
                    indicator = GetIndicatorFromPool();
                    _enemyIndicators[enemy] = indicator;
                }
                
                // Calculate predicted position
                Vector3 predictedPos = CalculatePredictedPosition(enemy, distance);
                
                // Update indicator position
                UpdateIndicatorPosition(indicator, predictedPos);
            }
            else
            {
                // Return indicator to pool if out of range
                if (_enemyIndicators.TryGetValue(enemy, out var indicator))
                {
                    ReturnIndicatorToPool(indicator);
                    _enemyIndicators.Remove(enemy);
                }
            }
        }
    }
    
    private Vector3 CalculatePredictedPosition(EnemyVehicle enemy, float distance)
    {
        Vector3 enemyVelocity = enemy.Velocity;
        
        float timeToTarget;
        if (_referenceGun != null && _referenceGun.BulletPrefab != null)
        {
            float bulletSpeed = _referenceGun.BulletPrefab.Speed;
            timeToTarget = distance / bulletSpeed;
        }
        else
        {
            timeToTarget = _predictionTime;
        }
        
        return enemy.transform.position + enemyVelocity * timeToTarget;
    }
    
    private void UpdateIndicatorPosition(PredictionIndicator indicator, Vector3 worldPosition)
    {
        Vector3 screenPos = _playerCamera.WorldToScreenPoint(worldPosition);
        
        // Check if in front of camera
        if (screenPos.z > 0)
        {
            indicator.SetActive(true);
            indicator.SetPosition(screenPos);
        }
        else
        {
            indicator.SetActive(false);
        }
    }
    
    private PredictionIndicator GetIndicatorFromPool()
    {
        if (_indicatorPool.Count > 0)
        {
            var indicator = _indicatorPool.Dequeue();
            indicator.SetActive(true);
            return indicator;
        }
        
        // Create new indicator
        GameObject obj = Instantiate(_predictionIndicatorPrefab, _hudCanvas.transform);
        var newIndicator = obj.GetComponent<PredictionIndicator>();
        newIndicator.Initialize(_indicatorColor);
        return newIndicator;
    }
    
    private void ReturnIndicatorToPool(PredictionIndicator indicator)
    {
        indicator.SetActive(false);
        _indicatorPool.Enqueue(indicator);
    }
    
    /// <summary>
    /// Register an enemy to be tracked
    /// </summary>
    public void RegisterEnemy(EnemyVehicle enemy)
    {
        if (!_trackedEnemies.Contains(enemy))
            _trackedEnemies.Add(enemy);
    }
    
    /// <summary>
    /// Unregister an enemy (call when destroyed)
    /// </summary>
    public void UnregisterEnemy(EnemyVehicle enemy)
    {
        _trackedEnemies.Remove(enemy);
        
        if (_enemyIndicators.TryGetValue(enemy, out var indicator))
        {
            ReturnIndicatorToPool(indicator);
            _enemyIndicators.Remove(enemy);
        }
    }
    
    /// <summary>
    /// Set the display range at runtime
    /// </summary>
    public void SetDisplayRange(float range)
    {
        _displayRange = range;
    }
    
    /// <summary>
    /// Clear all indicators and tracked enemies
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in _enemyIndicators)
        {
            ReturnIndicatorToPool(kvp.Value);
        }
        _enemyIndicators.Clear();
        _trackedEnemies.Clear();
    }
}