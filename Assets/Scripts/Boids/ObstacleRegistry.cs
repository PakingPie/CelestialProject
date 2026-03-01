using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized registry for all obstacles in the scene.
/// Uses a spatial grid for efficient nearby obstacle queries without Unity physics.
/// </summary>
public class ObstacleRegistry : MonoBehaviour
{
    private static ObstacleRegistry _instance;
    public static ObstacleRegistry Instance => _instance;

    [Header("Spatial Grid Settings")]
    [SerializeField] private float _cellSize = 500f;
    [SerializeField] private bool _debugMode = false;

    private Dictionary<int, List<ObstacleData>> _grid = new Dictionary<int, List<ObstacleData>>();
    private Dictionary<int, ObstacleData> _obstacles = new Dictionary<int, ObstacleData>();
    private List<ObstacleData> _queryResults = new List<ObstacleData>(64);

    private int _nextObstacleId = 0;

    public struct ObstacleData
    {
        public int Id;
        public Vector3 Position;
        public float Radius;
        public bool IsStatic;
        public Transform Transform;

        public bool IsValid => Transform != null;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// Register an obstacle and receive an ID for future updates/removal.
    /// </summary>
    public int RegisterObstacle(Transform transform, float radius, bool isStatic)
    {
        int id = _nextObstacleId++;

        ObstacleData data = new ObstacleData
        {
            Id = id,
            Position = transform.position,
            Radius = radius,
            IsStatic = isStatic,
            Transform = transform
        };

        _obstacles[id] = data;
        AddToGrid(data);

        if (_debugMode)
            Debug.Log($"ObstacleRegistry: Registered obstacle {id} at {data.Position} with radius {radius}");

        return id;
    }

    /// <summary>
    /// Remove an obstacle from the registry.
    /// </summary>
    public void UnregisterObstacle(int id)
    {
        if (_obstacles.TryGetValue(id, out ObstacleData data))
        {
            RemoveFromGrid(data);
            _obstacles.Remove(id);

            if (_debugMode)
                Debug.Log($"ObstacleRegistry: Unregistered obstacle {id}");
        }
    }

    /// <summary>
    /// Update position of a moving obstacle.
    /// </summary>
    public void UpdateObstaclePosition(int id, Vector3 newPosition)
    {
        if (!_obstacles.TryGetValue(id, out ObstacleData data))
            return;

        Vector3 oldPosition = data.Position;
        int oldHash = GetCellHash(oldPosition);
        int newHash = GetCellHash(newPosition);

        data.Position = newPosition;
        _obstacles[id] = data;

        if (oldHash != newHash)
        {
            RemoveFromGridCell(oldHash, id);
            AddToGridCell(newHash, data);
        }
        else
        {
            UpdateInGridCell(oldHash, data);
        }
    }

    /// <summary>
    /// Get all obstacles within range of a position.
    /// </summary>
    public List<ObstacleData> GetNearbyObstacles(Vector3 position, float range)
    {
        _queryResults.Clear();

        int cellRange = Mathf.CeilToInt(range / _cellSize);
        Vector3Int centerCell = GetCellCoord(position);

        float rangeSqr = range * range;

        for (int x = -cellRange; x <= cellRange; x++)
        {
            for (int y = -cellRange; y <= cellRange; y++)
            {
                for (int z = -cellRange; z <= cellRange; z++)
                {
                    int hash = GetCellHash(centerCell.x + x, centerCell.y + y, centerCell.z + z);

                    if (_grid.TryGetValue(hash, out List<ObstacleData> cell))
                    {
                        for (int i = 0; i < cell.Count; i++)
                        {
                            ObstacleData obstacle = cell[i];

                            float distSqr = (obstacle.Position - position).sqrMagnitude;
                            float combinedRange = range + obstacle.Radius;

                            if (distSqr <= combinedRange * combinedRange)
                            {
                                _queryResults.Add(obstacle);
                            }
                        }
                    }
                }
            }
        }

        return _queryResults;
    }

    /// <summary>
    /// Check if there's an obstacle in a specific direction (for directional avoidance).
    /// Returns the closest obstacle in the cone.
    /// </summary>
    public bool CheckDirection(Vector3 position, Vector3 direction, float distance, float coneAngle, out ObstacleData hitObstacle, out float hitDistance)
    {
        hitObstacle = default;
        hitDistance = float.MaxValue;
        bool found = false;

        List<ObstacleData> nearby = GetNearbyObstacles(position, distance);
        float cosAngle = Mathf.Cos(coneAngle * Mathf.Deg2Rad);

        for (int i = 0; i < nearby.Count; i++)
        {
            ObstacleData obstacle = nearby[i];
            Vector3 toObstacle = obstacle.Position - position;
            float dist = toObstacle.magnitude;

            if (dist < 0.01f || dist > distance + obstacle.Radius)
                continue;

            float dot = Vector3.Dot(direction, toObstacle / dist);

            if (dot >= cosAngle)
            {
                float effectiveDist = dist - obstacle.Radius;

                if (effectiveDist < hitDistance)
                {
                    hitDistance = effectiveDist;
                    hitObstacle = obstacle;
                    found = true;
                }
            }
        }

        return found;
    }

    private void AddToGrid(ObstacleData data)
    {
        int hash = GetCellHash(data.Position);
        AddToGridCell(hash, data);
    }

    private void AddToGridCell(int hash, ObstacleData data)
    {
        if (!_grid.TryGetValue(hash, out List<ObstacleData> cell))
        {
            cell = new List<ObstacleData>(8);
            _grid[hash] = cell;
        }
        cell.Add(data);
    }

    private void RemoveFromGrid(ObstacleData data)
    {
        int hash = GetCellHash(data.Position);
        RemoveFromGridCell(hash, data.Id);
    }

    private void RemoveFromGridCell(int hash, int id)
    {
        if (_grid.TryGetValue(hash, out List<ObstacleData> cell))
        {
            for (int i = cell.Count - 1; i >= 0; i--)
            {
                if (cell[i].Id == id)
                {
                    cell.RemoveAt(i);
                    break;
                }
            }

            if (cell.Count == 0)
                _grid.Remove(hash);
        }
    }

    private void UpdateInGridCell(int hash, ObstacleData data)
    {
        if (_grid.TryGetValue(hash, out List<ObstacleData> cell))
        {
            for (int i = 0; i < cell.Count; i++)
            {
                if (cell[i].Id == data.Id)
                {
                    cell[i] = data;
                    break;
                }
            }
        }
    }

    private Vector3Int GetCellCoord(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / _cellSize),
            Mathf.FloorToInt(position.y / _cellSize),
            Mathf.FloorToInt(position.z / _cellSize)
        );
    }

    private int GetCellHash(Vector3 position)
    {
        Vector3Int coord = GetCellCoord(position);
        return GetCellHash(coord.x, coord.y, coord.z);
    }

    private int GetCellHash(int x, int y, int z)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + z;
            return hash;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!_debugMode || _obstacles == null)
            return;

        Gizmos.color = Color.yellow;
        foreach (var kvp in _obstacles)
        {
            ObstacleData data = kvp.Value;
            if (data.IsValid)
            {
                Gizmos.DrawWireSphere(data.Position, data.Radius);
            }
        }
    }
}