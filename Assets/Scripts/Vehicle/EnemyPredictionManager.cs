using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyPredictionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _player;
    [SerializeField] private Canvas _hudCanvas;

    [Header("Canvas Mode")]
    [SerializeField] private bool _useWorldSpace = true;
    [Tooltip("Offset towards camera to prevent clipping with geometry")]
    [SerializeField] private float _worldSpaceOffset = 5f;

    [Header("Display Settings")]
    [SerializeField] private float _displayRange = 1000f;
    [SerializeField] private float _minDisplayRange = 50f;

    [Header("Prediction Settings")]
    [SerializeField] private float _predictionTime = 1f;
    [Tooltip("If assigned, uses bullet speed to calculate prediction time based on distance")]
    [SerializeField] private Gun _referenceGun;

    [Header("Enemy Indicator")]
    [SerializeField] private GameObject _enemyIndicatorPrefab;
    [SerializeField] private Color _enemyIndicatorColor = Color.red;

    [Header("Lead Indicator")]
    [SerializeField] private GameObject _leadIndicatorPrefab;
    [SerializeField] private Color _leadIndicatorColor = Color.yellow;
    [Tooltip("Minimum velocity magnitude to show lead indicator")]
    [SerializeField] private float _minVelocityToShowLead = 5f;
    [Tooltip("Enable/disable lead indicator entirely")]
    [SerializeField] private bool _showLeadIndicator = true;
    [Tooltip("Maximum prediction time (caps how far ahead the lead shows)")]
    [SerializeField] private float _maxPredictionTime = 2f;
    [Tooltip("Multiplier to shorten lead distance (0.5 = half distance)")]
    [SerializeField] private float _leadDistanceMultiplier = 1f;

    [Header("Connecting Line")]
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private Color _lineColor = Color.white;
    [SerializeField] private float _lineWidth = 2f;
    [Tooltip("Scale line width with distance")]
    [SerializeField] private bool _scaleLineWidth = true;

    [Header("Scaling")]
    [SerializeField] private float _baseScale = 1f;
    [SerializeField] private float _minScale = 0.3f;
    [SerializeField] private float _maxScale = 2f;
    [SerializeField] private float _scaleDistanceReference = 500f;
    [Tooltip("For world space: scale multiplier to maintain visible size")]
    [SerializeField] private float _worldSpaceScaleMultiplier = 0.1f;

    [Header("Update Settings")]
    [SerializeField] private float _updateInterval = 0.02f;

    private List<EnemyVehicle> _trackedEnemies = new List<EnemyVehicle>();
    private Dictionary<EnemyVehicle, EnemyIndicatorSet> _enemyIndicators = new Dictionary<EnemyVehicle, EnemyIndicatorSet>();
    private Queue<EnemyIndicatorSet> _indicatorPool = new Queue<EnemyIndicatorSet>();

    private float _lastUpdateTime;

    private class EnemyIndicatorSet
    {
        public PredictionIndicator EnemyIndicator;
        public PredictionIndicator LeadIndicator;
        public LineRenderer ConnectingLine;
    }

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
        _trackedEnemies.RemoveAll(e => e == null);

        foreach (var enemy in _trackedEnemies)
        {
            float distance = Vector3.Distance(_player.position, enemy.transform.position);
            bool inRange = distance <= _displayRange && distance >= _minDisplayRange;

            // Check if enemy is in front of camera
            Vector3 viewportPos = _playerCamera.WorldToViewportPoint(enemy.transform.position);
            bool inFrontOfCamera = viewportPos.z > 0;

            if (inRange && inFrontOfCamera)
            {
                if (!_enemyIndicators.TryGetValue(enemy, out var indicatorSet))
                {
                    indicatorSet = GetIndicatorSetFromPool();
                    _enemyIndicators[enemy] = indicatorSet;
                }

                float scale = CalculateScale(distance);

                // Calculate positions
                Vector3 enemyWorldPos = enemy.transform.position;
                Vector3 predictedPos = CalculatePredictedPosition(enemy, distance);
                bool showLead = _showLeadIndicator && enemy.Velocity.magnitude >= _minVelocityToShowLead;

                if (_useWorldSpace)
                {
                    Vector3 offsetEnemyPos = GetOffsetPosition(enemyWorldPos);
                    float worldScale = scale * distance * _worldSpaceScaleMultiplier;

                    // Update enemy indicator - pass enemy.transform
                    UpdateIndicatorWorldPosition(indicatorSet.EnemyIndicator, offsetEnemyPos, worldScale, enemy.transform);

                    // Update lead indicator - also tracks the same enemy
                    if (showLead)
                    {
                        Vector3 offsetPredictedPos = GetOffsetPosition(predictedPos);
                        UpdateIndicatorWorldPosition(indicatorSet.LeadIndicator, offsetPredictedPos, worldScale, enemy.transform);

                        UpdateConnectingLine(indicatorSet, offsetEnemyPos, offsetPredictedPos, worldScale, true);
                    }
                    else
                    {
                        indicatorSet.LeadIndicator.SetActive(false);
                        indicatorSet.ConnectingLine.enabled = false;
                    }
                }
                else
                {
                    // Screen space mode - pass enemy.transform
                    UpdateIndicatorScreenPosition(indicatorSet.EnemyIndicator, enemyWorldPos, scale, enemy.transform);

                    if (showLead)
                    {
                        UpdateIndicatorScreenPosition(indicatorSet.LeadIndicator, predictedPos, scale, enemy.transform);

                        Vector3 screenPosEnemy = _playerCamera.WorldToScreenPoint(enemyWorldPos);
                        Vector3 screenPosLead = _playerCamera.WorldToScreenPoint(predictedPos);
                        UpdateConnectingLineScreenSpace(indicatorSet, screenPosEnemy, screenPosLead, scale, true);
                    }
                    else
                    {
                        indicatorSet.LeadIndicator.SetActive(false);
                        indicatorSet.ConnectingLine.enabled = false;
                    }
                }
            }
            else
            {
                if (_enemyIndicators.TryGetValue(enemy, out var indicatorSet))
                {
                    ReturnIndicatorSetToPool(indicatorSet);
                    _enemyIndicators.Remove(enemy);
                }
            }
        }
    }

    private Vector3 GetOffsetPosition(Vector3 worldPosition)
    {
        Vector3 dirToCamera = (_playerCamera.transform.position - worldPosition).normalized;
        return worldPosition + dirToCamera * _worldSpaceOffset;
    }

    private float CalculateScale(float distance)
    {
        float scale = _baseScale * (_scaleDistanceReference / distance);
        return Mathf.Clamp(scale, _minScale, _maxScale);
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

        // Cap prediction time
        timeToTarget = Mathf.Min(timeToTarget, _maxPredictionTime);

        // Calculate predicted position with multiplier
        Vector3 leadOffset = enemyVelocity * timeToTarget * _leadDistanceMultiplier;

        return enemy.transform.position + leadOffset;
    }

    private void UpdateIndicatorWorldPosition(PredictionIndicator indicator, Vector3 worldPosition, float scale, Transform target = null)
    {
        indicator.SetActive(true);
        if (target != null)
            indicator.SetTarget(target);
        indicator.SetWorldPosition(worldPosition);
        indicator.SetScale(scale);
    }

    private void UpdateIndicatorScreenPosition(PredictionIndicator indicator, Vector3 worldPosition, float scale, Transform target = null)
    {
        Vector3 screenPos = _playerCamera.WorldToScreenPoint(worldPosition);

        if (screenPos.z > 0)
        {
            indicator.SetActive(true);
            if (target != null)
                indicator.SetTarget(target);
            indicator.SetPosition(screenPos);
            indicator.SetScale(scale);
        }
        else
        {
            indicator.SetActive(false);
        }
    }

    private void UpdateConnectingLine(EnemyIndicatorSet set, Vector3 startPos, Vector3 endPos, float scale, bool visible)
    {
        if (set.ConnectingLine == null)
            return;

        set.ConnectingLine.enabled = visible;

        if (!visible)
            return;

        set.ConnectingLine.SetPosition(0, startPos);
        set.ConnectingLine.SetPosition(1, endPos);

        if (_scaleLineWidth)
        {
            float scaledWidth = _lineWidth * scale;
            set.ConnectingLine.startWidth = scaledWidth;
            set.ConnectingLine.endWidth = scaledWidth;
        }
    }

    private void UpdateConnectingLineScreenSpace(EnemyIndicatorSet set, Vector3 screenStart, Vector3 screenEnd, float scale, bool visible)
    {
        if (set.ConnectingLine == null)
            return;

        set.ConnectingLine.enabled = visible;

        if (!visible)
            return;

        // Convert screen positions to world positions on a plane in front of camera
        float planeDistance = 1f;
        Vector3 worldStart = _playerCamera.ScreenToWorldPoint(new Vector3(screenStart.x, screenStart.y, planeDistance));
        Vector3 worldEnd = _playerCamera.ScreenToWorldPoint(new Vector3(screenEnd.x, screenEnd.y, planeDistance));

        set.ConnectingLine.SetPosition(0, worldStart);
        set.ConnectingLine.SetPosition(1, worldEnd);

        if (_scaleLineWidth)
        {
            float scaledWidth = _lineWidth * scale * 0.01f; // Smaller scale for screen space
            set.ConnectingLine.startWidth = scaledWidth;
            set.ConnectingLine.endWidth = scaledWidth;
        }
    }

    private EnemyIndicatorSet GetIndicatorSetFromPool()
    {
        if (_indicatorPool.Count > 0)
        {
            var set = _indicatorPool.Dequeue();
            set.EnemyIndicator.SetActive(true);
            set.LeadIndicator.SetActive(true);
            set.ConnectingLine.enabled = true;
            return set;
        }

        var newSet = new EnemyIndicatorSet();

        // Create enemy indicator
        GameObject enemyObj = Instantiate(_enemyIndicatorPrefab, _hudCanvas.transform);
        newSet.EnemyIndicator = enemyObj.GetComponent<PredictionIndicator>();
        newSet.EnemyIndicator.Initialize(_enemyIndicatorColor, _playerCamera);

        // Create lead indicator
        GameObject leadObj = Instantiate(_leadIndicatorPrefab, _hudCanvas.transform);
        newSet.LeadIndicator = leadObj.GetComponent<PredictionIndicator>();
        newSet.LeadIndicator.Initialize(_leadIndicatorColor, _playerCamera);

        // Create connecting line
        GameObject lineObj = new GameObject("ConnectingLine");
        lineObj.transform.SetParent(_hudCanvas.transform);
        newSet.ConnectingLine = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(newSet.ConnectingLine);

        return newSet;
    }

    private void SetupLineRenderer(LineRenderer lineRenderer)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = _lineWidth;
        lineRenderer.endWidth = _lineWidth;
        lineRenderer.material = _lineMaterial;
        lineRenderer.startColor = _lineColor;
        lineRenderer.endColor = _lineColor;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 100; // Render on top

        // Make it render in front
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }

    private void ReturnIndicatorSetToPool(EnemyIndicatorSet set)
    {
        set.EnemyIndicator.SetActive(false);
        set.LeadIndicator.SetActive(false);
        set.ConnectingLine.enabled = false;
        _indicatorPool.Enqueue(set);
    }

    public void RegisterEnemy(EnemyVehicle enemy)
    {
        if (!_trackedEnemies.Contains(enemy))
            _trackedEnemies.Add(enemy);
    }

    public void UnregisterEnemy(EnemyVehicle enemy)
    {
        _trackedEnemies.Remove(enemy);

        if (_enemyIndicators.TryGetValue(enemy, out var indicatorSet))
        {
            ReturnIndicatorSetToPool(indicatorSet);
            _enemyIndicators.Remove(enemy);
        }
    }

    public void SetDisplayRange(float range)
    {
        _displayRange = range;
    }

    public void SetReferenceGun(Gun gun)
    {
        _referenceGun = gun;
    }

    public void SetLeadIndicatorEnabled(bool enabled)
    {
        _showLeadIndicator = enabled;
    }

    public void SetLeadDistanceMultiplier(float multiplier)
    {
        _leadDistanceMultiplier = Mathf.Clamp01(multiplier);
    }

    public void SetMaxPredictionTime(float maxTime)
    {
        _maxPredictionTime = Mathf.Max(0f, maxTime);
    }

    public void ClearAll()
    {
        foreach (var kvp in _enemyIndicators)
        {
            ReturnIndicatorSetToPool(kvp.Value);
        }
        _enemyIndicators.Clear();
        _trackedEnemies.Clear();
    }

    private void OnDestroy()
    {
        // Clean up all indicators
        foreach (var kvp in _enemyIndicators)
        {
            if (kvp.Value.EnemyIndicator != null)
                Destroy(kvp.Value.EnemyIndicator.gameObject);
            if (kvp.Value.LeadIndicator != null)
                Destroy(kvp.Value.LeadIndicator.gameObject);
            if (kvp.Value.ConnectingLine != null)
                Destroy(kvp.Value.ConnectingLine.gameObject);
        }

        while (_indicatorPool.Count > 0)
        {
            var set = _indicatorPool.Dequeue();
            if (set.EnemyIndicator != null)
                Destroy(set.EnemyIndicator.gameObject);
            if (set.LeadIndicator != null)
                Destroy(set.LeadIndicator.gameObject);
            if (set.ConnectingLine != null)
                Destroy(set.ConnectingLine.gameObject);
        }
    }
}