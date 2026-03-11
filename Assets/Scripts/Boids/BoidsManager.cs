// BoidsManager.cs - UPDATED with full dynamic support
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.EditorTools;
#endif

public class BoidsManager : MonoBehaviour
{
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader computeShader;
    List<Boid> boids;

    public Transform target;
    [Tooltip("Height range for boid movement (Y axis)")]
    public Vector2 HeightRange = new Vector2(-1000.0f, 1000.0f);

    [Header("Formation")]
    public bool syncCombatState = true;

    [Header("Target Management")]
    [SerializeField] private BoidFlockTargetManager _targetManager;

    [Header("Squad State")]
    [SerializeField] private BoidSquadBlackboard _squadBlackboard;
    [SerializeField] private BoidSquadTacticalBrain _squadTacticalBrain;

    [Header("Hybrid AI Migration")]
    [SerializeField] private bool _useSquadBlackboard = true;
    [SerializeField] private bool _useSquadTacticalBrain = true;
    [SerializeField] private bool _useSquadTacticalBrainLiveOutputs = true;
    [SerializeField] private bool _useHybridTargetFocusPolicy = true;
    [SerializeField] private bool _useIndividualTacticalLayer = true;
    [SerializeField] private bool _useHybridProfileSemantics = true;
    [SerializeField] private int _migrationFlagsVersion = 1;

    [Header("Performance")]
    [Tooltip("Use async GPU readback to avoid CPU stall. Uses previous frame data if not ready.")]
    public bool UseAsyncReadback = true;
    [Tooltip("Only update weapons every N frames.")]
    [Min(1)] public int WeaponUpdateIntervalFrames = 2;
    [Tooltip("Only cleanup destroyed boids every N frames.")]
    [Min(1)] public int CleanupIntervalFrames = 5;
    [Tooltip("Only evaluate combat sync every N frames.")]
    [Min(1)] public int CombatSyncIntervalFrames = 2;

#if UNITY_EDITOR
    [Header("Debug")]
    [SerializeField] private bool _drawDebugGizmos = false;
    [SerializeField] private bool _drawBoidIndexLabels = false;
#endif

    [Header("Flock Identity")]
    [SerializeField] private string _flockId = "Flock_01";
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Player;
    [SerializeField] private List<string> _targetTags = new List<string>();
    [SerializeField] private List<string> _ignoreTags = new List<string>();
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private float _detectionRadius = 5000f;

    private FormationType _lastFormationType;
    private bool _lastUseFormation;

    private List<WeaponBase> _boidWeapons = new List<WeaponBase>();
    private Boid _formationLeader = null;
    private BoidCommandController _commandController;

    private bool _wasAnyInCombat = false;

    private List<BoidSpawner> _spawners = new List<BoidSpawner>();

    // Compute resources
    private BoidData[] _boidData;
    private ComputeBuffer _boidBuffer;
    private int _cachedBoidCount = 0;
    private bool _readbackPending = false;
    private int _readbackBoidCount = 0; // Size of the buffer being read back
    private int _weaponUpdateCounter = 0;
    private int _cleanupCounter = 0;
    private int _combatSyncCounter = 0;

    // Track when formation needs reassignment
    private bool _formationDirty = false;
    private float _formationDirtyTimer = 0f;
    private const float FormationReassignDelay = 0.1f; // Small delay to batch changes

    // Events for external listeners
    public System.Action<Boid> OnBoidAdded;
    public System.Action<Boid> OnBoidRemoved;
    public System.Action OnFlockChanged;

    void Awake()
    {
        EnsureMigrationFlagsInitialized();
    }

    void Start()
    {
        EnsureMigrationFlagsInitialized();

        if (_targetManager == null)
        {
            _targetManager = gameObject.AddComponent<BoidFlockTargetManager>();
        }

        if (_squadBlackboard == null)
        {
            _squadBlackboard = GetComponent<BoidSquadBlackboard>();
        }

        if (_squadBlackboard == null)
        {
            _squadBlackboard = gameObject.AddComponent<BoidSquadBlackboard>();
        }

        if (_squadTacticalBrain == null)
        {
            _squadTacticalBrain = GetComponent<BoidSquadTacticalBrain>();
        }

        if (_squadTacticalBrain == null)
        {
            _squadTacticalBrain = gameObject.AddComponent<BoidSquadTacticalBrain>();
        }

        RefreshMigrationRuntimeState();

        _commandController = GetComponent<BoidCommandController>();

        _targetManager.Initialize(_flockId, _team, _detectionRadius, _targetTags, _ignoreTags);
        _targetManager.SetCommandAnchor(target);
        _squadBlackboard.Initialize(_flockId, _team, settings != null && settings.useFormation, settings != null ? settings.combatAnchorMode : CombatAnchorMode.None);

        boids = new List<Boid>();
        _boidWeapons = new List<WeaponBase>();

        var spawners = GetComponentsInChildren<BoidSpawner>();
        foreach (BoidSpawner spawner in spawners)
        {
            _spawners.Add(spawner);

            spawner.OnBoidSpawned += OnBoidSpawned;
            spawner.OnSpawningComplete += OnSpawnerComplete;

            if (spawner.SpawnedObjects != null)
            {
                foreach (GameObject boidObj in spawner.SpawnedObjects)
                {
                    RegisterBoidInternal(boidObj);
                }
            }
        }

        AssignFormationPositions();

        _lastFormationType = settings.formationType;
        _lastUseFormation = settings.useFormation;

        UpdateSquadBlackboard();
        ApplyHybridTargetingPolicy();
    }

    void OnDestroy()
    {
        foreach (var spawner in _spawners)
        {
            if (spawner != null)
            {
                spawner.OnBoidSpawned -= OnBoidSpawned;
                spawner.OnSpawningComplete -= OnSpawnerComplete;
            }
        }

        if (_boidBuffer != null)
        {
            _boidBuffer.Release();
            _boidBuffer = null;
        }
    }

    private void OnBoidSpawned(Boid boid)
    {
        if (boid == null) return;

        RegisterBoidInternal(boid.gameObject);
        MarkFormationDirty();
    }

    private void OnSpawnerComplete()
    {
        AssignFormationPositions();
    }

    private void RegisterBoidInternal(GameObject boidObj)
    {
        if (boidObj == null) return;

        var boid = boidObj.GetComponent<Boid>();
        if (boid == null) return;

        if (boids.Contains(boid)) return;

        boids.Add(boid);
        boid.Initialize(settings, target);
        boid.SetTargetManager(_targetManager);

        var vehicle = boidObj.GetComponent<VehicleBase>();
        if (vehicle != null)
        {
            vehicle.BoidManager = this;
        }

        _targetManager.RegisterBoid(boid);

        var weapons = boidObj.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            if (!_boidWeapons.Contains(weapon))
            {
                weapon.UseManagedUpdates = false;
                _boidWeapons.Add(weapon);
            }
        }

        OnBoidAdded?.Invoke(boid);
    }

    /// <summary>
    /// Add a boid to this flock at runtime.
    /// </summary>
    public void AddBoid(Boid boid)
    {
        if (boid == null || boids.Contains(boid)) return;

        RegisterBoidInternal(boid.gameObject);
        MarkFormationDirty();
    }

    /// <summary>
    /// Add multiple boids at runtime.
    /// </summary>
    public void AddBoids(IEnumerable<Boid> newBoids)
    {
        bool anyAdded = false;

        foreach (var boid in newBoids)
        {
            if (boid != null && !boids.Contains(boid))
            {
                RegisterBoidInternal(boid.gameObject);
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            MarkFormationDirty();
        }
    }

    /// <summary>
    /// Remove a boid from this flock.
    /// </summary>
    public void RemoveBoid(Boid boid)
    {
        if (boid == null) return;

        bool wasLeader = (boid == _formationLeader);

        _targetManager.UnregisterBoid(boid);
        boids.Remove(boid);

        var weapons = boid.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            _boidWeapons.Remove(weapon);
        }

        OnBoidRemoved?.Invoke(boid);

        if (wasLeader || boids.Count > 0)
        {
            MarkFormationDirty();
        }
    }

    /// <summary>
    /// Transfer a boid to another flock manager.
    /// </summary>
    public void TransferBoid(Boid boid, BoidsManager targetFlock)
    {
        if (boid == null || targetFlock == null || targetFlock == this) return;
        if (!boids.Contains(boid)) return;

        // Remove from this flock
        _targetManager.UnregisterBoid(boid);
        boids.Remove(boid);

        var weapons = boid.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            _boidWeapons.Remove(weapon);
        }

        // Add to target flock
        targetFlock.AddBoid(boid);

        MarkFormationDirty();
    }

    /// <summary>
    /// Mark formation as needing reassignment (batched for performance).
    /// </summary>
    private void MarkFormationDirty()
    {
        _formationDirty = true;
        _formationDirtyTimer = FormationReassignDelay;
    }

    void AssignFormationPositions()
    {
        boids.RemoveAll(b => b == null);

        if (boids.Count == 0)
        {
            _formationLeader = null;
            OnFlockChanged?.Invoke();
            return;
        }

        _formationLeader = boids[0];
        _formationLeader.FormationIndex = 0;
        _formationLeader.FormationLeader = null;
        _formationLeader.OnFormationChanged();

        for (int i = 1; i < boids.Count; i++)
        {
            boids[i].FormationIndex = i;
            boids[i].FormationLeader = _formationLeader;
            boids[i].OnFormationChanged();
        }

        _formationDirty = false;
        OnFlockChanged?.Invoke();
    }

    void Update()
    {
        if (boids == null)
            return;

        RefreshMigrationRuntimeState();

        // Handle deferred formation reassignment
        if (_formationDirty)
        {
            _formationDirtyTimer -= Time.deltaTime;
            if (_formationDirtyTimer <= 0f)
            {
                AssignFormationPositions();
            }
        }

        _cleanupCounter++;
        if (_cleanupCounter >= CleanupIntervalFrames)
        {
            _cleanupCounter = 0;
            CleanupDestroyedBoids();
        }

        int numBoids = boids.Count;
        if (numBoids <= 0)
        {
            UpdateSquadBlackboard();
            return;
        }

        _combatSyncCounter++;
        if (_combatSyncCounter >= CombatSyncIntervalFrames)
        {
            _combatSyncCounter = 0;
            // Check combat state once
            bool anyInCombat = false;
            foreach (var boid in boids)
            {
                if (boid != null && boid.IsInCombat)
                {
                    anyInCombat = true;
                    break;
                }
            }

            if (syncCombatState && anyInCombat && !_wasAnyInCombat)
            {
                foreach (var boid in boids)
                {
                    if (boid != null)
                        boid.EnterCombat();
                }
            }

            if (_wasAnyInCombat && !anyInCombat)
            {
                AssignFormationPositions();
            }
            _wasAnyInCombat = anyInCombat;
        }

        EnsureComputeResources(numBoids);

        int boidCountSnapshot = boids.Count;
        int copyCount = Mathf.Min(numBoids, boidCountSnapshot);
        for (int i = 0; i < copyCount; i++)
        {
            Boid boid = boids[i];
            if (boid == null) continue;
            _boidData[i].position = boid.position;
            _boidData[i].direction = boid.forward;
        }

        _boidBuffer.SetData(_boidData, 0, 0, numBoids);

        computeShader.SetBuffer(0, "boids", _boidBuffer);
        computeShader.SetInt("numBoids", numBoids);
        computeShader.SetFloat("viewRadius", settings.perceptionRadius);
        computeShader.SetVector("heightRange", HeightRange);
        int threadGroups = Mathf.CeilToInt(numBoids / (float)threadGroupSize);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        if (UseAsyncReadback)
        {
            if (!_readbackPending)
            {
                _readbackPending = true;
                _readbackBoidCount = numBoids; // Store size of buffer being read
                AsyncGPUReadback.Request(_boidBuffer, OnBoidReadbackComplete);
            }
        }
        else
        {
            _boidBuffer.GetData(_boidData, 0, 0, numBoids);
        }

        for (int i = 0; i < numBoids; i++)
        {
            if (i >= boids.Count || boids[i] == null) continue;

            boids[i].avgFlockHeading = _boidData[i].flockHeading;
            boids[i].avgAvoidanceHeading = _boidData[i].seperationHeading;
            boids[i].flockmatesCenter = _boidData[i].flockCenter;
            boids[i].numPerceivedFlockmates = _boidData[i].numFlockmates;

            boids[i].UpdateBoid();
        }

        if (settings.formationType != _lastFormationType || settings.useFormation != _lastUseFormation)
        {
            _lastFormationType = settings.formationType;
            _lastUseFormation = settings.useFormation;

            foreach (var boid in boids)
            {
                if (boid != null)
                    boid.OnFormationChanged();
            }
        }

        _weaponUpdateCounter++;
        if (_weaponUpdateCounter >= WeaponUpdateIntervalFrames)
        {
            _weaponUpdateCounter = 0;
            UpdateBoidWeapons();
        }

        UpdateSquadBlackboard();
        ApplyHybridTargetingPolicy();
    }

    private void RefreshMigrationRuntimeState()
    {
        if (_squadTacticalBrain != null && _squadTacticalBrain.enabled != UseSquadTacticalBrain)
        {
            _squadTacticalBrain.enabled = UseSquadTacticalBrain;
        }
    }

    private void OnBoidReadbackComplete(AsyncGPUReadbackRequest request)
    {
        _readbackPending = false;
        if (request.hasError || _boidData == null)
            return;

        var gpuData = request.GetData<BoidData>();
        int copyCount = Mathf.Min(_readbackBoidCount, _boidData.Length, gpuData.Length);
        for (int i = 0; i < copyCount; i++)
        {
            _boidData[i] = gpuData[i];
        }
    }

    private void EnsureMigrationFlagsInitialized()
    {
        if (_migrationFlagsVersion >= 1)
            return;

        _useSquadBlackboard = true;
        _useSquadTacticalBrain = true;
        _useSquadTacticalBrainLiveOutputs = true;
        _useHybridTargetFocusPolicy = true;
        _useIndividualTacticalLayer = true;
        _useHybridProfileSemantics = true;
        _migrationFlagsVersion = 1;
    }

    private void UpdateSquadBlackboard()
    {
        if (!UseSquadBlackboard || _squadBlackboard == null)
            return;

        PublishBlackboardCommandState();
        PublishBlackboardTargetState();
        PublishBlackboardSquadMetrics();
    }

    private void ApplyHybridTargetingPolicy()
    {
        if (_targetManager == null)
            return;

        if (UseHybridTargetFocusPolicy && _squadBlackboard != null)
        {
            _targetManager.SetTacticalPolicy(
                _squadBlackboard.DesiredFocusTarget,
                _squadBlackboard.DesiredCombatSpread,
                _squadBlackboard.DesiredAggression);
            return;
        }

        _targetManager.ClearTacticalPolicy();
    }

    private void PublishBlackboardCommandState()
    {
        if (_commandController != null)
        {
            _squadBlackboard.PublishCommandState(
                _commandController.CurrentCommandType,
                _commandController.CurrentCommandTarget,
                _commandController.CurrentCommandPosition,
                _commandController.CurrentCommandRadius,
                _commandController.LastCommandIssuedTime);
            return;
        }

        _squadBlackboard.PublishCommandState(BoidCommandType.None, null, Vector3.zero, 0f, -1f);
    }

    private void PublishBlackboardTargetState()
    {
        Vector3 flockCenter = _targetManager != null ? _targetManager.GetFlockCenterPosition() : transform.position;

        if (_targetManager != null)
        {
            _targetManager.GetTargetSummary(
                out int knownTargetCount,
                out Transform primaryThreat,
                out float nearestThreatDistance,
                out float hostilePressureScore,
                out bool defenseIntrusionDetected,
                flockCenter);

            _squadBlackboard.PublishTargetState(
                _targetManager.CommandAnchor,
                _targetManager.DefendTarget,
                _targetManager.PriorityTarget,
                _targetManager.IsDefenseMode,
                defenseIntrusionDetected,
                knownTargetCount,
                primaryThreat,
                nearestThreatDistance,
                hostilePressureScore,
                flockCenter);

            return;
        }

        _squadBlackboard.PublishTargetState(null, null, null, false, false, 0, null, -1f, 0f, flockCenter);
    }

    private void PublishBlackboardSquadMetrics()
    {
        int aliveBoidCount = 0;
        int combatCapableBoidCount = 0;
        int outOfEnvelopeBoidCount = 0;
        float totalSpreadDistance = 0f;
        float totalAnchorDistance = 0f;
        float totalHullPercent = 0f;
        int hullSampleCount = 0;

        Vector3 flockCenter = _targetManager != null ? _targetManager.GetFlockCenterPosition() : transform.position;
        float cohesionReference = 1f;
        float leashRadius = 0f;

        if (settings != null)
        {
            float combatReference = Mathf.Max(
                settings.combatAnchorSlackRadius + settings.combatRegroupHysteresis,
                settings.combatLeashRadius * 0.85f);
            float formationReference = Mathf.Max(
                settings.formationSpacing * 3f,
                settings.formationUrgencyRange * 1.5f);
            cohesionReference = Mathf.Max(1f, combatReference, formationReference);
            leashRadius = Mathf.Max(0f, settings.combatLeashRadius);
        }

        CombatAnchorMode metricsAnchorMode = settings != null ? settings.combatAnchorMode : CombatAnchorMode.None;
        if (_squadBlackboard != null && _useSquadTacticalBrainLiveOutputs && _squadBlackboard.DesiredAnchorMode != CombatAnchorMode.None)
        {
            metricsAnchorMode = _squadBlackboard.DesiredAnchorMode;
        }

        for (int i = 0; i < boids.Count; i++)
        {
            Boid boid = boids[i];
            if (boid == null)
                continue;

            aliveBoidCount++;
            if (!boid.IsDespawning)
            {
                combatCapableBoidCount++;
            }

            VehicleBase vehicle = boid.Vehicle;
            if (vehicle != null && vehicle.MaxHitPoints > 0)
            {
                totalHullPercent += Mathf.Clamp01(vehicle.HitPoints / (float)vehicle.MaxHitPoints);
                hullSampleCount++;
            }

            totalSpreadDistance += Vector3.Distance(boid.position, flockCenter);

            if (_targetManager != null && _targetManager.TryGetCombatAnchorPosition(boid, metricsAnchorMode, out Vector3 anchorPosition))
            {
                float anchorDistance = Vector3.Distance(boid.position, anchorPosition);
                totalAnchorDistance += anchorDistance;

                if (leashRadius > 0f && anchorDistance > leashRadius)
                {
                    outOfEnvelopeBoidCount++;
                }
            }
        }

        float averageSpreadDistance = aliveBoidCount > 0 ? totalSpreadDistance / aliveBoidCount : 0f;
        float averageHullPercent = hullSampleCount > 0 ? totalHullPercent / hullSampleCount : 1f;
        float averageAnchorDistance = aliveBoidCount > 0 ? totalAnchorDistance / aliveBoidCount : 0f;
        float cohesionScore = 1f - Mathf.Clamp01(averageSpreadDistance / cohesionReference);

        _squadBlackboard.PublishSquadMetrics(
            aliveBoidCount,
            combatCapableBoidCount,
            cohesionScore,
            averageHullPercent,
            averageAnchorDistance,
            outOfEnvelopeBoidCount);
    }

    private void EnsureComputeResources(int numBoids)
    {
        if (_boidData == null || _cachedBoidCount != numBoids)
        {
            _boidData = new BoidData[numBoids];
            _cachedBoidCount = numBoids;

            if (_boidBuffer != null)
            {
                _boidBuffer.Release();
                _boidBuffer = null;
            }

            _boidBuffer = new ComputeBuffer(numBoids, BoidData.Size);
        }
    }

    /// <summary>
    /// Returns number of boids removed.
    /// </summary>
    private int CleanupDestroyedBoids()
    {
        bool leaderRemoved = false;
        int removedCount = 0;

        for (int i = boids.Count - 1; i >= 0; i--)
        {
            if (boids[i] == null)
            {
                if (i == 0) leaderRemoved = true;
                boids.RemoveAt(i);
                removedCount++;
            }
        }

        if (removedCount > 0 && (leaderRemoved || _formationDirty))
        {
            MarkFormationDirty();
        }

        return removedCount;
    }

    private void UpdateBoidWeapons()
    {
        for (int i = _boidWeapons.Count - 1; i >= 0; i--)
        {
            if (_boidWeapons[i] == null)
            {
                _boidWeapons.RemoveAt(i);
                continue;
            }
            _boidWeapons[i].ManagedUpdateTarget();
        }
    }

    public void UpdateBoidList()
    {
        boids.Clear();
        _boidWeapons.Clear();

        foreach (BoidSpawner spawner in _spawners)
        {
            if (spawner == null || spawner.SpawnedObjects == null) continue;

            foreach (GameObject boidObj in spawner.SpawnedObjects)
            {
                RegisterBoidInternal(boidObj);
            }
        }

        AssignFormationPositions();
    }

    public void SetFormationType(FormationType type)
    {
        settings.formationType = type;

        foreach (var boid in boids)
        {
            if (boid != null)
            {
                boid.OnFormationChanged();
            }
        }
    }

    public void SetUseFormation(bool useFormation)
    {
        settings.useFormation = useFormation;

        foreach (var boid in boids)
        {
            if (boid != null)
            {
                boid.OnFormationChanged();
            }
        }
    }

    public void ForceFormationMode()
    {
        foreach (var boid in boids)
        {
            if (boid != null)
                boid.IsInCombat = false;
        }
    }

    /// <summary>
    /// Get a spawner to spawn additional boids.
    /// </summary>
    public BoidSpawner GetSpawner(int index = 0)
    {
        if (index >= 0 && index < _spawners.Count)
            return _spawners[index];
        return null;
    }

    /// <summary>
    /// Spawn additional boids using the first spawner.
    /// </summary>
    public void SpawnAdditional(int count)
    {
        if (_spawners.Count > 0 && _spawners[0] != null)
        {
            _spawners[0].SpawnAdditionalSequential(count);
        }
    }

    /// <summary>
    /// Pause all spawners from spawning new boids.
    /// </summary>
    public void PauseSpawning()
    {
        foreach (var spawner in _spawners)
        {
            if (spawner != null)
                spawner.Pause();
        }
    }

    /// <summary>
    /// Resume spawning on all spawners.
    /// </summary>
    public void ResumeSpawning()
    {
        foreach (var spawner in _spawners)
        {
            if (spawner != null)
                spawner.Resume();
        }
    }

    /// <summary>
    /// Spawn boids using the specified spawner.
    /// </summary>
    public void SpawnBoids(int count, int spawnerIndex = 0)
    {
        if (spawnerIndex >= 0 && spawnerIndex < _spawners.Count && _spawners[spawnerIndex] != null)
        {
            _spawners[spawnerIndex].Resume(); // Ensure not paused
            _spawners[spawnerIndex].SpawnAdditionalSequential(count);
        }
    }

    /// <summary>
    /// Get the default spawn count from the first spawner.
    /// </summary>
    public int GetDefaultSpawnCount()
    {
        if (_spawners.Count > 0 && _spawners[0] != null)
            return _spawners[0].spawnCount;
        return 0;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (_targetManager != null)
        {
            _targetManager.SetCommandAnchor(newTarget);
        }
        foreach (var boid in boids)
        {
            boid.SetFallbackTarget(newTarget);
        }
    }

    // Properties
    public int BoidCount => boids?.Count ?? 0;
    public IReadOnlyList<Boid> Boids => boids;
    public Boid Leader => _formationLeader;
    public BoidFlockTargetManager TargetManager => _targetManager;
    public BoidSquadBlackboard SquadBlackboard => _squadBlackboard;
    public BoidSquadTacticalBrain SquadTacticalBrain => _squadTacticalBrain;
    public bool UseSquadBlackboard => _useSquadBlackboard || _useSquadTacticalBrain || _useSquadTacticalBrainLiveOutputs || _useHybridTargetFocusPolicy || _useIndividualTacticalLayer;
    public bool UseSquadTacticalBrain => UseSquadBlackboard && _useSquadTacticalBrain;
    public bool UseSquadTacticalBrainLiveOutputs => UseSquadTacticalBrain && _useSquadTacticalBrainLiveOutputs;
    public bool UseHybridTargetFocusPolicy => UseSquadTacticalBrainLiveOutputs && _useHybridTargetFocusPolicy;
    public bool UseIndividualTacticalLayer => _useIndividualTacticalLayer;
    public bool UseHybridProfileSemantics => _useHybridProfileSemantics;

    public struct BoidData
    {
        public Vector3 position;
        public Vector3 direction;
        public Vector3 flockHeading;
        public Vector3 flockCenter;
        public Vector3 seperationHeading;
        public int numFlockmates;

        public static int Size => sizeof(float) * 3 * 5 + sizeof(int);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!_drawDebugGizmos || boids == null) return;

        if (_formationLeader != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_formationLeader.position, 30f);
        }

        Gizmos.color = _wasAnyInCombat ? Color.red : Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 50f);
    }

    void OnDrawGizmosSelected()
    {
        if (!_drawDebugGizmos || boids == null) return;

        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] == null) continue;

            Gizmos.color = (i == 0) ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(boids[i].position, 8f);

            if (_drawBoidIndexLabels)
            {
                UnityEditor.Handles.Label(boids[i].position + Vector3.up * 25f, $"#{i}");
            }
        }
    }
#endif
}