using UnityEngine;
using System.Collections.Generic;

public class BoidsManager : MonoBehaviour
{
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader computeShader;
    List<Boid> boids;

    public Transform target;
    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);

    [Header("Formation")]
    public bool syncCombatState = true;

    [Header("Target Management")]
    [SerializeField] private FlockTargetManager _targetManager;

    [Header("Flock Identity")]
    [SerializeField] private string _flockId = "Flock_01";
    [SerializeField] private GlobalHelper.Team _team = GlobalHelper.Team.Player;
    [SerializeField] private List<string> _targetTags = new List<string>();
    [SerializeField] private List<string> _ignoreTags = new List<string>();
    [SerializeField] private LayerMask _targetLayers;
    [SerializeField] private float _detectionRadius = 5000f;
    [Header("Detection")]
    [SerializeField] private int _maxOverlapResults = 256;

    private List<WeaponBase> _boidWeapons = new List<WeaponBase>();
    private Boid _formationLeader = null;

    void Start()
    {
        // Create target manager if not assigned
        if (_targetManager == null)
        {
            _targetManager = gameObject.AddComponent<FlockTargetManager>();
        }

        _targetManager.Initialize(_flockId, _team, _detectionRadius, _targetLayers, _maxOverlapResults, _targetTags, _ignoreTags);

        var spawners = GetComponentsInChildren<BoidSpawner>();
        boids = new List<Boid>();
        _boidWeapons = new List<WeaponBase>();

        foreach (BoidSpawner spawner in spawners)
        {
            var spawnedBoids = spawner.SpawnedObjects;
            foreach (GameObject boidObj in spawnedBoids)
            {
                var boid = boidObj.GetComponent<Boid>();
                if (boid != null)
                {
                    boids.Add(boid);
                    boid.Initialize(settings, target);
                    boid.SetTargetManager(_targetManager);
                    boid.transform.gameObject.GetComponent<VehicleBase>().BoidManager = this;

                    _targetManager.RegisterBoid(boid);
                }

                var weapons = boidObj.GetComponentsInChildren<WeaponBase>();
                foreach (var weapon in weapons)
                {
                    weapon.UseManagedUpdates = false;
                    _boidWeapons.Add(weapon);
                }
            }
        }

        AssignFormationPositions();
    }

    void AssignFormationPositions()
    {
        if (boids == null || boids.Count == 0) return;

        _formationLeader = boids[0];
        _formationLeader.FormationIndex = 0;
        _formationLeader.FormationLeader = null;

        for (int i = 1; i < boids.Count; i++)
        {
            boids[i].FormationIndex = i;
            boids[i].FormationLeader = _formationLeader;
        }
    }

    void Update()
    {
        if (boids == null)
            return;

        CleanupDestroyedBoids();

        int numBoids = boids.Count;
        if (numBoids <= 0)
            return;

        if (syncCombatState)
        {
            SyncCombatState();
        }

        var boidData = new BoidData[numBoids];
        for (int i = 0; i < numBoids; i++)
        {
            boidData[i].position = boids[i].position;
            boidData[i].direction = boids[i].forward;
        }

        var boidBuffer = new ComputeBuffer(numBoids, BoidData.Size);
        boidBuffer.SetData(boidData);

        computeShader.SetBuffer(0, "boids", boidBuffer);
        computeShader.SetInt("numBoids", numBoids);
        computeShader.SetFloat("viewRadius", settings.perceptionRadius);
        computeShader.SetFloat("avoidRadius", settings.avoidanceRadius);
        computeShader.SetVector("heightRange", HeightRange);
        int threadGroups = Mathf.CeilToInt(numBoids / (float)threadGroupSize);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        boidBuffer.GetData(boidData);

        for (int i = 0; i < numBoids; i++)
        {
            boids[i].avgFlockHeading = boidData[i].flockHeading;
            boids[i].avgAvoidanceHeading = boidData[i].seperationHeading;
            boids[i].flockmatesCenter = boidData[i].flockCenter;
            boids[i].numPerceivedFlockmates = boidData[i].numFlockmates;

            boids[i].UpdateBoid();
        }

        UpdateBoidWeapons();

        boidBuffer.Release();
    }

    private void CleanupDestroyedBoids()
    {
        bool leaderRemoved = false;

        for (int i = boids.Count - 1; i >= 0; i--)
        {
            if (boids[i] == null)
            {
                if (i == 0) leaderRemoved = true;
                _targetManager.UnregisterBoid(boids[i]);
                boids.RemoveAt(i);
            }
        }

        if (leaderRemoved)
        {
            AssignFormationPositions();
        }
    }

    private void SyncCombatState()
    {
        bool anyInCombat = false;

        for (int i = 0; i < boids.Count; i++)
        {
            if (boids[i] != null && boids[i].IsInCombat)
            {
                anyInCombat = true;
                break;
            }
        }

        if (anyInCombat)
        {
            for (int i = 0; i < boids.Count; i++)
            {
                if (boids[i] != null)
                    boids[i].EnterCombat();
            }
        }
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
        var spawners = GetComponentsInChildren<BoidSpawner>();
        boids = new List<Boid>();
        _boidWeapons = new List<WeaponBase>();

        foreach (BoidSpawner spawner in spawners)
        {
            var spawnedBoids = spawner.SpawnedObjects;
            foreach (GameObject boidObj in spawnedBoids)
            {
                var boid = boidObj.GetComponent<Boid>();
                if (boid != null)
                {
                    boids.Add(boid);
                    boid.Initialize(settings, target);
                    boid.SetTargetManager(_targetManager);
                    boid.transform.gameObject.GetComponent<VehicleBase>().BoidManager = this;

                    _targetManager.RegisterBoid(boid);
                }

                var weapons = boidObj.GetComponentsInChildren<WeaponBase>();
                foreach (var weapon in weapons)
                {
                    weapon.UseManagedUpdates = false;
                    _boidWeapons.Add(weapon);
                }
            }
        }

        AssignFormationPositions();
    }

    public void RemoveBoid(Boid boid)
    {
        bool wasLeader = (boid == _formationLeader);
        _targetManager.UnregisterBoid(boid);
        boids.Remove(boid);

        if (wasLeader)
        {
            AssignFormationPositions();
        }
    }

    public void SetFormationType(FormationType type)
    {
        settings.formationType = type;
    }

    public void ForceCombatMode()
    {
        foreach (var boid in boids)
        {
            if (boid != null)
                boid.EnterCombat();
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

    public FlockTargetManager TargetManager => _targetManager;

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
}