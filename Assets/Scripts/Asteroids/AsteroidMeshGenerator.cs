using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class AsteroidRingSpawner : MonoBehaviour
{
    [Header("Ring Settings")]
    [SerializeField] private float innerRadius = 50f;
    [SerializeField] private float outerRadius = 100f;
    [SerializeField] private float ringThickness = 5f;
    [SerializeField] private Transform planetCenter;
    
    [Header("Spawn Settings")]
    [SerializeField] private int asteroidCount = 1000;
    [SerializeField] private int seed = 42;
    
    [Header("Asteroid Settings")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 3f);
    [SerializeField] private Material asteroidMaterial;
    [SerializeField] private Color asteroidColor = new Color(0.5f, 0.45f, 0.4f);
    
    [Header("LOD Settings")]
    [SerializeField] private float lodDistance1 = 100f;
    [SerializeField] private float lodDistance2 = 300f;
    [SerializeField] private float cullDistance = 500f;
    
    [Header("Mesh Variants")]
    [SerializeField, Range(2, 32)] private int highResolution = 16;
    [SerializeField, Range(2, 16)] private int mediumResolution = 8;
    [SerializeField, Range(2, 8)] private int lowResolution = 4;
    [SerializeField] private int meshVariantCount = 10;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private Mesh[] _highLodMeshes;
    private Mesh[] _mediumLodMeshes;
    private Mesh[] _lowLodMeshes;
    
    private NativeArray<AsteroidData> _asteroidData;
    private NativeArray<Matrix4x4> _matrices;
    private NativeArray<int> _meshIndices;
    
    private List<Matrix4x4>[] _highLodBatches;
    private List<Matrix4x4>[] _mediumLodBatches;
    private List<Matrix4x4>[] _lowLodBatches;
    
    private MaterialPropertyBlock _propertyBlock;
    private bool _isInitialized;
    
    private int _visibleCount;
    private int _highLodCount;
    private int _mediumLodCount;
    private int _lowLodCount;

    private struct AsteroidData
    {
        public Quaternion Rotation;
        public float Scale;
        public int MeshIndex;
        public float OrbitSpeed;
        public float OrbitAngle;
        public float OrbitDistance;
        public float HeightOffset;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
        DisposeNativeArrays();
    }

    private void OnDestroy()
    {
        DisposeNativeArrays();
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!Application.isPlaying && _isInitialized)
        {
            // Force scene view repaint
            SceneView.RepaintAll();
        }
    }
#endif

    private void DisposeNativeArrays()
    {
        if (_asteroidData.IsCreated) _asteroidData.Dispose();
        if (_matrices.IsCreated) _matrices.Dispose();
        if (_meshIndices.IsCreated) _meshIndices.Dispose();
    }

    public void GenerateRing()
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        
        DisposeNativeArrays();
        
        _propertyBlock = new MaterialPropertyBlock();
        
        EnsureMaterial();
        
        Random.InitState(seed);
        
        Debug.Log($"AsteroidRingSpawner: Generating {meshVariantCount} mesh variants...");
        GenerateMeshVariants();
        
        Debug.Log($"AsteroidRingSpawner: Generating {asteroidCount} asteroid positions...");
        GenerateAsteroidData();
        
        _highLodBatches = new List<Matrix4x4>[meshVariantCount];
        _mediumLodBatches = new List<Matrix4x4>[meshVariantCount];
        _lowLodBatches = new List<Matrix4x4>[meshVariantCount];
        
        for (int i = 0; i < meshVariantCount; i++)
        {
            _highLodBatches[i] = new List<Matrix4x4>(256);
            _mediumLodBatches[i] = new List<Matrix4x4>(512);
            _lowLodBatches[i] = new List<Matrix4x4>(1024);
        }
        
        // Initialize matrices immediately so we can render in edit mode
        UpdateAsteroidMatrices(0f);
        
        _isInitialized = true;
        
        sw.Stop();
        Debug.Log($"AsteroidRingSpawner: Ring generated in {sw.ElapsedMilliseconds}ms");
        Debug.Log($"AsteroidRingSpawner: Inner radius: {innerRadius}, Outer radius: {outerRadius}");
        
#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    private void EnsureMaterial()
    {
        if (asteroidMaterial != null && asteroidMaterial.enableInstancing) return;
        
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        
        if (shader == null)
        {
            Debug.LogError("AsteroidRingSpawner: Could not find any valid shader!");
            return;
        }
        
        asteroidMaterial = new Material(shader);
        asteroidMaterial.name = "Asteroid Material (Auto-generated)";
        asteroidMaterial.color = asteroidColor;
        asteroidMaterial.enableInstancing = true;
        
        Debug.Log($"AsteroidRingSpawner: Created material with shader '{shader.name}', GPU Instancing: {asteroidMaterial.enableInstancing}");
    }

    private Vector3 GetRingCenter()
    {
        return planetCenter != null ? planetCenter.position : transform.position;
    }

    private void GenerateMeshVariants()
    {
        // Clean up old meshes
        if (_highLodMeshes != null)
        {
            foreach (var mesh in _highLodMeshes) if (mesh != null) DestroyImmediate(mesh);
            foreach (var mesh in _mediumLodMeshes) if (mesh != null) DestroyImmediate(mesh);
            foreach (var mesh in _lowLodMeshes) if (mesh != null) DestroyImmediate(mesh);
        }
        
        _highLodMeshes = new Mesh[meshVariantCount];
        _mediumLodMeshes = new Mesh[meshVariantCount];
        _lowLodMeshes = new Mesh[meshVariantCount];

        for (int i = 0; i < meshVariantCount; i++)
        {
            _highLodMeshes[i] = GenerateAsteroidMesh(1f, highResolution, i);
            _highLodMeshes[i].name = $"Asteroid_High_{i}";
            
            _mediumLodMeshes[i] = GenerateAsteroidMesh(1f, mediumResolution, i);
            _mediumLodMeshes[i].name = $"Asteroid_Medium_{i}";
            
            _lowLodMeshes[i] = GenerateAsteroidMesh(1f, lowResolution, i);
            _lowLodMeshes[i].name = $"Asteroid_Low_{i}";
        }
        
        Debug.Log($"AsteroidRingSpawner: High LOD mesh has {_highLodMeshes[0].vertexCount} vertices");
    }

    private Mesh GenerateAsteroidMesh(float radius, int resolution, int variantSeed)
    {
        Random.State previousState = Random.state;
        Random.InitState(seed + variantSeed * 1000);

        AsteroidShapeGenerator shapeGenerator = new AsteroidShapeGenerator(
            radius,
            Random.Range(0.3f, 0.6f),
            Random.Range(3, 5),
            Random.Range(0.5f, 1.0f),
            Random.Range(1.5f, 2.0f),
            0.5f
        );

        Vector3[] directions = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
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
        combinedMesh.Optimize();

        for (int i = 0; i < 6; i++)
        {
            if (combineInstances[i].mesh != null)
            {
                DestroyImmediate(combineInstances[i].mesh);
            }
        }

        Random.state = previousState;
        return combinedMesh;
    }

    private void GenerateAsteroidData()
    {
        _asteroidData = new NativeArray<AsteroidData>(asteroidCount, Allocator.Persistent);
        _matrices = new NativeArray<Matrix4x4>(asteroidCount, Allocator.Persistent);
        _meshIndices = new NativeArray<int>(asteroidCount, Allocator.Persistent);

        for (int i = 0; i < asteroidCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float t = Mathf.Sqrt(Random.Range(0f, 1f));
            float distance = Mathf.Lerp(innerRadius, outerRadius, t);
            float heightOffset = Random.Range(-ringThickness / 2f, ringThickness / 2f);

            float clusterNoise = Mathf.PerlinNoise(angle * 3f + seed, distance * 0.05f);
            heightOffset *= (0.5f + clusterNoise);

            AsteroidData data = new AsteroidData
            {
                Rotation = Random.rotation,
                Scale = Random.Range(scaleRange.x, scaleRange.y),
                MeshIndex = Random.Range(0, meshVariantCount),
                OrbitSpeed = Random.Range(0.1f, 0.5f) * (Random.value > 0.5f ? 1f : -1f) / Mathf.Sqrt(distance),
                OrbitAngle = angle,
                OrbitDistance = distance,
                HeightOffset = heightOffset
            };

            _asteroidData[i] = data;
            _meshIndices[i] = data.MeshIndex;
        }
    }

    private void Update()
    {
        if (!_isInitialized) return;

        float time = Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup;
        UpdateAsteroidMatrices(time);
        RenderAsteroids();
    }

    private void UpdateAsteroidMatrices(float time)
    {
        if (!_asteroidData.IsCreated) return;
        
        Vector3 center = GetRingCenter();

        var job = new UpdateAsteroidPositionsJob
        {
            AsteroidData = _asteroidData,
            Matrices = _matrices,
            Center = center,
            RingRotation = transform.rotation,
            Time = time
        };

        JobHandle handle = job.Schedule(asteroidCount, 128);
        handle.Complete();
    }

    [BurstCompile]
    private struct UpdateAsteroidPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<AsteroidData> AsteroidData;
        [WriteOnly] public NativeArray<Matrix4x4> Matrices;
        public Vector3 Center;
        public Quaternion RingRotation;
        public float Time;

        public void Execute(int index)
        {
            AsteroidData data = AsteroidData[index];
            
            float currentAngle = data.OrbitAngle + Time * data.OrbitSpeed;
            
            Vector3 localPos = new Vector3(
                Mathf.Cos(currentAngle) * data.OrbitDistance,
                data.HeightOffset,
                Mathf.Sin(currentAngle) * data.OrbitDistance
            );

            Vector3 worldPos = Center + RingRotation * localPos;
            
            float rotSpeed = 5f + (index % 10);
            Quaternion rotation = data.Rotation * Quaternion.Euler(
                Time * rotSpeed,
                Time * rotSpeed * 1.3f,
                Time * rotSpeed * 0.7f
            );
            
            Matrices[index] = Matrix4x4.TRS(worldPos, rotation, Vector3.one * data.Scale);
        }
    }

    private void RenderAsteroids()
    {
        if (asteroidMaterial == null || !_matrices.IsCreated) return;

        Camera cam = Camera.current;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        ClearBatches();
        
        Vector3 cameraPos = cam.transform.position;
        float cullDistSq = cullDistance * cullDistance;
        float lod1DistSq = lodDistance1 * lodDistance1;
        float lod2DistSq = lodDistance2 * lodDistance2;

        _visibleCount = 0;
        _highLodCount = 0;
        _mediumLodCount = 0;
        _lowLodCount = 0;

        for (int i = 0; i < asteroidCount; i++)
        {
            Matrix4x4 matrix = _matrices[i];
            Vector3 asteroidPos = new Vector3(matrix.m03, matrix.m13, matrix.m23);
            
            float distSq = (asteroidPos - cameraPos).sqrMagnitude;
            
            if (distSq > cullDistSq) continue;

            _visibleCount++;
            int meshIndex = _meshIndices[i];

            if (distSq < lod1DistSq)
            {
                _highLodBatches[meshIndex].Add(matrix);
                _highLodCount++;
            }
            else if (distSq < lod2DistSq)
            {
                _mediumLodBatches[meshIndex].Add(matrix);
                _mediumLodCount++;
            }
            else
            {
                _lowLodBatches[meshIndex].Add(matrix);
                _lowLodCount++;
            }
        }

        RenderBatches(_highLodBatches, _highLodMeshes);
        RenderBatches(_mediumLodBatches, _mediumLodMeshes);
        RenderBatches(_lowLodBatches, _lowLodMeshes);
    }

    private void ClearBatches()
    {
        if (_highLodBatches == null) return;
        
        for (int i = 0; i < meshVariantCount; i++)
        {
            _highLodBatches[i].Clear();
            _mediumLodBatches[i].Clear();
            _lowLodBatches[i].Clear();
        }
    }

    private void RenderBatches(List<Matrix4x4>[] batches, Mesh[] meshes)
    {
        if (batches == null || meshes == null) return;
        
        Matrix4x4[] tempArray = new Matrix4x4[1023];
        
        for (int meshIndex = 0; meshIndex < meshVariantCount; meshIndex++)
        {
            List<Matrix4x4> matrices = batches[meshIndex];
            if (matrices.Count == 0) continue;

            Mesh mesh = meshes[meshIndex];
            if (mesh == null) continue;
            
            for (int i = 0; i < matrices.Count; i += 1023)
            {
                int count = Mathf.Min(1023, matrices.Count - i);
                
                for (int j = 0; j < count; j++)
                {
                    tempArray[j] = matrices[i + j];
                }

                Graphics.DrawMeshInstanced(mesh, 0, asteroidMaterial, tempArray, count, _propertyBlock);
            }
        }
    }

    public void ClearRing()
    {
        DisposeNativeArrays();
        
        if (_highLodMeshes != null)
        {
            foreach (var mesh in _highLodMeshes) if (mesh != null) DestroyImmediate(mesh);
            foreach (var mesh in _mediumLodMeshes) if (mesh != null) DestroyImmediate(mesh);
            foreach (var mesh in _lowLodMeshes) if (mesh != null) DestroyImmediate(mesh);
        }
        
        _highLodMeshes = null;
        _mediumLodMeshes = null;
        _lowLodMeshes = null;
        _highLodBatches = null;
        _mediumLodBatches = null;
        _lowLodBatches = null;
        
        _isInitialized = false;
        
        Debug.Log("AsteroidRingSpawner: Ring cleared");
        
#if UNITY_EDITOR
        SceneView.RepaintAll();
#endif
    }

    private void OnGUI()
    {
        if (!showDebugInfo || !_isInitialized || !Application.isPlaying) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label($"Asteroid Ring Debug Info");
        GUILayout.Label($"Total Asteroids: {asteroidCount}");
        GUILayout.Label($"Visible: {_visibleCount}");
        GUILayout.Label($"High LOD: {_highLodCount}");
        GUILayout.Label($"Medium LOD: {_mediumLodCount}");
        GUILayout.Label($"Low LOD: {_lowLodCount}");
        GUILayout.Label($"Culled: {asteroidCount - _visibleCount}");
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = GetRingCenter();
        
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
        DrawWireDisc(center, transform.up, innerRadius, 64);
        
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
        DrawWireDisc(center, transform.up, outerRadius, 64);
        
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        float midRadius = (innerRadius + outerRadius) / 2f;
        DrawWireDisc(center + transform.up * ringThickness / 2f, transform.up, midRadius, 32);
        DrawWireDisc(center - transform.up * ringThickness / 2f, transform.up, midRadius, 32);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 1f);
    }

    private void DrawWireDisc(Vector3 center, Vector3 normal, float radius, int segments)
    {
        Vector3 from = Vector3.Cross(normal, Vector3.up).normalized;
        if (from.sqrMagnitude < 0.001f)
            from = Vector3.Cross(normal, Vector3.right).normalized;

        Vector3 prevPoint = center + Quaternion.AngleAxis(0, normal) * from * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * 360f;
            Vector3 nextPoint = center + Quaternion.AngleAxis(angle, normal) * from * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (innerRadius > outerRadius)
            innerRadius = outerRadius - 1f;
        if (innerRadius < 1f) innerRadius = 1f;
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(AsteroidRingSpawner))]
public class AsteroidRingSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AsteroidRingSpawner spawner = (AsteroidRingSpawner)target;
        
        EditorGUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("Generate Ring", GUILayout.Height(35)))
        {
            spawner.GenerateRing();
            EditorUtility.SetDirty(spawner);
        }
        
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Clear Ring", GUILayout.Height(35)))
        {
            spawner.ClearRing();
            EditorUtility.SetDirty(spawner);
        }
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }
}
#endif