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

    private static List<AAMissile> _allMissiles = new List<AAMissile>(128);
    private static List<AAMissile> _playerMissiles = new List<AAMissile>(32);
    private static List<AAMissile> _allyMissiles = new List<AAMissile>(64);
    private static List<AAMissile> _foeMissiles = new List<AAMissile>(64);

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

        _allMissiles.Clear();
        _playerMissiles.Clear();
        _allyMissiles.Clear();
        _foeMissiles.Clear();
    }

    #region Registration

    public static void Register(VehicleBase vehicle, Faction faction)
    {
        if (vehicle == null) return;

        // Register to all matching faction lists (supports flags enum combinations)
        if ((faction & Faction.Player) != 0 && !_playerVehicles.Contains(vehicle))
            _playerVehicles.Add(vehicle);
        if ((faction & Faction.Ally) != 0 && !_allyVehicles.Contains(vehicle))
            _allyVehicles.Add(vehicle);
        if ((faction & Faction.Foe) != 0 && !_foeVehicles.Contains(vehicle))
            _foeVehicles.Add(vehicle);
        if ((faction & Faction.Neutral) != 0 && !_neutralVehicles.Contains(vehicle))
            _neutralVehicles.Add(vehicle);
    }

    public static void Unregister(VehicleBase vehicle, Faction faction)
    {
        if (vehicle == null) return;

        // Unregister from all matching faction lists (supports flags enum combinations)
        if ((faction & Faction.Player) != 0)
            _playerVehicles.Remove(vehicle);
        if ((faction & Faction.Ally) != 0)
            _allyVehicles.Remove(vehicle);
        if ((faction & Faction.Foe) != 0)
            _foeVehicles.Remove(vehicle);
        if ((faction & Faction.Neutral) != 0)
            _neutralVehicles.Remove(vehicle);
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

    public static void GetNearbyEnemies(Vector3 position, float range, Faction targetFactions, List<VehicleBase> results, bool isTargetingMissile = false)
    {
        results.Clear();

        float rangeSqr = range * range;


        // Check each target faction
        if ((targetFactions & Faction.Foe) != 0)
            AddNearbyFromList(_foeVehicles, position, rangeSqr, results, isTargetingMissile);

        if ((targetFactions & Faction.Player) != 0)
            AddNearbyFromList(_playerVehicles, position, rangeSqr, results, isTargetingMissile);

        if ((targetFactions & Faction.Ally) != 0)
            AddNearbyFromList(_allyVehicles, position, rangeSqr, results, isTargetingMissile);

        if ((targetFactions & Faction.Neutral) != 0)
            AddNearbyFromList(_neutralVehicles, position, rangeSqr, results, isTargetingMissile);
    }

    private static void AddNearbyFromList(List<VehicleBase> vehicles, Vector3 position, float rangeSqr, List<VehicleBase> results, bool isTargetingMissile = false)
    {
        if (vehicles == null) return;

        for (int i = vehicles.Count - 1; i >= 0; i--)
        {
            VehicleBase vehicle = vehicles[i];

            // Remove destroyed vehicles - must check BEFORE accessing any properties
            if (vehicle == null)
            {
                vehicles.RemoveAt(i);
                continue;
            }

            if (!isTargetingMissile && vehicle.CompareTag("Missile"))
                continue;

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

    #region Missile Registration

    public static void RegisterMissile(AAMissile missile, Faction sourceFaction)
    {
        if (missile == null) return;

        if (!_allMissiles.Contains(missile))
            _allMissiles.Add(missile);

        List<AAMissile> list = GetMissileListForFaction(sourceFaction);
        if (list != null && !list.Contains(missile))
            list.Add(missile);
    }

    public static void UnregisterMissile(AAMissile missile, Faction sourceFaction)
    {
        if (missile == null) return;

        _allMissiles.Remove(missile);

        List<AAMissile> list = GetMissileListForFaction(sourceFaction);
        list?.Remove(missile);
    }

    private static List<AAMissile> GetMissileListForFaction(Faction faction)
    {
        switch (faction)
        {
            case Faction.Player:
                return _playerMissiles;
            case Faction.Ally:
                return _allyMissiles;
            case Faction.Foe:
                return _foeMissiles;
            default:
                return null;
        }
    }

    #endregion

    #region Missile Queries

    /// <summary>
    /// Find hostile missiles (missiles fired BY enemy factions)
    /// </summary>
    public static void GetHostileMissiles(Vector3 position, float range, Faction myFaction, List<AAMissile> results)
    {
        results.Clear();
        float rangeSqr = range * range;

        if (myFaction == Faction.Player || myFaction == Faction.Ally)
        {
            AddNearbyMissiles(_foeMissiles, position, rangeSqr, results);
        }
        else if (myFaction == Faction.Foe)
        {
            AddNearbyMissiles(_playerMissiles, position, rangeSqr, results);
            AddNearbyMissiles(_allyMissiles, position, rangeSqr, results);
        }
    }

    /// <summary>
    /// Find nearest hostile missile
    /// </summary>
    public static AAMissile FindNearestHostileMissile(Vector3 position, float range, Faction myFaction)
    {
        float rangeSqr = range * range;
        float nearestDistSqr = float.MaxValue;
        AAMissile nearest = null;

        if (myFaction == Faction.Player || myFaction == Faction.Ally)
        {
            nearest = FindNearestInMissileList(_foeMissiles, position, rangeSqr, ref nearestDistSqr);
        }
        else if (myFaction == Faction.Foe)
        {
            nearest = FindNearestInMissileList(_playerMissiles, position, rangeSqr, ref nearestDistSqr);
            AAMissile allyNearest = FindNearestInMissileList(_allyMissiles, position, rangeSqr, ref nearestDistSqr);
            if (allyNearest != null) nearest = allyNearest;
        }

        return nearest;
    }

    private static AAMissile FindNearestInMissileList(List<AAMissile> missiles, Vector3 position, float rangeSqr, ref float nearestDistSqr)
    {
        AAMissile nearest = null;

        for (int i = missiles.Count - 1; i >= 0; i--)
        {
            AAMissile missile = missiles[i];

            if (missile == null)
            {
                missiles.RemoveAt(i);
                continue;
            }

            float distSqr = (missile.transform.position - position).sqrMagnitude;
            if (distSqr <= rangeSqr && distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = missile;
            }
        }

        return nearest;
    }

    private static void AddNearbyMissiles(List<AAMissile> missiles, Vector3 position, float rangeSqr, List<AAMissile> results)
    {
        if (missiles == null) return;

        for (int i = missiles.Count - 1; i >= 0; i--)
        {
            AAMissile missile = missiles[i];

            if (missile == null)
            {
                missiles.RemoveAt(i);
                continue;
            }

            float distSqr = (missile.transform.position - position).sqrMagnitude;
            if (distSqr <= rangeSqr)
                results.Add(missile);
        }
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