using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static GlobalHelper;

public class EnemyPredictionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _player;
    [SerializeField] private Canvas _hudCanvas;
    [SerializeField] private PlayerShipMovement _playerShipMovement;

    [Header("Indicator Settings")]
    [SerializeField] private PredictionIndicatorSettings _enemySettings;
    [SerializeField] private PredictionIndicatorSettings _missileSettings;
    [SerializeField] private PredictionIndicatorSettings _allySettings;

    [Header("Canvas Mode")]
    [SerializeField] private bool _useWorldSpace = true;
    [SerializeField] private float _worldSpaceOffset = 5f;

    [Header("Display Settings")]
    [SerializeField] private float _displayRange = 1000f;
    [SerializeField] private float _minDisplayRange = 50f;
    [SerializeField] private float _missileDisplayRange = 500f;

    [Header("Prediction Settings")]
    [SerializeField] private float _maxPredictionTime = 2f;
    [SerializeField] private float _leadDistanceMultiplier = 1f;
    [SerializeField] private float _minVelocityToShowLead = 5f;
    [SerializeField] private Gun _referenceGun;

    [Header("Line Settings")]
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private float _lineWidth = 2f;
    [SerializeField] private bool _scaleLineWidth = true;

    [Header("Scaling")]
    [SerializeField] private float _scaleDistanceReference = 500f;
    [SerializeField] private float _worldSpaceScaleMultiplier = 0.1f;

    [Header("Update Settings")]
    [SerializeField] private float _updateInterval = 0.02f;

    [Header("Toggle Controls")]
    [SerializeField] private bool _showEnemyIndicators = true;
    [SerializeField] private bool _showMissileIndicators = true;
    [SerializeField] private bool _showAllyIndicators = false;

    // Tracked targets
    private Dictionary<Transform, TrackedTarget> _trackedTargets = new Dictionary<Transform, TrackedTarget>();
    private Dictionary<Transform, IndicatorSet> _activeIndicators = new Dictionary<Transform, IndicatorSet>();

    // Object pools per type
    private Dictionary<IndicatorType, Queue<IndicatorSet>> _indicatorPools = new Dictionary<IndicatorType, Queue<IndicatorSet>>();

    private float _lastUpdateTime;

    private class IndicatorSet
    {
        public PredictionIndicator PositionIndicator;
        public PredictionIndicator LeadIndicator;
        public LineRenderer ConnectingLine;
        public IndicatorType Type;
        public PredictionIndicatorSettings Settings;
    }

    private void Awake()
    {
        // Initialize pools
        foreach (IndicatorType type in System.Enum.GetValues(typeof(IndicatorType)))
        {
            _indicatorPools[type] = new Queue<IndicatorSet>();
        }
    }

    private void Start()
    {
        if (_playerCamera == null)
            _playerCamera = Camera.main;

        if (_player == null)
            _player = _playerCamera.transform;

        if (_playerShipMovement == null && _player != null)
            _playerShipMovement = _player.GetComponentInParent<PlayerShipMovement>();
    }

    private void Update()
    {
        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        _lastUpdateTime = Time.time;

        CleanupInvalidTargets();
        UpdateAllIndicators();
    }

    private void CleanupInvalidTargets()
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (var kvp in _trackedTargets)
        {
            if (!kvp.Value.IsValid)
                toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
        {
            UnregisterTarget(key);
        }
    }

    private void UpdateAllIndicators()
    {
        foreach (var kvp in _trackedTargets)
        {
            TrackedTarget target = kvp.Value;
            if (!target.IsValid) continue;

            if (!ShouldShowType(target.Type))
            {
                HideIndicator(kvp.Key);
                continue;
            }

            if(_player == null || target == null) continue;
            float distance = Vector3.Distance(_player.position, target.Position);
            float maxRange = GetDisplayRangeForType(target.Type);
            bool inRange = distance <= maxRange && distance >= _minDisplayRange;

            if (inRange)
            {
                UpdateIndicatorForTarget(kvp.Key, target, distance);
            }
            else
            {
                HideIndicator(kvp.Key);
            }
        }
    }

    private bool ShouldShowType(IndicatorType type)
    {
        switch (type)
        {
            case IndicatorType.Enemy:
                return _showEnemyIndicators;
            case IndicatorType.Missile:
                return _showMissileIndicators;
            case IndicatorType.Ally:
                return _showAllyIndicators;
            default:
                return true;
        }
    }

    private float GetDisplayRangeForType(IndicatorType type)
    {
        switch (type)
        {
            case IndicatorType.Missile:
                return _missileDisplayRange;
            default:
                return _displayRange;
        }
    }

    private PredictionIndicatorSettings GetSettingsForType(IndicatorType type)
    {
        switch (type)
        {
            case IndicatorType.Enemy:
                return _enemySettings;
            case IndicatorType.Missile:
                return _missileSettings;
            case IndicatorType.Ally:
                return _allySettings;
            default:
                return _enemySettings;
        }
    }

    private void UpdateIndicatorForTarget(Transform key, TrackedTarget target, float distance)
    {
        if (!_activeIndicators.TryGetValue(key, out var indicatorSet))
        {
            indicatorSet = GetIndicatorFromPool(target.Type);
            _activeIndicators[key] = indicatorSet;
        }

        PredictionIndicatorSettings settings = indicatorSet.Settings;
        float scale = CalculateScale(distance, settings);

        // Calculate distance-based colors
        Color positionColor = GetDistanceBasedColor(distance, settings, true);
        Color leadColor = GetDistanceBasedColor(distance, settings, false);

        Vector3 worldPos = target.Position;
        Vector3 predictedPos = CalculatePredictedPosition(target, distance);

        bool showPosition = settings.ShowPositionIndicator;
        bool showLead = settings.ShowLeadIndicator && target.Velocity.magnitude >= _minVelocityToShowLead;
        bool showLine = settings.ShowConnectingLine && showPosition && showLead;

        if (_useWorldSpace)
        {
            float worldScale = scale * distance * _worldSpaceScaleMultiplier;
            Vector3 offsetPos = GetOffsetPosition(worldPos);
            Vector3 offsetPredicted = GetOffsetPosition(predictedPos);

            if (showPosition)
            {
                UpdateIndicatorWorldPosition(indicatorSet.PositionIndicator, offsetPos, worldScale, target.Transform);
                indicatorSet.PositionIndicator.SetColor(positionColor);
            }
            else
            {
                indicatorSet.PositionIndicator.SetActive(false);
            }

            if (showLead)
            {
                UpdateIndicatorWorldPosition(indicatorSet.LeadIndicator, offsetPredicted, worldScale, target.Transform);
                indicatorSet.LeadIndicator.SetColor(leadColor);
            }
            else
            {
                indicatorSet.LeadIndicator.SetActive(false);
            }

            if (showLine)
            {
                UpdateConnectingLine(indicatorSet, offsetPos, offsetPredicted, worldScale, true);
                indicatorSet.ConnectingLine.startColor = positionColor;
                indicatorSet.ConnectingLine.endColor = leadColor;
            }
            else
            {
                indicatorSet.ConnectingLine.enabled = false;
            }
        }
        else
        {
            // Screen space mode - same pattern
            if (showPosition)
            {
                UpdateIndicatorScreenPosition(indicatorSet.PositionIndicator, worldPos, scale, target.Transform);
                indicatorSet.PositionIndicator.SetColor(positionColor);
            }
            else
            {
                indicatorSet.PositionIndicator.SetActive(false);
            }

            if (showLead)
            {
                UpdateIndicatorScreenPosition(indicatorSet.LeadIndicator, predictedPos, scale, target.Transform);
                indicatorSet.LeadIndicator.SetColor(leadColor);
            }
            else
            {
                indicatorSet.LeadIndicator.SetActive(false);
            }

            if (showLine)
            {
                Vector3 screenStart = _playerCamera.WorldToScreenPoint(worldPos);
                Vector3 screenEnd = _playerCamera.WorldToScreenPoint(predictedPos);
                UpdateConnectingLineScreenSpace(indicatorSet, screenStart, screenEnd, scale, true);
                indicatorSet.ConnectingLine.startColor = positionColor;
                indicatorSet.ConnectingLine.endColor = leadColor;
            }
            else
            {
                indicatorSet.ConnectingLine.enabled = false;
            }
        }
    }

    private void HideIndicator(Transform key)
    {
        if (_activeIndicators.TryGetValue(key, out var indicatorSet))
        {
            ReturnIndicatorToPool(indicatorSet);
            _activeIndicators.Remove(key);
        }
    }

    private float CalculateScale(float distance, PredictionIndicatorSettings settings)
    {
        float scale = settings.BaseScale * (_scaleDistanceReference / distance);
        return Mathf.Clamp(scale, settings.MinScale, settings.MaxScale);
    }

    private Vector3 GetOffsetPosition(Vector3 worldPosition)
    {
        Vector3 dirToCamera = (_playerCamera.transform.position - worldPosition).normalized;
        return worldPosition + dirToCamera * _worldSpaceOffset;
    }

    private Vector3 CalculatePredictedPosition(TrackedTarget target, float distance)
    {
        Vector3 targetPosition = target.Position;
        Vector3 targetVelocity = target.Velocity;
        Vector3 playerPosition = _player.position;
        Vector3 playerVelocity = _playerShipMovement != null ? _playerShipMovement.Velocity : Vector3.zero;

        float bulletSpeed = _referenceGun != null
            ? _referenceGun.BulletPrefab.Speed
            : 200f;

        Vector3 predictedPos = LeadCalculator.CalculateInterceptPoint(
            playerPosition,
            playerVelocity,
            bulletSpeed,
            targetPosition,
            targetVelocity,
            1f,
            _maxPredictionTime
        );

        if (predictedPos == Vector3.zero)
        {
            predictedPos = LeadCalculator.CalculateSimpleLead(
                playerPosition,
                targetPosition,
                targetVelocity,
                bulletSpeed,
                playerVelocity,
                1f,
                _maxPredictionTime
            );
        }

        return predictedPos;
    }

    private void UpdateIndicatorWorldPosition(PredictionIndicator indicator, Vector3 worldPosition, float scale, Transform target)
    {
        indicator.SetActive(true);
        indicator.SetTarget(target);
        indicator.SetWorldPosition(worldPosition);
        indicator.SetScale(scale);
    }

    private void UpdateIndicatorScreenPosition(PredictionIndicator indicator, Vector3 worldPosition, float scale, Transform target)
    {
        Vector3 screenPos = _playerCamera.WorldToScreenPoint(worldPosition);

        // Check if behind camera
        if (screenPos.z < 0)
        {
            // Flip the position for behind-camera targets
            screenPos.x = Screen.width - screenPos.x;
            screenPos.y = Screen.height - screenPos.y;
            screenPos.z = -screenPos.z;
        }

        // Clamp to screen edges with padding
        float padding = 50f;
        bool isOffScreen = screenPos.x < padding || screenPos.x > Screen.width - padding ||
                           screenPos.y < padding || screenPos.y > Screen.height - padding;

        if (isOffScreen)
        {
            // Clamp to screen edges
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
            Vector3 direction = (screenPos - screenCenter).normalized;

            // Find intersection with screen edge
            float maxX = (Screen.width / 2f) - padding;
            float maxY = (Screen.height / 2f) - padding;

            float scaleX = Mathf.Abs(direction.x) > 0.001f ? maxX / Mathf.Abs(direction.x) : float.MaxValue;
            float scaleY = Mathf.Abs(direction.y) > 0.001f ? maxY / Mathf.Abs(direction.y) : float.MaxValue;
            float edgeScale = Mathf.Min(scaleX, scaleY);

            screenPos = screenCenter + direction * edgeScale;
        }

        indicator.SetActive(true);
        indicator.SetTarget(target);
        indicator.SetPosition(screenPos);
        indicator.SetScale(scale);
        // indicator.SetOffScreen(isOffScreen); // Optional: change appearance when off-screen
    }

    private Color GetDistanceBasedColor(float distance, PredictionIndicatorSettings settings, bool isPositionIndicator)
    {
        if (!settings.UseDistanceColor)
            return isPositionIndicator ? settings.PositionColor : settings.LeadColor;

        float maxRange = _displayRange;
        float t = Mathf.Clamp01(distance / maxRange);

        // Lerp from near (bright) to far (dark)
        return Color.Lerp(settings.NearColor, settings.FarColor, t);
    }

    private void UpdateConnectingLine(IndicatorSet set, Vector3 startPos, Vector3 endPos, float scale, bool visible)
    {
        if (set.ConnectingLine == null) return;

        set.ConnectingLine.enabled = visible;
        if (!visible) return;

        set.ConnectingLine.SetPosition(0, startPos);
        set.ConnectingLine.SetPosition(1, endPos);

        if (_scaleLineWidth)
        {
            float scaledWidth = _lineWidth * scale;
            set.ConnectingLine.startWidth = scaledWidth;
            set.ConnectingLine.endWidth = scaledWidth;
        }
    }

    private void UpdateConnectingLineScreenSpace(IndicatorSet set, Vector3 screenStart, Vector3 screenEnd, float scale, bool visible)
    {
        if (set.ConnectingLine == null) return;

        set.ConnectingLine.enabled = visible;
        if (!visible) return;

        float planeDistance = 1f;
        Vector3 worldStart = _playerCamera.ScreenToWorldPoint(new Vector3(screenStart.x, screenStart.y, planeDistance));
        Vector3 worldEnd = _playerCamera.ScreenToWorldPoint(new Vector3(screenEnd.x, screenEnd.y, planeDistance));

        set.ConnectingLine.SetPosition(0, worldStart);
        set.ConnectingLine.SetPosition(1, worldEnd);

        if (_scaleLineWidth)
        {
            float scaledWidth = _lineWidth * scale * 0.01f;
            set.ConnectingLine.startWidth = scaledWidth;
            set.ConnectingLine.endWidth = scaledWidth;
        }
    }

    #region Object Pooling

    private IndicatorSet GetIndicatorFromPool(IndicatorType type)
    {
        var pool = _indicatorPools[type];
        PredictionIndicatorSettings settings = GetSettingsForType(type);

        if (pool.Count > 0)
        {
            var set = pool.Dequeue();
            set.PositionIndicator.SetActive(true);
            set.LeadIndicator.SetActive(true);
            set.ConnectingLine.enabled = true;
            return set;
        }

        return CreateIndicatorSet(type, settings);
    }

    private IndicatorSet CreateIndicatorSet(IndicatorType type, PredictionIndicatorSettings settings)
    {
        var set = new IndicatorSet
        {
            Type = type,
            Settings = settings
        };

        // Position indicator
        if (settings.PositionIndicatorPrefab != null)
        {
            GameObject posObj = Instantiate(settings.PositionIndicatorPrefab, _hudCanvas.transform);
            set.PositionIndicator = posObj.GetComponent<PredictionIndicator>();
            set.PositionIndicator.Initialize(settings.PositionColor, _playerCamera);
        }

        // Lead indicator
        if (settings.LeadIndicatorPrefab != null)
        {
            GameObject leadObj = Instantiate(settings.LeadIndicatorPrefab, _hudCanvas.transform);
            set.LeadIndicator = leadObj.GetComponent<PredictionIndicator>();
            set.LeadIndicator.Initialize(settings.LeadColor, _playerCamera);
        }

        // Connecting line
        GameObject lineObj = new GameObject($"ConnectingLine_{type}");
        lineObj.transform.SetParent(_hudCanvas.transform);
        set.ConnectingLine = lineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(set.ConnectingLine, settings.LineColor);

        return set;
    }

    private void SetupLineRenderer(LineRenderer lineRenderer, Color color)
    {
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = _lineWidth;
        lineRenderer.endWidth = _lineWidth;
        lineRenderer.material = _lineMaterial;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 100;
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
    }

    private void ReturnIndicatorToPool(IndicatorSet set)
    {
        if (set.PositionIndicator != null)
            set.PositionIndicator.SetActive(false);
        if (set.LeadIndicator != null)
            set.LeadIndicator.SetActive(false);
        if (set.ConnectingLine != null)
            set.ConnectingLine.enabled = false;

        _indicatorPools[set.Type].Enqueue(set);
    }

    #endregion

    #region Public Registration API

    public void RegisterEnemy(EnemyVehicle enemy)
    {
        if (enemy == null || _trackedTargets.ContainsKey(enemy.transform))
            return;

        var target = new TrackedTarget(
            enemy.transform,
            IndicatorType.Enemy,
            () => enemy.Velocity
        );

        _trackedTargets[enemy.transform] = target;
    }

    public void UnregisterEnemy(EnemyVehicle enemy)
    {
        if (enemy != null)
            UnregisterTarget(enemy.transform);
    }

    public void RegisterMissile(MissileVehicle missile)
    {
        if (missile == null || _trackedTargets.ContainsKey(missile.transform))
            return;

        var target = new TrackedTarget(
            missile.transform,
            IndicatorType.Missile,
            () => missile.transform.forward * missile.Velocity // Approximate velocity
        );

        _trackedTargets[missile.transform] = target;
    }

    public void UnregisterMissile(MissileVehicle missile)
    {
        if (missile != null)
            UnregisterTarget(missile.transform);
    }

    public void RegisterAlly(EnemyVehicle ally)
    {
        if (ally == null || _trackedTargets.ContainsKey(ally.transform))
            return;

        var target = new TrackedTarget(
            ally.transform,
            IndicatorType.Ally,
            () => ally.Velocity
        );

        _trackedTargets[ally.transform] = target;
    }

    public void UnregisterAlly(EnemyVehicle ally)
    {
        if (ally != null)
            UnregisterTarget(ally.transform);
    }

    private void UnregisterTarget(Transform key)
    {
        if (_activeIndicators.TryGetValue(key, out var set))
        {
            ReturnIndicatorToPool(set);
            _activeIndicators.Remove(key);
        }
        _trackedTargets.Remove(key);
    }

    #endregion

    #region Public Toggle API

    public void SetEnemyIndicatorsEnabled(bool enabled)
    {
        _showEnemyIndicators = enabled;
    }

    public void SetMissileIndicatorsEnabled(bool enabled)
    {
        _showMissileIndicators = enabled;
    }

    public void SetAllyIndicatorsEnabled(bool enabled)
    {
        _showAllyIndicators = enabled;
    }

    public void SetPositionIndicatorEnabled(IndicatorType type, bool enabled)
    {
        var settings = GetSettingsForType(type);
        if (settings != null)
            settings.ShowPositionIndicator = enabled;
    }

    public void SetLeadIndicatorEnabled(IndicatorType type, bool enabled)
    {
        var settings = GetSettingsForType(type);
        if (settings != null)
            settings.ShowLeadIndicator = enabled;
    }

    public void SetConnectingLineEnabled(IndicatorType type, bool enabled)
    {
        var settings = GetSettingsForType(type);
        if (settings != null)
            settings.ShowConnectingLine = enabled;
    }

    public void SetDisplayRange(float range)
    {
        _displayRange = range;
    }

    public void SetMissileDisplayRange(float range)
    {
        _missileDisplayRange = range;
    }

    public void SetReferenceGun(Gun gun)
    {
        _referenceGun = gun;
    }

    public void SetLeadDistanceMultiplier(float multiplier)
    {
        _leadDistanceMultiplier = Mathf.Clamp01(multiplier);
    }

    public void SetMaxPredictionTime(float maxTime)
    {
        _maxPredictionTime = Mathf.Max(0f, maxTime);
    }

    #endregion

    #region Cleanup

    public void ClearAll()
    {
        foreach (var kvp in _activeIndicators)
        {
            ReturnIndicatorToPool(kvp.Value);
        }
        _activeIndicators.Clear();
        _trackedTargets.Clear();
    }

    public void ClearType(IndicatorType type)
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (var kvp in _trackedTargets)
        {
            if (kvp.Value.Type == type)
                toRemove.Add(kvp.Key);
        }

        foreach (var key in toRemove)
        {
            UnregisterTarget(key);
        }
    }

    private void OnDestroy()
    {
        foreach (var kvp in _activeIndicators)
        {
            DestroyIndicatorSet(kvp.Value);
        }

        foreach (var pool in _indicatorPools.Values)
        {
            while (pool.Count > 0)
            {
                DestroyIndicatorSet(pool.Dequeue());
            }
        }
    }

    private void DestroyIndicatorSet(IndicatorSet set)
    {
        if (set.PositionIndicator != null)
            Destroy(set.PositionIndicator.gameObject);
        if (set.LeadIndicator != null)
            Destroy(set.LeadIndicator.gameObject);
        if (set.ConnectingLine != null)
            Destroy(set.ConnectingLine.gameObject);
    }

    #endregion
}