using System.Collections.Generic;
using UnityEngine;

public class MissionObjectivePanelController : MonoBehaviour
{
    private const string ProtectBattleshipObjectiveId = "protect_battleship";
    private const string DestroyAllEnemiesObjectiveId = "destroy_all_enemy";

    [Header("Layout")]
    [SerializeField] private Transform _rowContainer;
    [SerializeField] private MissionObjectiveRowView _rowTemplate;
    [SerializeField] private bool _hideTemplateOnStart = true;

    [Header("Objectives")]
    [SerializeField] private bool _showProtectBattleshipObjective = true;
    [SerializeField] private BoidsManager _battleshipSourceManager;
    [Min(1f)]
    [SerializeField] private float _protectDurationSeconds = 600f;
    [SerializeField] private bool _showDestroyAllEnemiesObjective = true;
    [SerializeField] private List<BoidsManager> _enemySourceManagers = new List<BoidsManager>();
    [Min(0.1f)]
    [SerializeField] private float _refreshInterval = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool _debugMode = false;
    [Min(0f)]
    [SerializeField] private float _debugProtectTimeRemaining = 600f;
    [SerializeField] private MissionObjectiveStatus _debugProtectStatus = MissionObjectiveStatus.Active;
    [Min(0)]
    [SerializeField] private int _debugEnemyRemaining = 30;
    [SerializeField] private MissionObjectiveStatus _debugEnemyStatus = MissionObjectiveStatus.Active;

    private readonly Dictionary<string, MissionObjectiveRowView> _rows = new Dictionary<string, MissionObjectiveRowView>();
    private readonly List<MissionObjectiveViewData> _objectives = new List<MissionObjectiveViewData>(4);
    private readonly List<string> _missingIds = new List<string>(4);

    private EnemyVehicle _trackedBattleship;
    private float _protectTimeRemaining;
    private float _refreshTimer;
    private bool _battleshipWasResolved;
    private bool _protectObjectiveFailed;
    private bool _protectObjectiveCompleted;
    private bool _enemyObjectiveCompleted;
    private int _enemyRemainingCount;

    private void Awake()
    {
        if (_rowContainer == null)
            _rowContainer = transform;

        if (_rowTemplate == null)
            _rowTemplate = GetComponentInChildren<MissionObjectiveRowView>(true);

        ResetRuntimeState();

        if (_hideTemplateOnStart && _rowTemplate != null)
            _rowTemplate.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_debugMode)
        {
            BuildDebugObjectives();
            SyncRows();
            return;
        }

        UpdateRuntimeState();
        SyncRows();
    }

    public void SetDebugMode(bool enabled)
    {
        if (_debugMode == enabled)
            return;

        _debugMode = enabled;
        ResetRuntimeState();
    }

    private void ResetRuntimeState()
    {
        _trackedBattleship = null;
        _protectTimeRemaining = Mathf.Max(1f, _protectDurationSeconds);
        _refreshTimer = 0f;
        _battleshipWasResolved = false;
        _protectObjectiveFailed = false;
        _protectObjectiveCompleted = false;
        _enemyObjectiveCompleted = false;
        _enemyRemainingCount = 0;
        _objectives.Clear();
        RefreshRuntimeSources();
    }

    private void UpdateRuntimeState()
    {
        if (_showProtectBattleshipObjective && !_protectObjectiveFailed && !_protectObjectiveCompleted)
        {
            UpdateProtectBattleshipObjective();
        }

        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = Mathf.Max(0.1f, _refreshInterval);
            RefreshRuntimeSources();
        }

        BuildRuntimeObjectives();
    }

    private void UpdateProtectBattleshipObjective()
    {
        if (_trackedBattleship == null)
        {
            if (_battleshipWasResolved)
                _protectObjectiveFailed = true;

            return;
        }

        if (_trackedBattleship.IsDying || _trackedBattleship.HitPoints <= 0)
        {
            _protectObjectiveFailed = true;
            return;
        }

        _protectTimeRemaining = Mathf.Max(0f, _protectTimeRemaining - Time.deltaTime);
        if (_protectTimeRemaining <= 0f)
        {
            _protectObjectiveCompleted = true;
        }
    }

    private void RefreshRuntimeSources()
    {
        if (_showProtectBattleshipObjective && _trackedBattleship == null && !_protectObjectiveFailed)
        {
            _trackedBattleship = FindAllyBattleship();
            _battleshipWasResolved |= _trackedBattleship != null;
        }

        if (_showDestroyAllEnemiesObjective)
        {
            _enemyRemainingCount = ResolveEnemyRemainingCount();
            _enemyObjectiveCompleted = _enemyRemainingCount <= 0;
        }
    }

    private void BuildRuntimeObjectives()
    {
        _objectives.Clear();

        if (_showProtectBattleshipObjective)
        {
            MissionObjectiveStatus status = MissionObjectiveStatus.Active;
            if (_protectObjectiveCompleted)
                status = MissionObjectiveStatus.Completed;
            else if (_protectObjectiveFailed)
                status = MissionObjectiveStatus.Failed;

            string detail = _battleshipWasResolved || _trackedBattleship != null
                ? $"Time remaining: {FormatTimer(_protectTimeRemaining)}"
                : GetBattleshipAwaitingDetail();

            _objectives.Add(new MissionObjectiveViewData(
                ProtectBattleshipObjectiveId,
                "Protect Battleship",
                detail,
                status,
                0));
        }

        if (_showDestroyAllEnemiesObjective)
        {
            MissionObjectiveStatus status = _enemyObjectiveCompleted
                ? MissionObjectiveStatus.Completed
                : MissionObjectiveStatus.Active;

            _objectives.Add(new MissionObjectiveViewData(
                DestroyAllEnemiesObjectiveId,
                "Destroy All Enemy",
                $"Remaining: {_enemyRemainingCount}",
                status,
                1));
        }
    }

    private void BuildDebugObjectives()
    {
        _objectives.Clear();

        if (_showProtectBattleshipObjective)
        {
            _objectives.Add(new MissionObjectiveViewData(
                ProtectBattleshipObjectiveId,
                "Protect Battleship",
                $"Time remaining: {FormatTimer(_debugProtectTimeRemaining)}",
                _debugProtectStatus,
                0));
        }

        if (_showDestroyAllEnemiesObjective)
        {
            _objectives.Add(new MissionObjectiveViewData(
                DestroyAllEnemiesObjectiveId,
                "Destroy All Enemy",
                $"Remaining: {Mathf.Max(0, _debugEnemyRemaining)}",
                _debugEnemyStatus,
                1));
        }
    }

    private void SyncRows()
    {
        _objectives.Sort(CompareObjectives);

        for (int index = 0; index < _objectives.Count; index++)
        {
            MissionObjectiveViewData data = _objectives[index];
            MissionObjectiveRowView row = GetOrCreateRow(data.Id);
            if (row == null)
                continue;

            row.Apply(data);
            row.transform.SetSiblingIndex(index + (_hideTemplateOnStart && _rowTemplate != null ? 1 : 0));
            if (!row.gameObject.activeSelf)
                row.gameObject.SetActive(true);
        }

        _missingIds.Clear();
        foreach (KeyValuePair<string, MissionObjectiveRowView> pair in _rows)
        {
            bool found = false;
            for (int i = 0; i < _objectives.Count; i++)
            {
                if (_objectives[i].Id == pair.Key)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                _missingIds.Add(pair.Key);
        }

        for (int i = 0; i < _missingIds.Count; i++)
        {
            string id = _missingIds[i];
            if (_rows.TryGetValue(id, out MissionObjectiveRowView row))
            {
                Destroy(row.gameObject);
            }

            _rows.Remove(id);
        }
    }

    private MissionObjectiveRowView GetOrCreateRow(string objectiveId)
    {
        if (_rows.TryGetValue(objectiveId, out MissionObjectiveRowView existingRow) && existingRow != null)
            return existingRow;

        if (_rowTemplate == null || _rowContainer == null)
        {
            Debug.LogError($"{name}: Mission objective panel is missing its row template or container.");
            return null;
        }

        MissionObjectiveRowView instance = Instantiate(_rowTemplate, _rowContainer);
        instance.name = $"ObjectiveRow_{objectiveId}";
        instance.gameObject.SetActive(true);
        _rows[objectiveId] = instance;
        return instance;
    }

    private int ResolveEnemyRemainingCount()
    {
        if (HasAssignedEnemySources())
            return Mathf.Max(0, ResolveEnemyRemainingFromManagers());

        int registryCount = CombatRegistry.CountVehicles(GlobalHelper.Faction.Foe);
        if (registryCount > 0)
            return registryCount;

        EnemyVehicle[] vehicles = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None);
        int remaining = 0;
        for (int i = 0; i < vehicles.Length; i++)
        {
            EnemyVehicle vehicle = vehicles[i];
            if (vehicle == null)
                continue;

            if (vehicle.VehicleFaction == GlobalHelper.Faction.Foe && !vehicle.IsDying)
                remaining++;
        }

        return remaining;
    }

    private int ResolveEnemyRemainingFromManagers()
    {
        int total = 0;

        for (int managerIndex = 0; managerIndex < _enemySourceManagers.Count; managerIndex++)
        {
            BoidsManager manager = _enemySourceManagers[managerIndex];
            if (manager == null)
                continue;

            total += CountLiveVehicles(manager.Boids, GlobalHelper.Faction.Foe);
            total += CountLiveVehicles(manager.DockedBoids, GlobalHelper.Faction.Foe);
            total += CountPendingInitialSpawns(manager, GlobalHelper.Faction.Foe);
        }

        return total;
    }

    private EnemyVehicle FindAllyBattleship()
    {
        if (_battleshipSourceManager != null)
            return FindBattleshipInManager(_battleshipSourceManager);

        VehicleBase registeredBattleship = CombatRegistry.FindFirstVehicle(
            GlobalHelper.Faction.Ally,
            GlobalHelper.VehicleType.Battleship);

        if (registeredBattleship is EnemyVehicle enemyVehicle && !enemyVehicle.IsDying)
            return enemyVehicle;

        EnemyVehicle[] vehicles = FindObjectsByType<EnemyVehicle>(FindObjectsSortMode.None);
        for (int i = 0; i < vehicles.Length; i++)
        {
            EnemyVehicle vehicle = vehicles[i];
            if (vehicle == null)
                continue;

            if (vehicle.VehicleFaction != GlobalHelper.Faction.Ally)
                continue;

            if (vehicle.VehicleType != GlobalHelper.VehicleType.Battleship)
                continue;

            if (vehicle.IsDying || vehicle.HitPoints <= 0)
                continue;

            return vehicle;
        }

        return null;
    }

    private EnemyVehicle FindBattleshipInManager(BoidsManager manager)
    {
        EnemyVehicle vehicle = FindBattleshipInBoids(manager.Boids);
        if (vehicle != null)
            return vehicle;

        return FindBattleshipInBoids(manager.DockedBoids);
    }

    private static EnemyVehicle FindBattleshipInBoids(IReadOnlyList<Boid> boids)
    {
        if (boids == null)
            return null;

        for (int index = 0; index < boids.Count; index++)
        {
            Boid boid = boids[index];
            if (boid == null)
                continue;

            EnemyVehicle vehicle = boid.GetComponent<EnemyVehicle>();
            if (vehicle == null)
                continue;

            if (vehicle.VehicleFaction != GlobalHelper.Faction.Ally)
                continue;

            if (vehicle.VehicleType != GlobalHelper.VehicleType.Battleship)
                continue;

            if (vehicle.IsDying || vehicle.HitPoints <= 0)
                continue;

            return vehicle;
        }

        return null;
    }

    private static int CountLiveVehicles(IReadOnlyList<Boid> boids, GlobalHelper.Faction faction)
    {
        if (boids == null)
            return 0;

        int total = 0;
        for (int index = 0; index < boids.Count; index++)
        {
            Boid boid = boids[index];
            if (boid == null)
                continue;

            VehicleBase vehicle = boid.GetComponent<VehicleBase>();
            if (vehicle == null)
                continue;

            if (vehicle.FactionType != faction)
                continue;

            if (vehicle.HitPoints <= 0)
                continue;

            if (vehicle is EnemyVehicle enemyVehicle && enemyVehicle.IsDying)
                continue;

            total++;
        }

        return total;
    }

    private static int CountPendingInitialSpawns(BoidsManager manager, GlobalHelper.Faction faction)
    {
        BoidSpawner[] spawners = manager.GetComponentsInChildren<BoidSpawner>(true);
        int pending = 0;

        for (int index = 0; index < spawners.Length; index++)
        {
            BoidSpawner spawner = spawners[index];
            if (spawner == null)
                continue;

            if (!SpawnerProducesFaction(spawner, faction))
                continue;

            int spawnedSoFar = Mathf.Min(spawner.TotalSpawnedCount, spawner.spawnCount);
            pending += Mathf.Max(0, spawner.spawnCount - spawnedSoFar);
        }

        return pending;
    }

    private static bool SpawnerProducesFaction(BoidSpawner spawner, GlobalHelper.Faction faction)
    {
        if (spawner.prefabs == null)
            return false;

        for (int prefabIndex = 0; prefabIndex < spawner.prefabs.Length; prefabIndex++)
        {
            Boid boidPrefab = spawner.prefabs[prefabIndex];
            if (boidPrefab == null)
                continue;

            VehicleBase vehicle = boidPrefab.GetComponent<VehicleBase>();
            if (vehicle != null && vehicle.FactionType == faction)
                return true;
        }

        return false;
    }

    private bool HasAssignedEnemySources()
    {
        for (int index = 0; index < _enemySourceManagers.Count; index++)
        {
            if (_enemySourceManagers[index] != null)
                return true;
        }

        return false;
    }

    private string GetBattleshipAwaitingDetail()
    {
        if (_battleshipSourceManager != null)
            return "Awaiting battleship spawn";

        return "Awaiting target";
    }

    private static int CompareObjectives(MissionObjectiveViewData left, MissionObjectiveViewData right)
    {
        return left.SortPriority.CompareTo(right.SortPriority);
    }

    private static string FormatTimer(float remainingSeconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }
}