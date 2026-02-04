using UnityEngine;
using UnityEngine.VFX;
using System.Collections.Generic;

/// <summary>
/// Object pool for VisualEffect instances to avoid constant instantiation/destruction.
/// </summary>
public class VFXPool : MonoBehaviour
{
    private static VFXPool _instance;
    public static VFXPool Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("VFXPool");
                _instance = go.AddComponent<VFXPool>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Dictionary: prefab instance ID -> pool of instances
    private Dictionary<int, Queue<VisualEffect>> _pools = new Dictionary<int, Queue<VisualEffect>>();
    // Track active VFX for automatic return
    private HashSet<VisualEffect> _activeVFX = new HashSet<VisualEffect>();

    [Header("Pool Settings")]
    [Tooltip("Maximum number of instances per prefab type")]
    public int maxPoolSize = 50;
    [Tooltip("How often to check active VFX for completion (seconds)")]
    public float checkInterval = 0.2f;

    private float _checkTimer = 0f;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= checkInterval)
        {
            _checkTimer = 0f;
            CheckActiveVFX();
        }
    }

    /// <summary>
    /// Get a VFX instance from the pool (or create new if needed)
    /// </summary>
    public VisualEffect Get(VisualEffect prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        int prefabID = prefab.GetInstanceID();
        
        // Get or create pool for this prefab
        if (!_pools.ContainsKey(prefabID))
        {
            _pools[prefabID] = new Queue<VisualEffect>();
        }

        VisualEffect vfx;
        
        // Try to get from pool
        while (_pools[prefabID].Count > 0)
        {
            vfx = _pools[prefabID].Dequeue();
            if (vfx != null)
            {
                vfx.transform.position = position;
                vfx.transform.rotation = rotation;
                vfx.gameObject.SetActive(true);
                vfx.Reinit();
                vfx.Play();
                _activeVFX.Add(vfx);
                return vfx;
            }
        }

        // Create new if pool is empty
        vfx = Instantiate(prefab, position, rotation, transform);
        vfx.Play();
        _activeVFX.Add(vfx);
        return vfx;
    }

    /// <summary>
    /// Return a VFX instance to the pool
    /// </summary>
    public void Return(VisualEffect vfx, VisualEffect prefab)
    {
        if (vfx == null || prefab == null) return;

        _activeVFX.Remove(vfx);

        int prefabID = prefab.GetInstanceID();
        
        if (!_pools.ContainsKey(prefabID))
        {
            _pools[prefabID] = new Queue<VisualEffect>();
        }

        // Check pool size limit
        if (_pools[prefabID].Count >= maxPoolSize)
        {
            Destroy(vfx.gameObject);
            return;
        }

        vfx.Stop();
        vfx.gameObject.SetActive(false);
        vfx.transform.SetParent(transform);
        _pools[prefabID].Enqueue(vfx);
    }

    /// <summary>
    /// Automatically check active VFX and return finished ones to pool
    /// </summary>
    private void CheckActiveVFX()
    {
        List<VisualEffect> toReturn = new List<VisualEffect>();

        foreach (var vfx in _activeVFX)
        {
            if (vfx == null || (!vfx.gameObject.activeInHierarchy && vfx.aliveParticleCount == 0))
            {
                toReturn.Add(vfx);
                continue;
            }

            // Check if finished playing (no alive particles and sufficient time passed)
            if (vfx.aliveParticleCount == 0)
            {
                VFXPooledInstance pooledInstance = vfx.GetComponent<VFXPooledInstance>();
                if (pooledInstance != null)
                {
                    if (Time.time - pooledInstance.spawnTime > 1f) // Wait 1 second after particles finish
                    {
                        toReturn.Add(vfx);
                    }
                }
            }
        }

        // Return finished VFX to pool
        foreach (var vfx in toReturn)
        {
            if (vfx != null)
            {
                VFXPooledInstance pooledInstance = vfx.GetComponent<VFXPooledInstance>();
                if (pooledInstance != null)
                {
                    Return(vfx, pooledInstance.prefab);
                }
            }
        }
    }

    /// <summary>
    /// Clear all pools
    /// </summary>
    public void ClearPools()
    {
        foreach (var pool in _pools.Values)
        {
            while (pool.Count > 0)
            {
                var vfx = pool.Dequeue();
                if (vfx != null)
                    Destroy(vfx.gameObject);
            }
        }
        _pools.Clear();
        _activeVFX.Clear();
    }
}

/// <summary>
/// Component added to pooled VFX instances to track metadata
/// </summary>
public class VFXPooledInstance : MonoBehaviour
{
    public VisualEffect prefab;
    public float spawnTime;

    public void Initialize(VisualEffect sourcePrefab)
    {
        prefab = sourcePrefab;
        spawnTime = Time.time;
    }
}
