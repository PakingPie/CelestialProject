using UnityEngine;

/// <summary>
/// Attach to any obstacle (asteroid, planet, etc.) to register it with ObstacleRegistry.
/// Automatically calculates radius from mesh bounds.
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
    private float _radius;
    private float _lastUpdateTime;
    private Vector3 _lastPosition;

    void OnEnable()
    {
        if (ObstacleRegistry.Instance == null)
        {
            Debug.LogWarning($"ObstacleEntity '{name}': ObstacleRegistry not found in scene.");
            return;
        }

        _radius = CalculateRadius();
        _obstacleId = ObstacleRegistry.Instance.RegisterObstacle(transform, _radius, _isStatic);
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

    private float CalculateRadius()
    {
        if (_radiusOverride > 0f)
            return _radiusOverride * _radiusMultiplier;

        // Check this GameObject first
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            // Check children
            meshFilter = GetComponentInChildren<MeshFilter>();
        }

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector3 scale = meshFilter.transform.lossyScale;
            Vector3 scaledSize = Vector3.Scale(bounds.size, scale);
            float radius = Mathf.Max(scaledSize.x, scaledSize.y, scaledSize.z) * 0.5f;
            return radius * _radiusMultiplier;
        }

        // Check MeshRenderer as fallback
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = GetComponentInChildren<MeshRenderer>();
        }

        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            return radius * _radiusMultiplier;
        }

        Debug.LogWarning($"ObstacleEntity '{name}': No mesh found, using default radius of 10.");
        return 10f * _radiusMultiplier;
    }

    void OnDrawGizmosSelected()
    {
        float radius = (_radiusOverride > 0f) ? _radiusOverride * _radiusMultiplier : CalculateRadius();
        Gizmos.color = _isStatic ? Color.blue : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}