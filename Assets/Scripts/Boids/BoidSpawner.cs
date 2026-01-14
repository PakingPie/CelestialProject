// BoidSpawner.cs - UPDATED with dynamic spawning support
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidSpawner : MonoBehaviour
{
    public enum GizmoType
    {
        Never, SelectedOnly, Always
    }

    public enum SpawnMode
    {
        Instant,
        Sequential,
        Wave
    }

    [Header("Spawn Settings")]
    public Boid[] prefabs;
    public int spawnCount = 10;
    public Color color;
    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);

    [Header("Spawn Mode")]
    public SpawnMode spawnMode = SpawnMode.Instant;

    [Header("Instant Mode Settings")]
    public float spawnRadius = 10.0f;

    [Header("Sequential Mode Settings")]
    [Tooltip("Spawn points to use. If empty, uses this transform.")]
    public Transform[] spawnPoints;
    [Tooltip("Time between each spawn")]
    public float spawnInterval = 0.5f;
    [Tooltip("Random offset applied at spawn point")]
    public float spawnPointRandomness = 2f;
    [Tooltip("Initial velocity direction (local to spawn point)")]
    public Vector3 launchDirection = Vector3.forward;
    [Tooltip("Initial speed when launched")]
    public float launchSpeed = 50f;
    [Tooltip("If true, spawns immediately on Awake. If false, call StartSpawning() manually.")]
    public bool autoStart = true;

    [Header("Wave Mode Settings")]
    public int boidsPerWave = 5;
    public float timeBetweenWaves = 3f;

    [Header("Continuous Spawning")]
    [Tooltip("If true, maintains spawnCount by respawning when boids are destroyed")]
    public bool maintainPopulation = false;
    [Tooltip("Delay before respawning a destroyed boid")]
    public float respawnDelay = 2f;
    [Tooltip("Maximum total boids ever spawned (0 = unlimited)")]
    public int maxTotalSpawns = 0;

    [Header("Attack Behavior")]
    public BoidAttackProfile attackProfile;

    [Header("Debug")]
    public GizmoType showSpawnRegion;

    public List<GameObject> SpawnedObjects { get; private set; }

    // Events
    public System.Action<Boid> OnBoidSpawned;
    public System.Action<Boid> OnBoidDestroyed;
    public System.Action OnSpawningComplete;

    // State
    private int _currentSpawnIndex = 0;
    private int _currentSpawnPointIndex = 0;
    private Coroutine _spawnCoroutine;
    private bool _isSpawning = false;
    private int _totalSpawnedCount = 0;
    private int _pendingRespawns = 0;

    public bool IsSpawning => _isSpawning;
    public int RemainingToSpawn => spawnCount - _currentSpawnIndex;
    public int ActiveBoidCount => CountActiveBoids();
    public int TotalSpawnedCount => _totalSpawnedCount;
    // Add this field near other state fields
    [SerializeField] private bool _isPaused = false;
    public bool IsPaused => _isPaused;

    public void Pause()
    {
        _isPaused = true;
        StopSpawning(); // Stop any ongoing sequential/wave spawning
    }

    public void Resume()
    {
        _isPaused = false;
    }

    void Awake()
    {
        SpawnedObjects = new List<GameObject>();

        if (autoStart)
        {
            StartSpawning();
        }
    }

    void Update()
    {
        if (maintainPopulation && !_isSpawning)
        {
            CleanupDestroyedBoids();
        }
    }

    private void CleanupDestroyedBoids()
    {
        for (int i = SpawnedObjects.Count - 1; i >= 0; i--)
        {
            if (SpawnedObjects[i] == null)
            {
                SpawnedObjects.RemoveAt(i);

                // Queue respawn if maintaining population
                if (maintainPopulation && CanSpawnMore())
                {
                    _pendingRespawns++;
                    StartCoroutine(RespawnAfterDelay());
                }
            }
        }
    }

    private bool CanSpawnMore()
    {
        if (maxTotalSpawns > 0 && _totalSpawnedCount >= maxTotalSpawns)
            return false;
        return true;
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        _pendingRespawns--;

        if (CanSpawnMore() && ActiveBoidCount < spawnCount)
        {
            SpawnOne(GetNextSpawnPoint());
        }
    }

    private int CountActiveBoids()
    {
        int count = 0;
        foreach (var obj in SpawnedObjects)
        {
            if (obj != null) count++;
        }
        return count;
    }

    public void StartSpawning()
    {
        if (_isSpawning || _isPaused) return;

        switch (spawnMode)
        {
            case SpawnMode.Instant:
                SpawnAllInstant();
                break;

            case SpawnMode.Sequential:
                _spawnCoroutine = StartCoroutine(SpawnSequentialCoroutine());
                break;

            case SpawnMode.Wave:
                _spawnCoroutine = StartCoroutine(SpawnWaveCoroutine());
                break;
        }
    }

    public void StopSpawning()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
        _isSpawning = false;
    }

    /// <summary>
    /// Spawn additional boids beyond the initial spawnCount.
    /// </summary>
    public void SpawnAdditional(int count)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            if (!CanSpawnMore()) break;
            SpawnOne(GetNextSpawnPoint());
        }
    }

    /// <summary>
    /// Spawn additional boids sequentially with delay.
    /// </summary>
    public void SpawnAdditionalSequential(int count)
    {
        if (count <= 0) return;
        StartCoroutine(SpawnAdditionalSequentialCoroutine(count));
    }

    private IEnumerator SpawnAdditionalSequentialCoroutine(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (!CanSpawnMore()) break;

            SpawnOne(GetNextSpawnPoint());

            if (i < count - 1)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }

    /// <summary>
    /// Spawn a single boid immediately.
    /// </summary>
    public Boid SpawnOne()
    {
        return SpawnOne(GetNextSpawnPoint());
    }

    /// <summary>
    /// Spawn a single boid at a specific position.
    /// </summary>
    public Boid SpawnOne(Transform spawnPoint)
    {
        if (_isPaused) return null;
        if (prefabs == null || prefabs.Length == 0) return null;
        if (!CanSpawnMore()) return null;

        Vector3 position;
        Quaternion rotation;
        Vector3 initialVelocity;

        if (spawnMode == SpawnMode.Instant)
        {
            Vector3 randomSphere = Random.insideUnitSphere * spawnRadius;
            position = transform.position + new Vector3(
                randomSphere.x,
                Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y),
                randomSphere.z
            );

            randomSphere = Random.insideUnitSphere;
            Vector3 forward = new Vector3(
                randomSphere.x,
                Mathf.Clamp(randomSphere.y, HeightRange.x, HeightRange.y),
                randomSphere.z
            ).normalized;

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            rotation = Quaternion.LookRotation(forward);
            initialVelocity = Vector3.zero;
        }
        else
        {
            Vector3 randomOffset = Random.insideUnitSphere * spawnPointRandomness;
            position = spawnPoint.position + randomOffset;

            Vector3 worldLaunchDir = spawnPoint.TransformDirection(launchDirection.normalized);
            rotation = Quaternion.LookRotation(worldLaunchDir);
            initialVelocity = worldLaunchDir * launchSpeed;
        }

        Boid boid = Instantiate(prefabs[Random.Range(0, prefabs.Length)], position, rotation);
        SpawnedObjects.Add(boid.gameObject);
        _totalSpawnedCount++;

        boid.SetColor(color);
        // Store spawn position for despawn command
        boid.SetSpawnPosition(spawnPoint != null ? spawnPoint.position : transform.position);

        if (initialVelocity.sqrMagnitude > 0.01f)
        {
            boid.SetInitialVelocity(initialVelocity);
        }

        if (attackProfile != null)
        {
            var attackBehavior = boid.GetComponent<BoidAttackBehavior>();
            if (attackBehavior == null)
                attackBehavior = boid.gameObject.AddComponent<BoidAttackBehavior>();
            attackBehavior.SetProfile(attackProfile);
        }

        OnBoidSpawned?.Invoke(boid);

        return boid;
    }

    private void SpawnAllInstant()
    {
        _isSpawning = true;

        for (int i = 0; i < spawnCount; i++)
        {
            if (!CanSpawnMore()) break;
            SpawnOne();
            _currentSpawnIndex++;
        }

        _isSpawning = false;
        OnSpawningComplete?.Invoke();
    }

    private IEnumerator SpawnSequentialCoroutine()
    {
        _isSpawning = true;
        _currentSpawnIndex = 0;

        while (_currentSpawnIndex < spawnCount && CanSpawnMore())
        {
            Transform spawnPoint = GetNextSpawnPoint();
            SpawnOne(spawnPoint);
            _currentSpawnIndex++;

            if (_currentSpawnIndex < spawnCount)
            {
                yield return new WaitForSeconds(spawnInterval);
            }
        }

        _isSpawning = false;
        OnSpawningComplete?.Invoke();
    }

    private IEnumerator SpawnWaveCoroutine()
    {
        _isSpawning = true;
        _currentSpawnIndex = 0;

        while (_currentSpawnIndex < spawnCount && CanSpawnMore())
        {
            int waveSize = Mathf.Min(boidsPerWave, spawnCount - _currentSpawnIndex);

            for (int i = 0; i < waveSize; i++)
            {
                if (!CanSpawnMore()) break;

                Transform spawnPoint = GetNextSpawnPoint();
                SpawnOne(spawnPoint);
                _currentSpawnIndex++;

                if (i < waveSize - 1)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            if (_currentSpawnIndex < spawnCount)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        _isSpawning = false;
        OnSpawningComplete?.Invoke();
    }

    private Transform GetNextSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return transform;
        }

        Transform point = spawnPoints[_currentSpawnPointIndex];
        _currentSpawnPointIndex = (_currentSpawnPointIndex + 1) % spawnPoints.Length;

        return point;
    }

    /// <summary>
    /// Reset spawner to spawn the initial batch again.
    /// </summary>
    public void ResetSpawner()
    {
        StopSpawning();
        _currentSpawnIndex = 0;
        _currentSpawnPointIndex = 0;
        // Note: doesn't reset _totalSpawnedCount to respect maxTotalSpawns
    }

    /// <summary>
    /// Full reset including total spawn counter.
    /// </summary>
    public void FullReset()
    {
        ResetSpawner();
        _totalSpawnedCount = 0;
        _pendingRespawns = 0;
    }

    /// <summary>
    /// Clear all spawned boids and reset.
    /// </summary>
    public void ClearAndReset()
    {
        StopSpawning();
        StopAllCoroutines();

        foreach (var obj in SpawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        SpawnedObjects.Clear();

        _currentSpawnIndex = 0;
        _currentSpawnPointIndex = 0;
        _totalSpawnedCount = 0;
        _pendingRespawns = 0;
    }

    /// <summary>
    /// Change the target population and adjust accordingly.
    /// </summary>
    public void SetPopulation(int newCount)
    {
        int currentActive = ActiveBoidCount;
        spawnCount = newCount;

        if (currentActive < newCount)
        {
            // Need more boids
            SpawnAdditionalSequential(newCount - currentActive);
        }
        // Note: doesn't destroy excess boids - let them die naturally or call RemoveExcess()
    }

    /// <summary>
    /// Remove excess boids if over population limit.
    /// </summary>
    public void RemoveExcess()
    {
        int excess = ActiveBoidCount - spawnCount;

        for (int i = SpawnedObjects.Count - 1; i >= 0 && excess > 0; i--)
        {
            if (SpawnedObjects[i] != null)
            {
                Destroy(SpawnedObjects[i]);
                SpawnedObjects.RemoveAt(i);
                excess--;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (showSpawnRegion == GizmoType.Always)
        {
            DrawGizmos();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (showSpawnRegion == GizmoType.SelectedOnly)
        {
            DrawGizmos();
        }
    }

    void DrawGizmos()
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);

        if (spawnMode == SpawnMode.Instant)
        {
            Gizmos.DrawSphere(transform.position, spawnRadius);
        }
        else
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                foreach (var point in spawnPoints)
                {
                    if (point == null) continue;

                    Gizmos.DrawWireSphere(point.position, spawnPointRandomness);

                    Gizmos.color = Color.cyan;
                    Vector3 worldDir = point.TransformDirection(launchDirection.normalized);
                    Gizmos.DrawRay(point.position, worldDir * launchSpeed * 0.5f);

                    Gizmos.color = new Color(color.r, color.g, color.b, 0.5f);
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, spawnPointRandomness);

                Gizmos.color = Color.cyan;
                Vector3 worldDir = transform.TransformDirection(launchDirection.normalized);
                Gizmos.DrawRay(transform.position, worldDir * launchSpeed * 0.5f);
            }
        }
    }
}