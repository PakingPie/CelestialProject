using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AsteroidSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private int asteroidCount = 10;
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(100f, 100f, 100f);
    [SerializeField] private float minDistanceBetweenAsteroids = 10f;
    
    [Header("Asteroid Settings")]
    [SerializeField, Range(2, 64)] private int resolution = 16;
    [SerializeField] private Vector2 radiusRange = new Vector2(2f, 8f);
    [SerializeField] private Material asteroidMaterial;
    
    [Header("Noise Settings")]
    [SerializeField] private Vector2Int layersRange = new Vector2Int(4, 6);
    [SerializeField] private Vector2 baseRoughnessRange = new Vector2(0.5f, 1.0f);
    [SerializeField] private Vector2 roughnessRange = new Vector2(1.5f, 2.0f);
    [SerializeField] private float strength = 0.5f;
    [SerializeField] private float persistence = 0.5f;
    
    [SerializeField, HideInInspector]
    private List<GameObject> spawnedAsteroids = new List<GameObject>();

    public void SpawnAsteroids()
    {
        ClearAsteroids();
        
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
        GameObject asteroidRoot = new GameObject($"Asteroid_{spawnedAsteroids.Count}");
        asteroidRoot.transform.parent = transform;
        asteroidRoot.transform.position = position;
        asteroidRoot.transform.rotation = Random.rotation;

        float radius = Random.Range(radiusRange.x, radiusRange.y);
        AsteroidShapeGenerator shapeGenerator = new AsteroidShapeGenerator(
            radius,
            strength,
            Random.Range(layersRange.x, layersRange.y),
            Random.Range(baseRoughnessRange.x, baseRoughnessRange.y),
            Random.Range(roughnessRange.x, roughnessRange.y),
            persistence
        );

        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        // Create a single combined mesh for better performance
        CombineInstance[] combineInstances = new CombineInstance[6];

        for (int i = 0; i < 6; i++)
        {
            Mesh faceMesh = new Mesh();
            AsteroidMeshOptimized.ConstructMesh(faceMesh, shapeGenerator, resolution, directions[i]);
            
            combineInstances[i].mesh = faceMesh;
            combineInstances[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combineInstances, true, true);
        combinedMesh.RecalculateNormals();
        combinedMesh.RecalculateBounds();

        // Clean up temporary meshes
        for (int i = 0; i < 6; i++)
        {
            DestroyImmediate(combineInstances[i].mesh);
        }

        MeshFilter meshFilter = asteroidRoot.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = combinedMesh;

        MeshRenderer renderer = asteroidRoot.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = asteroidMaterial != null 
            ? asteroidMaterial 
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // Optional: Add collider
        MeshCollider collider = asteroidRoot.AddComponent<MeshCollider>();
        collider.sharedMesh = combinedMesh;

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