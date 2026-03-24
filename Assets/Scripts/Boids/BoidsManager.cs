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

    [Header("Performance")]
    [Tooltip("Use async GPU readback to avoid CPU stall. Uses previous frame data if not ready.")]
    public bool UseAsyncReadback = true;
    [Tooltip("Only update weapons every N frames.")]
    [Min(1)] public int WeaponUpdateIntervalFrames = 2;
    [Tooltip("Maximum boid weapon target updates per frame. Limits CPU spikes.")]
    [Min(1)] public int MaxBoidWeaponUpdatesPerFrame = 8;
    [Tooltip("Only cleanup destroyed boids every N frames.")]
    [Min(1)] public int CleanupIntervalFrames = 5;
    [Tooltip("Only evaluate combat sync every N frames.")]
    [Min(1)] public int CombatSyncIntervalFrames = 2;

    [Header("Flock Identity")]
    [SerializeField] private string _flockId = "Flock_01";
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Player;
    [SerializeField] private List<string> _targetTags = new List<string>();
    [SerializeField] private List<string> _ignoreTags = new List<string>();
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private float _detectionRadius = 5000f;

    private FormationType _lastFormationType;
    private bool _lastUseFormation;
    private bool _lastUseSubFlocks;
    private FormationType _lastSubFlockFormationType;
    private int _lastPreferredSubFlockSize;
    private float _lastSubFlockFormationSpacing;

    private List<WeaponBase> _boidWeapons = new List<WeaponBase>();
    private Boid _formationLeader = null;
    private List<List<Boid>> _subFlocks = new List<List<Boid>>();

    private bool _wasAnyInCombat = false;

    // Adaptive morale
    private List<VehicleBase> _boidVehicles = new List<VehicleBase>();
    private int _initialBoidCount = 0;
    private bool _initialCountSet = false;
    private CombatMorale _currentMorale = CombatMorale.Confident;
    public CombatMorale CurrentMorale => _currentMorale;
    public float CurrentMoraleScore { get; private set; } = 1f;

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
    private int _boidWeaponIndex = 0;

    // Track when formation needs reassignment
    private bool _formationDirty = false;
    private float _formationDirtyTimer = 0f;
    private const float FormationReassignDelay = 0.1f; // Small delay to batch changes

    // Events for external listeners
    public System.Action<Boid> OnBoidAdded;
    public System.Action<Boid> OnBoidRemoved;
    public System.Action OnFlockChanged;

    void Start()
    {
        if (_targetManager == null)
        {
            _targetManager = gameObject.AddComponent<BoidFlockTargetManager>();
        }

        _targetManager.Initialize(_flockId, _team, _detectionRadius, _targetTags, _ignoreTags);
        _targetManager.SetCommandAnchor(target);

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
        _lastUseSubFlocks = settings.useSubFlocks;
        _lastSubFlockFormationType = settings.subFlockFormationType;
        _lastPreferredSubFlockSize = settings.preferredSubFlockSize;
        _lastSubFlockFormationSpacing = settings.subFlockFormationSpacing;
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
        if (!_initialCountSet && boids.Count > 0)
        {
            _initialBoidCount = boids.Count;
            _initialCountSet = true;
        }
    }

    private void RegisterBoidInternal(GameObject boidObj)
    {
        if (boidObj == null) return;

        var boid = boidObj.GetComponent<Boid>();
        if (boid == null) return;

        if (boids.Contains(boid)) return;

        boids.Add(boid);
        boid.SetHeightRange(HeightRange);
        boid.Initialize(settings, target);
        boid.SetTargetManager(_targetManager);

        var vehicle = boidObj.GetComponent<VehicleBase>();
        if (vehicle != null)
        {
            vehicle.BoidManager = this;
        }
        _boidVehicles.Add(vehicle); // May be null if no VehicleBase

        _targetManager.RegisterBoid(boid);

        var weapons = boidObj.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
        {
            if (!_boidWeapons.Contains(weapon))
            {
                weapon.UseManagedUpdates = false;
                CombatManager.Instance?.UnregisterTurret(weapon);
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

        int index = boids.IndexOf(boid);
        _targetManager.UnregisterBoid(boid);
        boids.Remove(boid);
        if (index >= 0 && index < _boidVehicles.Count)
            _boidVehicles.RemoveAt(index);

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
        int index = boids.IndexOf(boid);
        boids.Remove(boid);
        if (index >= 0 && index < _boidVehicles.Count)
            _boidVehicles.RemoveAt(index);

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
        _subFlocks.Clear();

        if (boids.Count == 0)
        {
            _formationLeader = null;
            OnFlockChanged?.Invoke();
            return;
        }

        _formationLeader = boids[0];

        if (settings.useSubFlocks && boids.Count > 1)
        {
            AssignSubFlockFormations();
        }
        else
        {
            AssignFlatFormation();
        }

        _formationDirty = false;
        OnFlockChanged?.Invoke();
    }

    private void AssignFlatFormation()
    {
        _formationLeader.FormationIndex = 0;
        _formationLeader.FormationLeader = null;
        _formationLeader.IsParentFormationTier = false;
        _formationLeader.OnFormationChanged();

        for (int i = 1; i < boids.Count; i++)
        {
            boids[i].FormationIndex = i;
            boids[i].FormationLeader = _formationLeader;
            boids[i].IsParentFormationTier = false;
            boids[i].OnFormationChanged();
        }
    }

    private void AssignSubFlockFormations()
    {
        int totalBoids = boids.Count;
        int preferred = Mathf.Clamp(settings.preferredSubFlockSize, settings.minSubFlockSize, settings.maxSubFlockSize);
        int minSize = Mathf.Max(2, settings.minSubFlockSize);

        // Determine sub-flock sizes
        List<int> subFlockSizes = new List<int>();
        int remaining = totalBoids;

        while (remaining > 0)
        {
            if (remaining <= preferred)
            {
                // Last group: if too small, merge into previous sub-flock
                if (subFlockSizes.Count > 0 && remaining < minSize)
                {
                    subFlockSizes[subFlockSizes.Count - 1] += remaining;
                }
                else
                {
                    subFlockSizes.Add(remaining);
                }
                remaining = 0;
            }
            else if (remaining - preferred < minSize && remaining - preferred > 0)
            {
                // Next chunk would leave a remainder too small — split evenly
                int half1 = remaining / 2;
                int half2 = remaining - half1;
                subFlockSizes.Add(half1);
                subFlockSizes.Add(half2);
                remaining = 0;
            }
            else
            {
                subFlockSizes.Add(preferred);
                remaining -= preferred;
            }
        }

        // Clamp any oversized sub-flocks
        for (int i = 0; i < subFlockSizes.Count; i++)
        {
            if (subFlockSizes[i] > settings.maxSubFlockSize)
            {
                subFlockSizes[i] = settings.maxSubFlockSize;
            }
        }

        // Assign boids to sub-flocks
        int boidIndex = 0;
        for (int sf = 0; sf < subFlockSizes.Count; sf++)
        {
            int size = subFlockSizes[sf];
            List<Boid> subFlock = new List<Boid>(size);

            for (int j = 0; j < size && boidIndex < totalBoids; j++, boidIndex++)
            {
                subFlock.Add(boids[boidIndex]);
            }

            _subFlocks.Add(subFlock);

            // Sub-flock leader
            Boid subFlockLeader = subFlock[0];

            if (sf == 0)
            {
                // First sub-flock leader IS the flock leader
                subFlockLeader.FormationIndex = 0;
                subFlockLeader.FormationLeader = null;
                subFlockLeader.IsParentFormationTier = false;
                subFlockLeader.OnFormationChanged();
            }
            else
            {
                // Other sub-flock leaders follow flock leader in parent formation
                subFlockLeader.FormationIndex = sf;
                subFlockLeader.FormationLeader = _formationLeader;
                subFlockLeader.IsParentFormationTier = true;
                subFlockLeader.OnFormationChanged();
            }

            // Sub-flock followers follow their sub-flock leader
            for (int j = 1; j < subFlock.Count; j++)
            {
                subFlock[j].FormationIndex = j;
                subFlock[j].FormationLeader = subFlockLeader;
                subFlock[j].IsParentFormationTier = false;
                subFlock[j].OnFormationChanged();
            }
        }
    }

    void Update()
    {
        if (boids == null)
            return;

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
            return;

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

            // Evaluate morale on same interval as combat sync
            if (settings.useAdaptiveMorale && anyInCombat)
            {
                EvaluateFlockMorale();
            }
            else if (settings.useAdaptiveMorale && _currentMorale != CombatMorale.Confident)
            {
                // Reset morale when out of combat
                _currentMorale = CombatMorale.Confident;
                CurrentMoraleScore = 1f;
                SetMoraleOnAllBoids(_currentMorale);
            }
        }

        EnsureComputeResources(numBoids);

        int boidCountSnapshot = boids.Count;
        int copyCount = Mathf.Min(numBoids, boidCountSnapshot);
        for (int i = 0; i < copyCount; i++)
        {
            Boid boid = boids[i];
            _boidData[i].flockHeading = Vector3.zero;
            _boidData[i].flockCenter = Vector3.zero;
            _boidData[i].seperationHeading = Vector3.zero;
            _boidData[i].numFlockmates = 0;

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
                AsyncGPUReadback.Request(_boidBuffer, request =>
                {
                    _readbackPending = false;
                    if (request.hasError) return;
                    
                    var gpuData = request.GetData<BoidData>();
                    
                    // Only copy up to the minimum of GPU buffer size and current _boidData size
                    int copyCount = Mathf.Min(_readbackBoidCount, _boidData.Length);
                    if (copyCount > 0)
                    {
                        System.Array.Copy(gpuData.ToArray(), 0, _boidData, 0, copyCount);
                    }
                });
            }
        }
        else
        {
            _boidBuffer.GetData(_boidData, 0, 0, numBoids);
        }

        for (int i = 0; i < numBoids; i++)
        {
            if (i >= boids.Count || boids[i] == null) continue;

            if (boids[i].HeightRange != HeightRange)
            {
                boids[i].SetHeightRange(HeightRange);
            }

            boids[i].avgFlockHeading = _boidData[i].flockHeading;
            boids[i].avgAvoidanceHeading = _boidData[i].seperationHeading;
            boids[i].flockmatesCenter = _boidData[i].flockCenter;
            boids[i].numPerceivedFlockmates = _boidData[i].numFlockmates;

            boids[i].UpdateBoid();
        }

        bool formationSettingChanged = settings.formationType != _lastFormationType
            || settings.useFormation != _lastUseFormation
            || settings.useSubFlocks != _lastUseSubFlocks
            || settings.subFlockFormationType != _lastSubFlockFormationType
            || settings.preferredSubFlockSize != _lastPreferredSubFlockSize
            || !Mathf.Approximately(settings.subFlockFormationSpacing, _lastSubFlockFormationSpacing);

        if (formationSettingChanged)
        {
            // Detect changes that require full sub-flock reassignment
            bool needsReassign = settings.useSubFlocks != _lastUseSubFlocks
                || settings.formationType != _lastFormationType
                || settings.preferredSubFlockSize != _lastPreferredSubFlockSize;

            _lastFormationType = settings.formationType;
            _lastUseFormation = settings.useFormation;
            _lastUseSubFlocks = settings.useSubFlocks;
            _lastSubFlockFormationType = settings.subFlockFormationType;
            _lastPreferredSubFlockSize = settings.preferredSubFlockSize;
            _lastSubFlockFormationSpacing = settings.subFlockFormationSpacing;

            if (needsReassign)
            {
                MarkFormationDirty();
            }

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
                if (i < _boidVehicles.Count)
                    _boidVehicles.RemoveAt(i);
                removedCount++;
            }
        }

        if (removedCount > 0 && (leaderRemoved || _formationDirty))
        {
            MarkFormationDirty();
        }

        return removedCount;
    }

    private void EvaluateFlockMorale()
    {
        int totalHP = 0;
        int totalMaxHP = 0;
        int aliveCount = 0;

        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] == null) continue;
            aliveCount++;

            VehicleBase vehicle = i < _boidVehicles.Count ? _boidVehicles[i] : null;
            if (vehicle != null)
            {
                totalHP += vehicle.HitPoints + vehicle.ArmorPoints + vehicle.ShieldPoints;
                totalMaxHP += vehicle.MaxHitPoints + vehicle.MaxArmorPoints + vehicle.MaxShieldPoints;
            }
        }

        float healthRatio = totalMaxHP > 0 ? (float)totalHP / totalMaxHP : 1f;
        int baseline = _initialCountSet ? _initialBoidCount : aliveCount;
        float strengthRatio = baseline > 0 ? (float)aliveCount / baseline : 1f;

        float score = healthRatio * settings.healthWeight + strengthRatio * settings.strengthWeight;
        CurrentMoraleScore = score;

        CombatMorale newMorale = _currentMorale;
        float hyst = settings.moraleHysteresis;

        // Determine new state with hysteresis for upward transitions
        if (score <= settings.brokenThreshold)
        {
            newMorale = CombatMorale.Broken;
        }
        else if (score > settings.confidentThreshold + (_currentMorale < CombatMorale.Confident ? hyst : 0f))
        {
            newMorale = CombatMorale.Confident;
        }
        else if (score > settings.brokenThreshold + (_currentMorale == CombatMorale.Broken ? hyst : 0f))
        {
            newMorale = CombatMorale.Cautious;
        }

        if (newMorale != _currentMorale)
        {
            _currentMorale = newMorale;
            SetMoraleOnAllBoids(newMorale);
        }
    }

    private void SetMoraleOnAllBoids(CombatMorale morale)
    {
        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] != null)
                boids[i].CurrentMorale = morale;
        }
    }

    private void UpdateBoidWeapons()
    {
        if (_boidWeapons.Count == 0) return;

        int updatesThisFrame = Mathf.Min(MaxBoidWeaponUpdatesPerFrame, _boidWeapons.Count);

        for (int i = 0; i < updatesThisFrame; i++)
        {
            if (_boidWeapons.Count == 0) break;

            _boidWeaponIndex = _boidWeaponIndex % _boidWeapons.Count;
            WeaponBase weapon = _boidWeapons[_boidWeaponIndex];

            if (weapon == null)
            {
                _boidWeapons.RemoveAt(_boidWeaponIndex);
                continue;
            }

            weapon.ManagedUpdateTarget();
            _boidWeaponIndex++;
        }
    }

    public void UpdateBoidList()
    {
        boids.Clear();
        _boidWeapons.Clear();
        _boidVehicles.Clear();

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
    public int SubFlockCount => _subFlocks?.Count ?? 0;
    public IReadOnlyList<List<Boid>> SubFlocks => _subFlocks;

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
        if (boids == null) return;

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
        if (boids == null) return;

        if (_subFlocks != null && _subFlocks.Count > 1)
        {
            // Sub-flock mode: color each sub-flock differently
            Color[] subFlockColors = {
                Color.cyan, Color.green, Color.magenta, Color.blue,
                new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f),
                new Color(0f, 1f, 0.5f), new Color(1f, 0f, 0.5f)
            };

            for (int sf = 0; sf < _subFlocks.Count; sf++)
            {
                Color sfColor = subFlockColors[sf % subFlockColors.Length];

                for (int j = 0; j < _subFlocks[sf].Count; j++)
                {
                    Boid boid = _subFlocks[sf][j];
                    if (boid == null) continue;

                    bool isSubFlockLeader = (j == 0);

                    Gizmos.color = isSubFlockLeader ? Color.yellow : sfColor;
                    float radius = isSubFlockLeader ? 12f : 8f;
                    Gizmos.DrawWireSphere(boid.position, radius);

                    // Draw line to sub-flock leader
                    if (!isSubFlockLeader && _subFlocks[sf][0] != null)
                    {
                        Gizmos.color = sfColor;
                        Gizmos.DrawLine(boid.position, _subFlocks[sf][0].position);
                    }

                    UnityEditor.Handles.Label(boid.position + Vector3.up * 25f, $"SF{sf}#{j}");
                }

                // Draw line from sub-flock leader to flock leader
                if (sf > 0 && _subFlocks[sf][0] != null && _formationLeader != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(_subFlocks[sf][0].position, _formationLeader.position);
                }
            }
        }
        else
        {
            for (int i = 0; i < boids.Count; i++)
            {
                if (boids[i] == null) continue;

                Gizmos.color = (i == 0) ? Color.yellow : Color.cyan;
                Gizmos.DrawWireSphere(boids[i].position, 8f);

                UnityEditor.Handles.Label(boids[i].position + Vector3.up * 25f, $"#{i}");
            }
        }
    }
#endif
}