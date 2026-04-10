using UnityEngine;

/// <summary>
/// Attach to any obstacle (asteroid, planet, etc.) to register it with ObstacleRegistry.
/// Automatically calculates an axis-aligned bounding box from all child renderers.
/// </summary>
public class ObstacleEntity : MonoBehaviour
{
    [Header("Obstacle Settings")]
    [SerializeField] private bool _isStatic = false;
    [SerializeField] private float _radiusMultiplier = 1.0f;
    [SerializeField] private float _radiusOverride = -1f;

    [Header("Update Settings")]
    [SerializeField] private float _updateInterval = 0.1f;

    private int _obstacleId = -1;
    private Bounds _localBounds;   // Bounds in local space (extents relative to transform origin)
    private float _lastUpdateTime;
    private Vector3 _lastPosition;
    private bool _pendingRegistration = false;

    void OnEnable()
    {
        _localBounds = CalculateLocalBounds();

        if (ObstacleRegistry.Instance != null)
        {
            Register();
        }
        else
        {
            _pendingRegistration = true;
        }
    }

    void Start()
    {
        // Retry registration if OnEnable fired before ObstacleRegistry.Awake()
        if (_pendingRegistration && _obstacleId < 0 && ObstacleRegistry.Instance != null)
        {
            Register();
            _pendingRegistration = false;
        }
    }

    private void Register()
    {
        Bounds worldBounds = GetWorldBounds();
        _obstacleId = ObstacleRegistry.Instance.RegisterObstacle(transform, worldBounds, _isStatic);
        _lastPosition = transform.position;
        _lastUpdateTime = Time.time;
    }

    void OnDisable()
    {
        if (ObstacleRegistry.Instance != null && _obstacleId >= 0)
        {
            ObstacleRegistry.Instance.UnregisterObstacle(_obstacleId);
            _obstacleId = -1;
        }
    }

    void Update()
    {
        if (_isStatic || _obstacleId < 0 || ObstacleRegistry.Instance == null)
            return;

        if (Time.time - _lastUpdateTime < _updateInterval)
            return;

        Vector3 currentPos = transform.position;

        if ((currentPos - _lastPosition).sqrMagnitude > 1f)
        {
            ObstacleRegistry.Instance.UpdateObstaclePosition(_obstacleId, currentPos);
            _lastPosition = currentPos;
        }

        _lastUpdateTime = Time.time;
    }

    /// <summary>
    /// Calculate combined bounds from all child renderers, stored relative to this transform's position.
    /// </summary>
    private Bounds CalculateLocalBounds()
    {
        if (_radiusOverride > 0f)
        {
            float r = _radiusOverride * _radiusMultiplier;
            return new Bounds(Vector3.zero, Vector3.one * r * 2f);
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning($"ObstacleEntity '{name}': No renderers found, using default 10-unit bounds.");
            float r = 10f * _radiusMultiplier;
            return new Bounds(Vector3.zero, Vector3.one * r * 2f);
        }

        // Build combined world bounds from all renderers
        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }

        // Convert to local offset from this transform's position
        Vector3 localCenter = combined.center - transform.position;
        Vector3 localSize = combined.size * _radiusMultiplier;
        return new Bounds(localCenter, localSize);
    }

    /// <summary>
    /// Get world-space AABB by translating local bounds to current position.
    /// </summary>
    private Bounds GetWorldBounds()
    {
        return new Bounds(transform.position + _localBounds.center, _localBounds.size);
    }

    void OnDrawGizmosSelected()
    {
        Bounds b;
        if (_radiusOverride > 0f)
        {
            float r = _radiusOverride * _radiusMultiplier;
            b = new Bounds(transform.position, Vector3.one * r * 2f);
        }
        else
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                b.size *= _radiusMultiplier;
            }
            else
            {
                b = new Bounds(transform.position, Vector3.one * 20f * _radiusMultiplier);
            }
        }

        Gizmos.color = _isStatic ? Color.blue : Color.cyan;
        Gizmos.DrawWireCube(b.center, b.size);
    }
}