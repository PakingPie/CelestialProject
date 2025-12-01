using UnityEngine;
using System.Collections.Generic;
using static GlobalHelper;

public static class CombatRegistry
{
    // Faction lists
    private static List<VehicleBase> _playerVehicles = new List<VehicleBase>(16);
    private static List<VehicleBase> _allyVehicles = new List<VehicleBase>(128);
    private static List<VehicleBase> _foeVehicles = new List<VehicleBase>(512);
    private static List<VehicleBase> _neutralVehicles = new List<VehicleBase>(64);

    // Spatial partitioning
    private static Dictionary<Vector2Int, List<VehicleBase>> _spatialGrid = new Dictionary<Vector2Int, List<VehicleBase>>(256);
    private static float _cellSize = 50f;

    // Reusable list for queries
    private static List<VehicleBase> _tempResults = new List<VehicleBase>(64);

    public static void Initialize(float cellSize)
    {
        _cellSize = cellSize;
        Clear();
    }

    public static void Clear()
    {
        _playerVehicles.Clear();
        _allyVehicles.Clear();
        _foeVehicles.Clear();
        _neutralVehicles.Clear();
        _spatialGrid.Clear();
    }

    #region Registration

    public static void Register(VehicleBase vehicle, Faction faction)
    {
        if (vehicle == null) return;

        List<VehicleBase> list = GetListForFaction(faction);
        if (list != null && !list.Contains(vehicle))
            list.Add(vehicle);
    }

    public static void Unregister(VehicleBase vehicle, Faction faction)
    {
        if (vehicle == null) return;

        List<VehicleBase> list = GetListForFaction(faction);
        if (list != null)
            list.Remove(vehicle);
    }

    private static List<VehicleBase> GetListForFaction(Faction faction)
    {
        switch (faction)
        {
            case Faction.Player:
                return _playerVehicles;
            case Faction.Ally:
                return _allyVehicles;
            case Faction.Foe:
                return _foeVehicles;
            case Faction.Neutral:
                return _neutralVehicles;
            default:
                return null;
        }
    }

    #endregion

    #region Spatial Grid

    public static void UpdateSpatialGrid()
    {
        _spatialGrid.Clear();

        AddToGrid(_playerVehicles);
        AddToGrid(_allyVehicles);
        AddToGrid(_foeVehicles);
        AddToGrid(_neutralVehicles);
    }

    private static void AddToGrid(List<VehicleBase> vehicles)
    {
        if (vehicles == null) return;

        for (int i = vehicles.Count - 1; i >= 0; i--)
        {
            VehicleBase vehicle = vehicles[i];

            if (vehicle == null)
            {
                vehicles.RemoveAt(i);
                continue;
            }

            Vector2Int cell = GetCell(vehicle.transform.position);
            if (!_spatialGrid.TryGetValue(cell, out var list))
            {
                list = new List<VehicleBase>(16);
                _spatialGrid[cell] = list;
            }
            list.Add(vehicle);
        }
    }

    private static Vector2Int GetCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / _cellSize),
            Mathf.FloorToInt(position.z / _cellSize)
        );
    }

    #endregion

    #region Queries

    public static void GetNearbyEnemies(Vector3 position, float range, Faction targetFactions, List<VehicleBase> results)
    {
        results.Clear();

        float rangeSqr = range * range;

        // Check each target faction
        if ((targetFactions & Faction.Foe) != 0)
            AddNearbyFromList(_foeVehicles, position, rangeSqr, results);

        if ((targetFactions & Faction.Player) != 0)
            AddNearbyFromList(_playerVehicles, position, rangeSqr, results);

        if ((targetFactions & Faction.Ally) != 0)
            AddNearbyFromList(_allyVehicles, position, rangeSqr, results);

        if ((targetFactions & Faction.Neutral) != 0)
            AddNearbyFromList(_neutralVehicles, position, rangeSqr, results);
    }

    private static void AddNearbyFromList(List<VehicleBase> vehicles, Vector3 position, float rangeSqr, List<VehicleBase> results)
    {
        if (vehicles == null) return;

        for (int i = vehicles.Count - 1; i >= 0; i--)
        {
            VehicleBase vehicle = vehicles[i];

            // Remove destroyed vehicles
            if (vehicle == null)
            {
                vehicles.RemoveAt(i);
                continue;
            }

            float distSqr = (vehicle.transform.position - position).sqrMagnitude;
            if (distSqr <= rangeSqr)
                results.Add(vehicle);
        }
    }

    /// <summary>
    /// Fast query using spatial grid. Use this for turret targeting.
    /// </summary>
    public static VehicleBase FindNearestEnemy(Vector3 position, float range, Faction targetFactions)
    {
        Vector2Int centerCell = GetCell(position);
        int cellRange = Mathf.CeilToInt(range / _cellSize);

        float rangeSqr = range * range;
        float nearestDistSqr = float.MaxValue;
        VehicleBase nearest = null;

        // Check surrounding cells
        for (int x = -cellRange; x <= cellRange; x++)
        {
            for (int z = -cellRange; z <= cellRange; z++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + z);

                if (!_spatialGrid.TryGetValue(cell, out var vehicles))
                    continue;

                for (int i = 0; i < vehicles.Count; i++)
                {
                    VehicleBase vehicle = vehicles[i];

                    if (vehicle == null) continue;

                    // Check faction
                    Faction vehicleFaction = GetVehicleFaction(vehicle);
                    if ((targetFactions & vehicleFaction) == 0)
                        continue;

                    float distSqr = (vehicle.transform.position - position).sqrMagnitude;
                    if (distSqr <= rangeSqr && distSqr < nearestDistSqr)
                    {
                        nearestDistSqr = distSqr;
                        nearest = vehicle;
                    }
                }
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get all enemies in range using spatial grid.
    /// </summary>
    public static void FindEnemiesInRange(Vector3 position, float range, Faction targetFactions, List<VehicleBase> results)
    {
        results.Clear();

        Vector2Int centerCell = GetCell(position);
        int cellRange = Mathf.CeilToInt(range / _cellSize);
        float rangeSqr = range * range;

        for (int x = -cellRange; x <= cellRange; x++)
        {
            for (int z = -cellRange; z <= cellRange; z++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + z);

                if (!_spatialGrid.TryGetValue(cell, out var vehicles))
                    continue;

                for (int i = 0; i < vehicles.Count; i++)
                {
                    VehicleBase vehicle = vehicles[i];

                    if (vehicle == null) continue;

                    // Check faction
                    Faction vehicleFaction = GetVehicleFaction(vehicle);
                    if ((targetFactions & vehicleFaction) == 0)
                        continue;

                    float distSqr = (vehicle.transform.position - position).sqrMagnitude;
                    if (distSqr <= rangeSqr)
                        results.Add(vehicle);
                }
            }
        }
    }

    private static Faction GetVehicleFaction(VehicleBase vehicle)
    {
        // Option 1: If FactionType is in VehicleBase
        return vehicle.FactionType;

        // Option 2: If FactionType is in subclasses
        // if (vehicle is EnemyVehicle enemy)
        //     return enemy.FactionType;
        // if (vehicle is PlayerVehicle player)
        //     return player.FactionType;
        // return Faction.None;
    }

    #endregion

    #region Debug

    public static int GetFactionCount(Faction faction)
    {
        List<VehicleBase> list = GetListForFaction(faction);
        return list?.Count ?? 0;
    }

    public static int GetTotalCount()
    {
        return _playerVehicles.Count + _allyVehicles.Count + _foeVehicles.Count + _neutralVehicles.Count;
    }

    #endregion
}