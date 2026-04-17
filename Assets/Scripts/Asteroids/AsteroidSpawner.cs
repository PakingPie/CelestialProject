using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AsteroidSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private int asteroidCount = 10;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(100f, 100f, 100f);
    [SerializeField] private float minDistanceBetweenAsteroids = 10f;
    
    [Header("Asteroid Prefabs")]
    [Tooltip("Drag asteroid prefabs here. A random one is chosen per spawn.")]
    [SerializeField] private GameObject[] asteroidPrefabs;
    [SerializeField] private Vector2 scaleRange = new Vector2(2f, 8f);
    [SerializeField] private List<Material> asteroidMaterials;
    [SerializeField] private VisualEffect destructionFXPrefab;
    [SerializeField, Range(0.01f, 2f)] private float vfxScaleFactor = 1f;
    
    [Header("Health Settings")]
    [SerializeField] private Vector2Int healthRange = new Vector2Int(5, 20);
    
    [SerializeField, HideInInspector]
    private List<GameObject> spawnedAsteroids = new List<GameObject>();

    public void SpawnAsteroids()
    {
        ClearAsteroids();
        
        if (asteroidPrefabs == null || asteroidPrefabs.Length == 0)
        {
            Debug.LogError("AsteroidSpawner: No asteroid prefabs assigned!");
            return;
        }
        
        List<Vector3> positions = GenerateSpawnPositions();
        
        foreach (Vector3 position in positions)
        {
            CreateAsteroid(position);
        }
    }

    public void ClearAsteroids()
    {
        for (int i = spawnedAsteroids.Count - 1; i >= 0; i--)
        {
            if (spawnedAsteroids[i] != null)
            {
                DestroyImmediate(spawnedAsteroids[i]);
            }
        }
        spawnedAsteroids.Clear();
    }

    private List<Vector3> GenerateSpawnPositions()
    {
        List<Vector3> positions = new List<Vector3>();
        int maxAttempts = asteroidCount * 10;
        int attempts = 0;

        while (positions.Count < asteroidCount && attempts < maxAttempts)
        {
            Vector3 candidate = transform.position + new Vector3(
                Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f),
                Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f),
                Random.Range(-spawnAreaSize.z / 2f, spawnAreaSize.z / 2f)
            );

            if (IsValidPosition(candidate, positions))
            {
                positions.Add(candidate);
            }
            attempts++;
        }

        if (positions.Count < asteroidCount)
        {
            Debug.LogWarning($"Could only place {positions.Count} of {asteroidCount} asteroids due to spacing constraints.");
        }

        return positions;
    }

    private bool IsValidPosition(Vector3 candidate, List<Vector3> existingPositions)
    {
        foreach (Vector3 pos in existingPositions)
        {
            if (Vector3.Distance(candidate, pos) < minDistanceBetweenAsteroids)
            {
                return false;
            }
        }
        return true;
    }

    private void CreateAsteroid(Vector3 position)
    {
        GameObject prefab = asteroidPrefabs[Random.Range(0, asteroidPrefabs.Length)];
        GameObject asteroidRoot = Instantiate(prefab, position, Random.rotation, transform);
        asteroidRoot.name = $"Asteroid_{spawnedAsteroids.Count}";

        float scale = Random.Range(scaleRange.x, scaleRange.y);
        asteroidRoot.transform.localScale = Vector3.one * scale;

        if (asteroidMaterials != null && asteroidMaterials.Count > 0)
        {
            Material mat = asteroidMaterials[Random.Range(0, asteroidMaterials.Count)];
            Renderer[] renderers = asteroidRoot.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = mat;
                renderer.sharedMaterials = mats;
            }
        }

        // Add gameplay components if not already on the prefab
        if (asteroidRoot.GetComponent<ObstacleEntity>() == null)
            asteroidRoot.AddComponent<ObstacleEntity>();

        Asteroid asteroid = asteroidRoot.GetComponent<Asteroid>();
        if (asteroid == null)
            asteroid = asteroidRoot.AddComponent<Asteroid>();

        if (destructionFXPrefab != null)
            asteroid.SetDestructionFX(destructionFXPrefab);

        asteroid.SetVFXScaleFactor(vfxScaleFactor);

        // Scale health based on asteroid size
        float t = Mathf.InverseLerp(scaleRange.x, scaleRange.y, scale);
        int health = Mathf.RoundToInt(Mathf.Lerp(healthRange.x, healthRange.y, t));
        asteroid.Initialize(health);

        spawnedAsteroids.Add(asteroidRoot);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireCube(transform.position, spawnAreaSize);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AsteroidSpawner))]
public class AsteroidSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AsteroidSpawner spawner = (AsteroidSpawner)target;
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Asteroids", GUILayout.Height(30)))
        {
            spawner.SpawnAsteroids();
        }
        if (GUILayout.Button("Clear Asteroids", GUILayout.Height(30)))
        {
            spawner.ClearAsteroids();
        }
        EditorGUILayout.EndHorizontal();
    }
}
#endif