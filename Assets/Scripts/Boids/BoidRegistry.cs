using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

/// <summary>
/// Central registry for all boids in the scene.
/// Enables cross-flock queries for separation and collision avoidance.
/// </summary>
public class BoidRegistry : MonoBehaviour
{
    private static BoidRegistry _instance;
    public static BoidRegistry Instance => _instance;

    [Header("Spatial Grid Settings")]
    [SerializeField] private float _cellSize = 100f;

    private Dictionary<int, List<Boid>> _grid = new Dictionary<int, List<Boid>>();
    private HashSet<Boid> _allBoids = new HashSet<Boid>();
    private List<Boid> _queryResults = new List<Boid>(64);

    // Cache for grid updates
    private Dictionary<Boid, int> _boidCellCache = new Dictionary<Boid, int>();

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

    public void RegisterBoid(Boid boid)
    {
        if (boid == null || _allBoids.Contains(boid))
            return;

        _allBoids.Add(boid);
        int hash = GetCellHash(boid.position);
        AddToCell(hash, boid);
        _boidCellCache[boid] = hash;
    }

    public void UnregisterBoid(Boid boid)
    {
        if (boid == null || !_allBoids.Contains(boid))
            return;

        _allBoids.Remove(boid);

        if (_boidCellCache.TryGetValue(boid, out int hash))
        {
            RemoveFromCell(hash, boid);
            _boidCellCache.Remove(boid);
        }
    }

    public void UpdateBoidPosition(Boid boid)
    {
        if (boid == null || !_allBoids.Contains(boid))
            return;

        int newHash = GetCellHash(boid.position);

        if (_boidCellCache.TryGetValue(boid, out int oldHash))
        {
            if (oldHash != newHash)
            {
                RemoveFromCell(oldHash, boid);
                AddToCell(newHash, boid);
                _boidCellCache[boid] = newHash;
            }
        }
        else
        {
            AddToCell(newHash, boid);
            _boidCellCache[boid] = newHash;
        }
    }

    /// <summary>
    /// Get all boids within range of a position.
    /// </summary>
    public List<Boid> GetNearbyBoids(Vector3 position, float range)
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

                    if (_grid.TryGetValue(hash, out List<Boid> cell))
                    {
                        for (int i = 0; i < cell.Count; i++)
                        {
                            Boid boid = cell[i];
                            if (boid == null)
                                continue;

                            float distSqr = (boid.position - position).sqrMagnitude;
                            if (distSqr <= rangeSqr)
                            {
                                _queryResults.Add(boid);
                            }
                        }
                    }
                }
            }
        }

        return _queryResults;
    }

    /// <summary>
    /// Get all boids within range, filtered by faction.
    /// </summary>
    public List<Boid> GetNearbyBoids(Vector3 position, float range, Faction faction, bool matchFaction)
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

                    if (_grid.TryGetValue(hash, out List<Boid> cell))
                    {
                        for (int i = 0; i < cell.Count; i++)
                        {
                            Boid boid = cell[i];
                            if (boid == null)
                                continue;

                            bool factionMatch = boid.GetFaction() == faction;
                            if (factionMatch != matchFaction)
                                continue;

                            float distSqr = (boid.position - position).sqrMagnitude;
                            if (distSqr <= rangeSqr)
                            {
                                _queryResults.Add(boid);
                            }
                        }
                    }
                }
            }
        }

        return _queryResults;
    }

    public int GetTotalBoidCount()
    {
        return _allBoids.Count;
    }

    private void AddToCell(int hash, Boid boid)
    {
        if (!_grid.TryGetValue(hash, out List<Boid> cell))
        {
            cell = new List<Boid>(16);
            _grid[hash] = cell;
        }
        cell.Add(boid);
    }

    private void RemoveFromCell(int hash, Boid boid)
    {
        if (_grid.TryGetValue(hash, out List<Boid> cell))
        {
            cell.Remove(boid);
            if (cell.Count == 0)
                _grid.Remove(hash);
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
}