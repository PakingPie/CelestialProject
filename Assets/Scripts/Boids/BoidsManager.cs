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

    [Tooltip("Leash center override. If not set, uses the flock target transform.")]
    public Transform LeashCenter;

    [Header("Formation")]
    public bool syncCombatState = true;

    [Header("Target Management")]
    [SerializeField] private BoidFlockTargetManager _targetManager;
    [Tooltip("Optional priority matrix for per-type target preference. Forwarded to the target manager.")]
    [SerializeField] private TargetPriorityMatrix _targetPriorityMatrix;

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
    private bool _debugMoraleLocked = false;
    public bool DebugMoraleLocked => _debugMoraleLocked;

    // Combat morale tracking — morale only decreases during combat via death penalties.
    // Full reassessment happens only when exiting combat.
    private float _combatEntryMoraleScore = 1f;
    private int _deathsDuringCombat = 0;

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
    private bool _formationDeferredUntilCombatEnd = false; // Defers reformation during combat

    // Moorage pool — boids removed from active list while docked/parked
    private List<Boid> _dockedPool = new List<Boid>();
    private List<VehicleBase> _dockedVehicles = new List<VehicleBase>();
    private Coroutine _launchCoroutine;
    private bool _isLaunching = false;

    // Events for external listeners
    public System.Action<Boid> OnBoidAdded;
    public System.Action<Boid> OnBoidRemoved;
    public System.Action OnFlockChanged;

    // Fleet coordination (opt-in)
    private FleetController _fleet;
    public FleetController Fleet
    {
        get => _fleet;
        set => _fleet = value;
    }

    void Start()
    {
        if (_targetManager == null)
        {
            _targetManager = gameObject.AddComponent<BoidFlockTargetManager>();
        }

        _targetManager.Initialize(_flockId, _team, _detectionRadius, _targetTags, _ignoreTags);
        if (_targetPriorityMatrix != null)
            _targetManager.SetTargetPriorityMatrix(_targetPriorityMatrix);
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
                    if (settings.startDocked && settings.moorageType == MoorageType.CarrierDocking)
                    {
                        // CarrierDocking: instant hide
                        var boid = boidObj.GetComponent<Boid>();
                        if (boid != null)
                        {
                            boid.SetHeightRange(HeightRange);
                            boid.Initialize(settings, target);
                            DockBoidImmediate(boid);
                        }
                    }
                    else
                    {
                        // StationParking + normal: register as Active
                        RegisterBoidInternal(boidObj);
                    }
                }
            }
        }

        if (!(settings.startDocked && settings.moorageType == MoorageType.CarrierDocking))
        {
            AssignFormationPositions();
        }

        // Ensure _initialBoidCount is set even if OnSpawnerComplete fired before subscription
        // (happens with Instant spawn mode where spawning occurs in Awake)
        if (!_initialCountSet)
        {
            int total = boids.Count + _dockedPool.Count;
            if (total > 0)
            {
                _initialBoidCount = total;
                _initialCountSet = true;
            }
        }

        // StationParking: if OnSpawnerComplete missed (Instant spawn), trigger parking now
        if (settings.startDocked && settings.moorageType == MoorageType.StationParking
            && boids.Count > 0 && _dockedPool.Count == 0)
        {
            ParkAllBoidsToSlots();
        }

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

        // CarrierDocking: instant hide into pool
        if (settings.startDocked && settings.moorageType == MoorageType.CarrierDocking)
        {
            boid.SetHeightRange(HeightRange);
            boid.Initialize(settings, target);
            DockBoidImmediate(boid);
            return;
        }

        // StationParking: register as Active (parking triggered in OnSpawnerComplete)

        RegisterBoidInternal(boid.gameObject);
        MarkFormationDirty();
    }

    private void OnSpawnerComplete()
    {
        if (settings.startDocked && settings.moorageType == MoorageType.CarrierDocking)
        {
            // Boids are in the docked pool, count them as initial
            if (!_initialCountSet && _dockedPool.Count > 0)
            {
                _initialBoidCount = _dockedPool.Count;
                _initialCountSet = true;
            }
            return;
        }

        AssignFormationPositions();

        if (!_initialCountSet && boids.Count > 0)
        {
            _initialBoidCount = boids.Count;
            _initialCountSet = true;
        }

        // StationParking: now that all boids are spawned and formation assigned,
        // send each boid to fly to its parked formation slot
        if (settings.startDocked && settings.moorageType == MoorageType.StationParking)
        {
            ParkAllBoidsToSlots();
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
        bool wasSubFlockLeader = boid.IsSubFlockLeader;

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

        // Clean from sub-flock list
        for (int sf = _subFlocks.Count - 1; sf >= 0; sf--)
        {
            _subFlocks[sf].Remove(boid);
            if (_subFlocks[sf].Count == 0)
                _subFlocks.RemoveAt(sf);
        }

        OnBoidRemoved?.Invoke(boid);

        // Track death for combat morale penalty
        if (_wasAnyInCombat && settings.useAdaptiveMorale)
            _deathsDuringCombat++;

        if (wasLeader || wasSubFlockLeader)
        {
            MarkFormationDirty(deferDuringCombat: true);
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
    /// If deferDuringCombat is true and flock is in combat, delays until combat ends.
    /// </summary>
    private void MarkFormationDirty(bool deferDuringCombat = false)
    {
        if (deferDuringCombat && _wasAnyInCombat)
        {
            _formationDeferredUntilCombatEnd = true;
            return;
        }

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

        // Sort boids by formation priority: Core → Escort → Screen,
        // heaviest ship first within each zone.
        SortBoidsByFormationPriority();

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

    /// <summary>
    /// Stable-sort boids so Core ships come first, then Escort, then Screen.
    /// Within each zone, heavier ship types sort first (Carrier > Battleship > ...).
    /// </summary>
    private void SortBoidsByFormationPriority()
    {
        // Stable sort: equal-priority boids keep their registration order.
        boids.Sort((a, b) =>
        {
            int pa = GlobalHelper.GetFormationSortPriority(a.ShipClass);
            int pb = GlobalHelper.GetFormationSortPriority(b.ShipClass);
            return pa.CompareTo(pb);
        });

        // Keep _boidVehicles in sync with the sorted boids list
        SyncVehicleListToSortedBoids();
    }

    private void SyncVehicleListToSortedBoids()
    {
        // Rebuild _boidVehicles to match new boid order
        var oldVehicles = new Dictionary<Boid, VehicleBase>(boids.Count);
        for (int i = 0; i < boids.Count && i < _boidVehicles.Count; i++)
        {
            if (boids[i] != null)
                oldVehicles[boids[i]] = _boidVehicles[i];
        }

        _boidVehicles.Clear();
        for (int i = 0; i < boids.Count; i++)
        {
            if (oldVehicles.TryGetValue(boids[i], out var v))
                _boidVehicles.Add(v);
            else
                _boidVehicles.Add(null);
        }
    }

    private void AssignFlatFormation()
    {
        _formationLeader.FormationIndex = 0;
        _formationLeader.FormationLeader = null;
        _formationLeader.IsParentFormationTier = false;
        _formationLeader.IsSubFlockLeader = false;
        _formationLeader.OnFormationChanged();

        for (int i = 1; i < boids.Count; i++)
        {
            boids[i].FormationIndex = i;
            boids[i].FormationLeader = _formationLeader;
            boids[i].IsParentFormationTier = false;
            boids[i].IsSubFlockLeader = false;
            boids[i].OnFormationChanged();
        }
    }

    private void AssignSubFlockFormations()
    {
        int totalBoids = boids.Count;

        // ── Group boids by FormationZone (type-homogeneous sub-flocks) ──
        // Boids are already sorted Core → Escort → Screen by SortBoidsByFormationPriority().
        // Split into contiguous runs by zone, then subdivide large zones into preferred-size chunks.
        List<List<Boid>> zoneGroups = new List<List<Boid>>(3);
        List<Boid> currentGroup = null;
        GlobalHelper.FormationZone currentZone = (GlobalHelper.FormationZone)(-1);

        for (int i = 0; i < totalBoids; i++)
        {
            var zone = boids[i].FormationZone;
            if (currentGroup == null || zone != currentZone)
            {
                currentGroup = new List<Boid>();
                zoneGroups.Add(currentGroup);
                currentZone = zone;
            }
            currentGroup.Add(boids[i]);
        }

        // Subdivide each zone group into preferred-size sub-flocks
        int preferred = Mathf.Clamp(settings.preferredSubFlockSize, settings.minSubFlockSize, settings.maxSubFlockSize);
        int minSize = Mathf.Max(2, settings.minSubFlockSize);
        int maxSize = settings.maxSubFlockSize;

        List<List<Boid>> finalSubFlocks = new List<List<Boid>>();
        for (int g = 0; g < zoneGroups.Count; g++)
        {
            SubdivideIntoSubFlocks(zoneGroups[g], preferred, minSize, maxSize, finalSubFlocks);
        }

        // If subdivision produced no groups (shouldn't happen), fall back to flat
        if (finalSubFlocks.Count == 0)
        {
            AssignFlatFormation();
            return;
        }

        // Assign formation roles within each sub-flock
        for (int sf = 0; sf < finalSubFlocks.Count; sf++)
        {
            _subFlocks.Add(finalSubFlocks[sf]);
            var subFlock = finalSubFlocks[sf];
            Boid subFlockLeader = subFlock[0]; // heaviest ship in this sub-flock (already sorted)

            if (sf == 0)
            {
                // First sub-flock leader IS the flock leader
                subFlockLeader.FormationIndex = 0;
                subFlockLeader.FormationLeader = null;
                subFlockLeader.IsParentFormationTier = false;
                subFlockLeader.IsSubFlockLeader = true;
                subFlockLeader.OnFormationChanged();
            }
            else
            {
                // Other sub-flock leaders follow flock leader in parent formation
                subFlockLeader.FormationIndex = sf;
                subFlockLeader.FormationLeader = _formationLeader;
                subFlockLeader.IsParentFormationTier = true;
                subFlockLeader.IsSubFlockLeader = true;
                subFlockLeader.OnFormationChanged();
            }

            // Sub-flock followers follow their sub-flock leader
            for (int j = 1; j < subFlock.Count; j++)
            {
                subFlock[j].FormationIndex = j;
                subFlock[j].FormationLeader = subFlockLeader;
                subFlock[j].IsParentFormationTier = false;
                subFlock[j].IsSubFlockLeader = false;
                subFlock[j].OnFormationChanged();
            }
        }
    }

    /// <summary>
    /// Subdivide a zone group into chunks of preferred size, respecting min/max constraints.
    /// </summary>
    private void SubdivideIntoSubFlocks(List<Boid> group, int preferred, int minSize, int maxSize, List<List<Boid>> output)
    {
        int remaining = group.Count;
        int offset = 0;

        while (remaining > 0)
        {
            int chunkSize;
            if (remaining <= preferred)
            {
                // Last chunk: if too small and we have previous sub-flocks, merge
                if (output.Count > 0 && remaining < minSize)
                {
                    var lastSubFlock = output[output.Count - 1];
                    for (int i = 0; i < remaining; i++)
                        lastSubFlock.Add(group[offset + i]);
                    break;
                }
                chunkSize = remaining;
            }
            else if (remaining - preferred < minSize && remaining - preferred > 0)
            {
                // Splitting would leave a too-small leftover — split evenly
                int half1 = remaining / 2;
                int half2 = remaining - half1;

                var sf1 = new List<Boid>(half1);
                for (int i = 0; i < half1; i++) sf1.Add(group[offset + i]);
                output.Add(sf1);

                var sf2 = new List<Boid>(half2);
                for (int i = 0; i < half2; i++) sf2.Add(group[offset + half1 + i]);
                output.Add(sf2);
                break;
            }
            else
            {
                chunkSize = Mathf.Min(preferred, maxSize);
            }

            chunkSize = Mathf.Min(chunkSize, maxSize);
            var subFlock = new List<Boid>(chunkSize);
            for (int i = 0; i < chunkSize; i++)
                subFlock.Add(group[offset + i]);
            output.Add(subFlock);

            offset += chunkSize;
            remaining -= chunkSize;
        }
    }

    void Update()
    {
        if (boids == null)
            return;

        // Handle deferred formation reassignment
        if (_formationDirty && !_formationDeferredUntilCombatEnd)
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

        // Auto-scramble: launch docked defence flock when enemies detected
        if (CheckAutoScramble())
        {
            LaunchAllBoids();
        }

        // Update parked boids' drift (they're not in the active boids list)
        if (settings.moorageType == MoorageType.StationParking && _dockedPool.Count > 0)
        {
            for (int i = 0; i < _dockedPool.Count; i++)
            {
                if (_dockedPool[i] != null && _dockedPool[i].IsParked)
                    _dockedPool[i].UpdateBoid();
            }
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

            // Broken flock stays "in combat" until enemies leave detection range
            if (_currentMorale == CombatMorale.Broken && _targetManager.HasDetectedEnemies())
                anyInCombat = true;

            if (syncCombatState && anyInCombat && !_wasAnyInCombat)
            {
                // Entering combat — snapshot morale and reset death counter
                if (settings.useAdaptiveMorale && !_debugMoraleLocked)
                {
                    EvaluateFlockMorale(); // get accurate baseline
                    _combatEntryMoraleScore = CurrentMoraleScore;
                    _deathsDuringCombat = 0;
                }

                foreach (var boid in boids)
                {
                    if (boid != null)
                        boid.EnterCombat();
                }
            }

            if (_wasAnyInCombat && !anyInCombat)
            {
                // Exiting combat — full reassessment from current surviving state
                if (settings.useAdaptiveMorale && !_debugMoraleLocked)
                {
                    _deathsDuringCombat = 0;
                    EvaluateFlockMorale();
                }

                // Flush any deferred formation changes from mid-combat leader deaths
                _formationDeferredUntilCombatEnd = false;
                AssignFormationPositions();

                // Auto-redock defence flocks after combat ends
                if (CheckAutoRedock())
                {
                    DockAllBoids();
                }
            }
            _wasAnyInCombat = anyInCombat;

            // During combat: apply death penalties only (no health recalc)
            if (settings.useAdaptiveMorale && anyInCombat && !_debugMoraleLocked)
            {
                ApplyCombatMoraleDeathPenalty();
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
                    int copyCount = Mathf.Min(_readbackBoidCount, Mathf.Min(gpuData.Length, _boidData.Length));
                    if (copyCount > 0)
                    {
                        Unity.Collections.NativeArray<BoidData>.Copy(gpuData, 0, _boidData, 0, copyCount);
                    }
                });
            }
        }
        else
        {
            _boidBuffer.GetData(_boidData, 0, 0, numBoids);
        }

        // Compute sub-flock centers (cheap: one pass over sub-flock lists)
        if (settings.useSubFlocks && _subFlocks.Count > 0)
        {
            for (int sf = 0; sf < _subFlocks.Count; sf++)
            {
                var subFlock = _subFlocks[sf];
                Vector3 center = Vector3.zero;
                int valid = 0;

                for (int j = 0; j < subFlock.Count; j++)
                {
                    if (subFlock[j] != null)
                    {
                        center += subFlock[j].position;
                        valid++;
                    }
                }

                if (valid > 0)
                    center /= valid;

                for (int j = 0; j < subFlock.Count; j++)
                {
                    if (subFlock[j] != null)
                        subFlock[j].SubFlockCenter = center;
                }
            }
        }

        // Compute leash center once per frame
        Vector3 leashCenter = Vector3.zero;
        bool leashActive = settings.useLeash;
        if (leashActive)
        {
            Transform lc = LeashCenter != null ? LeashCenter : target;
            leashCenter = lc != null ? lc.position : transform.position;
        }

        for (int i = 0; i < numBoids; i++)
        {
            if (i >= boids.Count || boids[i] == null) continue;

            if (boids[i].HeightRange != HeightRange)
            {
                boids[i].SetHeightRange(HeightRange);
            }

            boids[i].UseLeash = leashActive;
            if (leashActive)
                boids[i].LeashCenter = leashCenter;

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
        bool subFlockLeaderRemoved = false;
        int removedCount = 0;

        // Check if any sub-flock leaders were destroyed before removing nulls
        if (settings.useSubFlocks && _subFlocks.Count > 0)
        {
            for (int sf = 0; sf < _subFlocks.Count; sf++)
            {
                if (_subFlocks[sf].Count > 0 && _subFlocks[sf][0] == null)
                {
                    subFlockLeaderRemoved = true;
                    break;
                }
            }
        }

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

        // Also clean nulls from sub-flock lists
        if (removedCount > 0 && _subFlocks.Count > 0)
        {
            for (int sf = _subFlocks.Count - 1; sf >= 0; sf--)
            {
                _subFlocks[sf].RemoveAll(b => b == null);
                if (_subFlocks[sf].Count == 0)
                    _subFlocks.RemoveAt(sf);
            }
        }

        // Track deaths for combat morale penalty
        if (removedCount > 0 && _wasAnyInCombat && settings.useAdaptiveMorale)
            _deathsDuringCombat += removedCount;

        if (removedCount > 0 && (leaderRemoved || subFlockLeaderRemoved || _formationDirty))
        {
            MarkFormationDirty(deferDuringCombat: true);
        }

        return removedCount;
    }

    /// <summary>
    /// Full morale reassessment from current flock state.
    /// Called on combat entry (baseline snapshot) and combat exit (reassessment).
    /// </summary>
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

        // If all boids are dead, score is 0
        float healthRatio = (aliveCount == 0) ? 0f : (totalMaxHP > 0 ? (float)totalHP / totalMaxHP : 1f);
        int baseline = _initialCountSet ? _initialBoidCount : aliveCount;
        float strengthRatio = baseline > 0 ? (float)aliveCount / baseline : 0f;

        float score = healthRatio * settings.healthWeight + strengthRatio * settings.strengthWeight;
        CurrentMoraleScore = score;

        ApplyMoraleThresholds(score);
    }

    /// <summary>
    /// During combat: morale only decreases via death penalties.
    /// Each death subtracts a proportional penalty from the combat-entry snapshot.
    /// No health recalculation — killing a damaged boid never improves morale.
    /// </summary>
    private void ApplyCombatMoraleDeathPenalty()
    {
        int baseline = _initialCountSet ? _initialBoidCount : Mathf.Max(boids.Count + _deathsDuringCombat, 1);
        float deathPenalty = (float)_deathsDuringCombat / baseline;
        float score = Mathf.Clamp01(_combatEntryMoraleScore - deathPenalty);
        CurrentMoraleScore = score;

        // During combat, morale can only decrease — never transition upward
        CombatMorale newMorale = _currentMorale;

        if (score <= settings.brokenThreshold)
            newMorale = CombatMorale.Broken;
        else if (score <= settings.confidentThreshold && _currentMorale == CombatMorale.Confident)
            newMorale = CombatMorale.Cautious;

        if (newMorale > _currentMorale) // higher enum = worse morale
        {
            _currentMorale = newMorale;
            SetMoraleOnAllBoids(newMorale);

            if (newMorale == CombatMorale.Broken)
                _targetManager.ClearAllAssignments();
        }
    }

    /// <summary>
    /// Apply morale state thresholds with hysteresis. Used by full reassessment.
    /// </summary>
    private void ApplyMoraleThresholds(float score)
    {
        CombatMorale newMorale = _currentMorale;
        float hyst = settings.moraleHysteresis;

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

        // Recovery from Broken caps at Cautious per reassessment
        if (_currentMorale == CombatMorale.Broken && newMorale == CombatMorale.Confident)
            newMorale = CombatMorale.Cautious;

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

        _targetManager.SuppressAssignments = (morale == CombatMorale.Broken);
    }

    /// <summary>
    /// Force all boids into escape (Broken morale + combat). Editor debug only.
    /// </summary>
    public void DebugForceEscape()
    {
        if (boids == null || boids.Count == 0) return;

        _debugMoraleLocked = true;
        _currentMorale = CombatMorale.Broken;
        CurrentMoraleScore = 0f;
        SetMoraleOnAllBoids(CombatMorale.Broken);

        foreach (var boid in boids)
        {
            if (boid != null)
                boid.EnterCombat();
        }
        _wasAnyInCombat = true;

        Debug.Log($"[{_flockId}] Forced {boids.Count} boids into escape (Broken morale + combat)");
    }

    /// <summary>
    /// Restore all boids to Confident morale. Editor debug only.
    /// </summary>
    public void DebugRestoreConfident()
    {
        if (boids == null || boids.Count == 0) return;

        _debugMoraleLocked = false;
        _currentMorale = CombatMorale.Confident;
        CurrentMoraleScore = 1f;
        SetMoraleOnAllBoids(CombatMorale.Confident);
        Debug.Log($"[{_flockId}] Restored {boids.Count} boids to Confident morale");
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
    public int DockedCount => _dockedPool?.Count ?? 0;
    public IReadOnlyList<Boid> DockedBoids => _dockedPool;
    public bool IsLaunching => _isLaunching;

    public Vector3 FlockCenter
    {
        get
        {
            if (boids == null || boids.Count == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < boids.Count; i++)
            {
                if (boids[i] != null)
                {
                    sum += boids[i].transform.position;
                    count++;
                }
            }
            return count > 0 ? sum / count : transform.position;
        }
    }

    #region Moorage

    /// <summary>
    /// Get the current dock point position (manager position).
    /// </summary>
    public Vector3 GetDockPoint()
    {
        return transform.position;
    }

    /// <summary>
    /// Dock all active boids. Boids steer to dock point, then are pooled.
    /// For carrier docking: deactivated. For station parking: idle visible.
    /// </summary>
    public void DockAllBoids()
    {
        if (settings.moorageType == MoorageType.None) return;

        // Stop any active launch
        if (_launchCoroutine != null)
        {
            StopCoroutine(_launchCoroutine);
            _launchCoroutine = null;
            _isLaunching = false;
        }

        Vector3 dockPoint = GetDockPoint();
        bool isCarrier = settings.moorageType == MoorageType.CarrierDocking;

        for (int i = boids.Count - 1; i >= 0; i--)
        {
            Boid boid = boids[i];
            if (boid == null || boid.IsMoored || boid.IsTransitioning) continue;

            if (isCarrier)
            {
                boid.BeginMoorage(BoidMode.Docking, dockPoint, () => OnBoidMoorageArrived(boid, true));
            }
            else
            {
                boid.BeginMoorage(BoidMode.Parking, dockPoint, () => OnBoidMoorageArrived(boid, false));
            }
        }
    }

    private void OnBoidMoorageArrived(Boid boid, bool deactivate, Quaternion? parkedRotation = null)
    {
        if (boid == null) return;

        // Remove from active list
        _targetManager.UnregisterBoid(boid);
        int index = boids.IndexOf(boid);
        boids.Remove(boid);
        if (index >= 0 && index < _boidVehicles.Count)
        {
            _dockedVehicles.Add(_boidVehicles[index]);
            _boidVehicles.RemoveAt(index);
        }
        else
        {
            _dockedVehicles.Add(boid.GetComponent<VehicleBase>());
        }

        // Remove weapons from active tracking
        var weapons = boid.GetComponentsInChildren<WeaponBase>();
        foreach (var weapon in weapons)
            _boidWeapons.Remove(weapon);

        // Clean from sub-flock list
        for (int sf = _subFlocks.Count - 1; sf >= 0; sf--)
        {
            _subFlocks[sf].Remove(boid);
            if (_subFlocks[sf].Count == 0)
                _subFlocks.RemoveAt(sf);
        }

        if (deactivate)
        {
            boid.SetDocked();
            boid.gameObject.SetActive(false);
        }
        else
        {
            Quaternion targetRot = parkedRotation ?? boid.transform.rotation;
            boid.SetParked(settings.parkedDriftSpeed, targetRot);
        }

        _dockedPool.Add(boid);

        MarkFormationDirty();
        OnFlockChanged?.Invoke();
    }

    /// <summary>
    /// Launch all docked/parked boids. Instant for defence flocks, staggered for carrier.
    /// </summary>
    public void LaunchAllBoids()
    {
        if (_dockedPool.Count == 0) return;

        if (_launchCoroutine != null)
        {
            StopCoroutine(_launchCoroutine);
            _launchCoroutine = null;
        }

        // Defence flocks (startDocked) launch all at once
        if (settings.startDocked)
        {
            LaunchAllBoidsImmediate();
        }
        else
        {
            _launchCoroutine = StartCoroutine(LaunchBoidsStaggered());
        }
    }

    /// <summary>
    /// Activate all pooled boids simultaneously. Used for defence flock scramble.
    /// </summary>
    private void LaunchAllBoidsImmediate()
    {
        _isLaunching = true;

        while (_dockedPool.Count > 0)
        {
            Boid boid = _dockedPool[0];
            _dockedPool.RemoveAt(0);

            VehicleBase vehicle = _dockedVehicles.Count > 0 ? _dockedVehicles[0] : null;
            if (_dockedVehicles.Count > 0)
                _dockedVehicles.RemoveAt(0);

            if (boid == null) continue;

            ActivateBoidFromPool(boid, vehicle);
        }

        FinalizeLaunch();
    }

    private System.Collections.IEnumerator LaunchBoidsStaggered()
    {
        _isLaunching = true;
        float interval = settings.launchInterval;

        while (_dockedPool.Count > 0)
        {
            Boid boid = _dockedPool[0];
            _dockedPool.RemoveAt(0);

            VehicleBase vehicle = _dockedVehicles.Count > 0 ? _dockedVehicles[0] : null;
            if (_dockedVehicles.Count > 0)
                _dockedVehicles.RemoveAt(0);

            if (boid == null) continue;

            ActivateBoidFromPool(boid, vehicle);

            yield return new WaitForSeconds(interval);
        }

        FinalizeLaunch();
        _launchCoroutine = null;
    }

    private void ActivateBoidFromPool(Boid boid, VehicleBase vehicle)
    {
        // Reactivate
        boid.gameObject.SetActive(true);

        // Re-register into active list
        boids.Add(boid);
        _boidVehicles.Add(vehicle);

        // Initialize at current position (formation slot)
        Vector3 savedPos = boid.transform.position;
        Quaternion savedRot = boid.transform.rotation;
        boid.Initialize(settings, target);
        boid.transform.position = savedPos;
        boid.transform.rotation = savedRot;
        boid.position = savedPos;
        boid.forward = savedRot * Vector3.forward;

        boid.SetTargetManager(_targetManager);
        boid.SetHeightRange(HeightRange);
        _targetManager.RegisterBoid(boid);

        // Re-register weapons
        var weapons = boid.GetComponentsInChildren<WeaponBase>(); 
        foreach (var weapon in weapons)
        {
            if (!_boidWeapons.Contains(weapon))
            {
                weapon.UseManagedUpdates = false;
                CombatManager.Instance?.UnregisterTurret(weapon);
                _boidWeapons.Add(weapon);
            }
        }

        // Go directly to Active — boids are already at formation positions
        boid.Launch(boid.forward * settings.minSpeed);

        OnBoidAdded?.Invoke(boid);
    }

    private void FinalizeLaunch()
    {
        AssignFormationPositions();
        OnFlockChanged?.Invoke();

        if (!_initialCountSet && boids.Count > 0)
        {
            _initialBoidCount = boids.Count;
            _initialCountSet = true;
        }

        _isLaunching = false;
    }

    /// <summary>
    /// Immediately dock a boid into the pool (used for startDocked carrier spawns).
    /// </summary>
    private void DockBoidImmediate(Boid boid)
    {
        if (boid == null) return;

        var vehicle = boid.GetComponent<VehicleBase>();

        boid.SetDocked();
        boid.gameObject.SetActive(false);

        _dockedPool.Add(boid);
        _dockedVehicles.Add(vehicle);
    }

    /// <summary>
    /// Send a boid to fly to a parked formation slot, then enter Parked state on arrival.
    /// </summary>
    private void ParkBoidToSlot(Boid boid, int slotIndex)
    {
        Vector3 dockPoint = GetDockPoint();
        // Use sub-flock spacing when sub-flocks are enabled, otherwise a fraction of main spacing
        float parkedSpacing = settings.useSubFlocks
            ? settings.subFlockFormationSpacing
            : settings.formationSpacing * 0.15f;
        FormationType parkFormation = settings.useSubFlocks
            ? settings.subFlockFormationType
            : settings.formationType;
        // Offset by 1 so no boid gets index 0 (which returns Vector3.zero)
        Vector3 formationOffset = Boid.CalculateFormationOffset(slotIndex + 1, parkFormation, parkedSpacing);
        Vector3 worldOffset = transform.rotation * formationOffset;
        Vector3 targetPos = dockPoint + worldOffset;

        Quaternion targetRot = transform.rotation;

        boid.BeginMoorage(BoidMode.Parking, targetPos, () =>
        {
            // Snap position on arrival, rotation handled gradually in parked state
            boid.transform.position = targetPos;
            boid.position = targetPos;
            OnBoidMoorageArrived(boid, false, targetRot);
        });
    }

    /// <summary>
    /// Send all active boids to their parked formation slots.
    /// </summary>
    private void ParkAllBoidsToSlots()
    {
        for (int i = 0; i < boids.Count; i++)
        {
            ParkBoidToSlot(boids[i], i);
        }
    }

    /// <summary>
    /// Check if the flock has enemies in detection range and should auto-scramble.
    /// Called from Update when flock is fully docked and startDocked is true.
    /// </summary>
    private bool CheckAutoScramble()
    {
        if (!settings.startDocked) return false;
        if (settings.moorageType == MoorageType.None) return false;
        if (_isLaunching) return false;
        if (_dockedPool.Count == 0) return false;

        return _targetManager != null && _targetManager.HasDetectedEnemies();
    }

    /// <summary>
    /// Check if all active boids should auto-redock after combat ends.
    /// </summary>
    private bool CheckAutoRedock()
    {
        if (!settings.startDocked) return false;
        if (settings.moorageType == MoorageType.None) return false;
        if (_isLaunching) return false;
        if (boids.Count == 0) return false;
        if (_wasAnyInCombat) return false; // Still in combat

        // All boids are active and combat has ended
        return true;
    }

    #endregion

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