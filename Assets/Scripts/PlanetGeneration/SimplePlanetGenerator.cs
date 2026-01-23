using UnityEngine;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimplePlanetGenerator : MonoBehaviour
{
    [Header("Planet Settings")]
    public float radius = 10f;
    public int resolution = 128;
    public int seed = 42;

    [Header("Ocean")]
    public bool hasOcean = true;
    public float oceanDepth = 0.3f;
    public float oceanFloorDepth = 0.15f;

    [Header("Continents")]
    [Range(1, 15)]
    public int continentCount = 5;
    public float continentScale = 0.8f;
    public float continentShelfNoise = 0.3f;
    [Range(0f, 1f)]
    public float continentFragmentation = 0.5f;  // NEW: How broken up continents are
    [Range(0f, 1f)]
    public float coastlineComplexity = 0.6f;     // NEW: Fractal coastline detail
    public float domainWarpStrength = 0.4f;      // NEW: How much to warp the shapes

    [Header("Mountains")]
    public float mountainHeight = 1.5f;
    public float mountainScale = 2f;
    public float mountainLacunarity = 2.2f;
    [Range(0f, 1f)]
    public float mountainRidgeWeight = 0.7f;

    [Header("Plains & Basins")]
    public float plainsScale = 3f;
    public float basinDepth = 0.2f;
    public float basinScale = 1.5f;

    [Header("Detail Noise")]
    public int detailOctaves = 6;
    public float detailScale = 4f;
    public float detailStrength = 0.1f;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    [Header("Craters")]
    public bool hasCraters = true;
    public int craterCount = 20;
    [Range(0.01f, 0.5f)]
    public float craterMinSize = 0.08f;
    [Range(0.1f, 1f)]
    public float craterMaxSize = 0.3f;
    public float craterDepth = 0.3f;
    public float craterRimHeight = 0.15f;

    [Header("GPU Erosion")]
    public ComputeShader erosionComputeShader;
    public bool useGPUErosion = true;

    [Header("Erosion Settings")]
    public int erosionIterations = 100000;
    public int dropletsPerDispatch = 8192;
    public float erosionStrength = 0.3f;
    public float depositionRate = 0.3f;
    public float evaporationRate = 0.02f;
    public float sedimentCapacity = 8f;
    public float minSedimentCapacity = 0.01f;
    public float inertia = 0.1f;
    public float gravity = 10f;
    public int dropletLifetime = 64;

    [Header("Thermal Erosion")]
    public bool thermalErosion = true;
    public int thermalIterations = 5;
    public float talusAngle = 0.6f;
    public float thermalRate = 0.5f;

    [Header("Debug")]
    public bool showProgress = true;

    private MeshFilter meshFilter;
    private float[] heightMap;
    private Vector3[] spherePoints;
    private int[] triangleData;
    private System.Random random;

    // Compute shader buffers
    private ComputeBuffer heightMapBuffer;
    private ComputeBuffer heightChangesBuffer;
    private ComputeBuffer spherePointsBuffer;
    private ComputeBuffer neighborOffsetsBuffer;
    private ComputeBuffer neighborCountsBuffer;
    private ComputeBuffer neighborStartsBuffer;
    private ComputeBuffer randomStatesBuffer;

    // Neighbor data
    private int[] neighborOffsets;
    private int[] neighborCounts;
    private int[] neighborStarts;

    // Crater and continent data
    private List<CraterData> craters = new List<CraterData>();
    private List<Vector3> continentCenters = new List<Vector3>();
    private List<float> continentSizes = new List<float>();
    private List<Vector3> continentWarpOffsets = new List<Vector3>();

    struct CraterData
    {
        public Vector3 center;
        public float radius;
        public float depth;
        public float rimHeight;
        public float rimWidth;
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (GetComponent<MeshRenderer>() == null)
            gameObject.AddComponent<MeshRenderer>();

        GeneratePlanet();
    }

    void OnDestroy()
    {
        ReleaseBuffers();
    }

    void ReleaseBuffers()
    {
        heightMapBuffer?.Release();
        heightChangesBuffer?.Release();
        spherePointsBuffer?.Release();
        neighborOffsetsBuffer?.Release();
        neighborCountsBuffer?.Release();
        neighborStartsBuffer?.Release();
        randomStatesBuffer?.Release();
    }
    #region Planet Generation

    [ContextMenu("Generate Planet")]
    public void GeneratePlanet()
    {
        random = new System.Random(seed);

        GenerateContinentCenters();

        if (hasCraters)
            GenerateCraters();

        Mesh mesh = CreateSphereMeshWithTerrain();
        mesh.RecalculateBounds();
        gameObject.GetComponent<MeshFilter>().mesh = mesh;

        Debug.Log($"Planet generated with {mesh.vertexCount} vertices");
    }

    void GenerateContinentCenters()
    {
        continentCenters.Clear();
        continentSizes.Clear();
        continentWarpOffsets.Clear();

        for (int i = 0; i < continentCount; i++)
        {
            // Fibonacci sphere distribution with perturbation
            float t = (float)i / continentCount;
            float inclination = Mathf.Acos(1 - 2 * t);
            float azimuth = 2 * Mathf.PI * 0.618033988749895f * i;

            inclination += (float)(random.NextDouble() - 0.5) * 0.8f;
            azimuth += (float)(random.NextDouble() - 0.5) * 0.8f;

            Vector3 center = new Vector3(
                Mathf.Sin(inclination) * Mathf.Cos(azimuth),
                Mathf.Sin(inclination) * Mathf.Sin(azimuth),
                Mathf.Cos(inclination)
            );

            continentCenters.Add(center.normalized);

            // Random size for each continent (0.5 to 1.5 base size)
            continentSizes.Add(0.5f + (float)random.NextDouble() * 1.0f);

            // Unique warp offset for each continent
            continentWarpOffsets.Add(new Vector3(
                (float)random.NextDouble() * 100f,
                (float)random.NextDouble() * 100f,
                (float)random.NextDouble() * 100f
            ));
        }
    }

    float CalculateContinentMask(Vector3 point)
    {
        // === DOMAIN WARPING ===
        // Warp the sample point to break up circular patterns
        Vector3 warpedPoint = ApplyDomainWarping(point);

        // === MULTI-LAYER CONTINENT INFLUENCE ===
        float maxInfluence = -1f;

        for (int i = 0; i < continentCenters.Count; i++)
        {
            Vector3 center = continentCenters[i];
            float baseSize = continentSizes[i];
            Vector3 warpOffset = continentWarpOffsets[i];

            // Calculate warped distance (not simple euclidean)
            float influence = CalculateContinentInfluence(
                warpedPoint, center, baseSize, warpOffset);

            maxInfluence = Mathf.Max(maxInfluence, influence);
        }

        // === FRAGMENTATION LAYER ===
        // Adds islands, breaks up continent edges
        float fragmentation = CalculateFragmentation(warpedPoint);
        maxInfluence += fragmentation * continentFragmentation * 0.4f;

        // === COASTLINE COMPLEXITY ===
        // High-frequency noise for detailed coastlines
        float coastDetail = CalculateCoastlineDetail(point);
        maxInfluence += coastDetail * coastlineComplexity * 0.15f;

        return Mathf.Clamp(maxInfluence * 2f - 1f, -1f, 1f);
    }

    Vector3 ApplyDomainWarping(Vector3 point)
    {
        // Multi-octave domain warping for organic shapes
        float warpScale = continentScale * 0.3f;

        // First warp layer (large scale)
        Vector3 warp1 = new Vector3(
            FractalNoise(point + new Vector3(0, 0, 0), warpScale, 3, 2f, 0.5f),
            FractalNoise(point + new Vector3(43, 67, 91), warpScale, 3, 2f, 0.5f),
            FractalNoise(point + new Vector3(113, 157, 193), warpScale, 3, 2f, 0.5f)
        ) * domainWarpStrength;

        Vector3 warped = point + warp1;

        // Second warp layer (medium scale) - warp the warped coordinates
        Vector3 warp2 = new Vector3(
            FractalNoise(warped + new Vector3(173, 211, 239), warpScale * 2f, 2, 2f, 0.5f),
            FractalNoise(warped + new Vector3(263, 281, 307), warpScale * 2f, 2, 2f, 0.5f),
            FractalNoise(warped + new Vector3(337, 359, 383), warpScale * 2f, 2, 2f, 0.5f)
        ) * domainWarpStrength * 0.5f;

        return warped + warp2;
    }

    float CalculateContinentInfluence(Vector3 point, Vector3 center, float baseSize, Vector3 warpOffset)
    {
        // Warp the center-to-point direction for non-circular shapes
        Vector3 toPoint = point - center;

        // Create an irregular "blob" shape using noise
        float angle1 = Mathf.Atan2(toPoint.y, toPoint.x);
        float angle2 = Mathf.Atan2(toPoint.z, Mathf.Sqrt(toPoint.x * toPoint.x + toPoint.y * toPoint.y));

        // Sample noise based on angular position around the continent
        Vector3 angularSample = new Vector3(
            Mathf.Cos(angle1 * 3f) + Mathf.Sin(angle2 * 2f),
            Mathf.Sin(angle1 * 3f) + Mathf.Cos(angle2 * 2f),
            Mathf.Sin(angle1 * 2f + angle2 * 3f)
        ) + warpOffset;

        // Multi-frequency shape distortion
        float shapeNoise = 0f;
        shapeNoise += FractalNoise(angularSample * 0.5f, 1f, 2, 2f, 0.5f) * 0.4f;
        shapeNoise += FractalNoise(angularSample * 1.0f, 2f, 3, 2f, 0.5f) * 0.3f;
        shapeNoise += FractalNoise(angularSample * 2.0f, 4f, 2, 2f, 0.5f) * 0.15f;

        // Modify the effective radius based on direction
        float effectiveRadius = baseSize * (0.6f + shapeNoise * 0.8f);

        // Calculate distance with the modified radius
        float dist = Vector3.Distance(point, center);

        // Add distance-based noise warping too
        float distWarp = FractalNoise(point * continentScale, continentScale, 3, 2f, 0.5f)
                         * continentShelfNoise * 0.5f;
        dist += distWarp;

        // Smooth falloff with modified radius
        float influence = 1f - Mathf.Clamp01(dist / effectiveRadius);

        // Apply smooth step for natural edges
        influence = Mathf.SmoothStep(0, 1, influence);

        // Add "tendrils" - peninsulas and bays
        float tendrils = CalculateTendrils(point, center, warpOffset);
        influence = Mathf.Max(influence, influence * 0.3f + tendrils * 0.7f);

        return influence;
    }

    float CalculateTendrils(Vector3 point, Vector3 center, Vector3 offset)
    {
        // Creates peninsula and bay features extending from continents
        Vector3 dir = (point - center).normalized;

        // Ridged noise creates tendril-like extensions
        float tendrilNoise = RidgedNoise(dir * 3f + offset, 2f, 3, 2.2f, 0.5f);

        float dist = Vector3.Distance(point, center);
        float distFalloff = Mathf.Exp(-dist * 2f); // Exponential falloff

        return tendrilNoise * distFalloff * 0.5f;
    }

    float CalculateFragmentation(Vector3 point)
    {
        // Creates archipelagos and isolated landmasses

        // Large islands
        float largeIslands = RidgedNoise(point, continentScale * 2f, 3, 2f, 0.5f);
        largeIslands = Mathf.Pow(Mathf.Max(0, largeIslands - 0.3f), 1.5f);

        // Small islands (higher frequency)
        float smallIslands = RidgedNoise(point + Vector3.one * 50f, continentScale * 4f, 2, 2f, 0.5f);
        smallIslands = Mathf.Pow(Mathf.Max(0, smallIslands - 0.5f), 2f);

        // Voronoi-based archipelago clusters
        float archipelago = 1f - VoronoiNoise(point, continentScale * 3f);
        archipelago = Mathf.Pow(Mathf.Max(0, archipelago - 0.6f), 2f);

        return largeIslands * 0.5f + smallIslands * 0.25f + archipelago * 0.25f;
    }

    float CalculateCoastlineDetail(Vector3 point)
    {
        // High-frequency detail for realistic coastlines
        float detail = 0f;

        // Multiple octaves of high-frequency noise
        detail += FractalNoise(point, continentScale * 6f, 3, 2.2f, 0.6f) * 0.5f;
        detail += FractalNoise(point + Vector3.one * 30f, continentScale * 12f, 2, 2f, 0.5f) * 0.3f;
        detail += FractalNoise(point + Vector3.one * 60f, continentScale * 24f, 2, 2f, 0.4f) * 0.2f;

        return detail;
    }

    #endregion

    void GenerateCraters()
    {
        craters.Clear();

        for (int i = 0; i < craterCount; i++)
        {
            float theta = (float)(random.NextDouble() * 2 * Mathf.PI);
            float phi = Mathf.Acos(2 * (float)random.NextDouble() - 1);

            Vector3 center = new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi)
            );

            float size = Mathf.Lerp(craterMinSize, craterMaxSize,
                Mathf.Pow((float)random.NextDouble(), 2));

            craters.Add(new CraterData
            {
                center = center,
                radius = size,
                depth = craterDepth * size * 2,
                rimHeight = craterRimHeight * size,
                rimWidth = size * 0.3f
            });
        }

        craters.Sort((a, b) => b.radius.CompareTo(a.radius));
    }

    Mesh CreateSphereMeshWithTerrain()
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Vector3[] directions = {
            Vector3.up, Vector3.down, Vector3.left,
            Vector3.right, Vector3.forward, Vector3.back
        };

        foreach (Vector3 localUp in directions)
        {
            Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
            Vector3 axisB = Vector3.Cross(localUp, axisA);

            int[,] faceIndices = new int[resolution + 1, resolution + 1];

            for (int y = 0; y <= resolution; y++)
            {
                for (int x = 0; x <= resolution; x++)
                {
                    Vector2 percent = new Vector2(x, y) / resolution;
                    Vector3 pointOnCube = localUp
                        + (percent.x - 0.5f) * 2 * axisA
                        + (percent.y - 0.5f) * 2 * axisB;

                    Vector3 pointOnSphere = pointOnCube.normalized;
                    Vector3 roundedPoint = RoundVector(pointOnSphere, 6);

                    if (!vertexMap.TryGetValue(roundedPoint, out int existingIndex))
                    {
                        existingIndex = vertices.Count;
                        vertexMap[roundedPoint] = existingIndex;
                        vertices.Add(pointOnSphere);
                        uvs.Add(CalculateCubeUV(pointOnSphere, localUp, axisA, axisB));
                    }

                    faceIndices[x, y] = existingIndex;
                }
            }

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i00 = faceIndices[x, y];
                    int i10 = faceIndices[x + 1, y];
                    int i01 = faceIndices[x, y + 1];
                    int i11 = faceIndices[x + 1, y + 1];

                    triangles.Add(i00);
                    triangles.Add(i11);
                    triangles.Add(i01);

                    triangles.Add(i00);
                    triangles.Add(i10);
                    triangles.Add(i11);
                }
            }
        }

        spherePoints = vertices.ToArray();
        triangleData = triangles.ToArray();

        // Build neighbor data for erosion
        BuildNeighborData();

        // Generate base heightmap
        heightMap = new float[spherePoints.Length];
        for (int i = 0; i < spherePoints.Length; i++)
        {
            heightMap[i] = CalculateBaseElevation(spherePoints[i]);
        }

        // Apply erosion
        if (useGPUErosion && erosionComputeShader != null)
        {
            RunGPUErosion();
        }
        else
        {
            if (showProgress) Debug.Log("Running CPU erosion (slower)...");
            SimulateHydraulicErosionCPU();
            if (thermalErosion) SimulateThermalErosionCPU();
        }

        // Apply final heights to vertices
        Vector3[] finalVertices = new Vector3[spherePoints.Length];
        for (int i = 0; i < spherePoints.Length; i++)
        {
            float height = heightMap[i];

            if (hasOcean && height < 0)
            {
                float oceanFloor = -oceanFloorDepth;
                height = Mathf.Lerp(oceanFloor, 0, Mathf.Pow(Mathf.InverseLerp(-oceanDepth, 0, height), 0.5f));
            }

            finalVertices[i] = spherePoints[i] * (radius + height);
        }

        mesh.vertices = finalVertices;
        mesh.triangles = triangleData;
        mesh.uv = uvs.ToArray();
        CalculateSmoothNormals(mesh);

        return mesh;
    }

    void BuildNeighborData()
    {
        Dictionary<int, HashSet<int>> neighborSets = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < spherePoints.Length; i++)
        {
            neighborSets[i] = new HashSet<int>();
        }

        // Build from triangle data
        for (int i = 0; i < triangleData.Length; i += 3)
        {
            int a = triangleData[i];
            int b = triangleData[i + 1];
            int c = triangleData[i + 2];

            neighborSets[a].Add(b);
            neighborSets[a].Add(c);
            neighborSets[b].Add(a);
            neighborSets[b].Add(c);
            neighborSets[c].Add(a);
            neighborSets[c].Add(b);
        }

        // Flatten to arrays
        List<int> offsetsList = new List<int>();
        neighborCounts = new int[spherePoints.Length];
        neighborStarts = new int[spherePoints.Length];

        for (int i = 0; i < spherePoints.Length; i++)
        {
            neighborStarts[i] = offsetsList.Count;
            neighborCounts[i] = neighborSets[i].Count;
            offsetsList.AddRange(neighborSets[i]);
        }

        neighborOffsets = offsetsList.ToArray();

        if (showProgress)
            Debug.Log($"Built neighbor data: {spherePoints.Length} vertices, {neighborOffsets.Length} neighbor connections");
    }

    #region GPU Erosion

    void RunGPUErosion()
    {
        if (erosionComputeShader == null)
        {
            Debug.LogError("Erosion compute shader not assigned!");
            return;
        }

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        // Create buffers
        ReleaseBuffers();

        heightMapBuffer = new ComputeBuffer(spherePoints.Length, sizeof(float));
        heightChangesBuffer = new ComputeBuffer(spherePoints.Length, sizeof(float));
        spherePointsBuffer = new ComputeBuffer(spherePoints.Length, sizeof(float) * 3);
        neighborOffsetsBuffer = new ComputeBuffer(neighborOffsets.Length, sizeof(int));
        neighborCountsBuffer = new ComputeBuffer(neighborCounts.Length, sizeof(int));
        neighborStartsBuffer = new ComputeBuffer(neighborStarts.Length, sizeof(int));
        randomStatesBuffer = new ComputeBuffer(dropletsPerDispatch, sizeof(uint));

        // Initialize random states
        uint[] randomStates = new uint[dropletsPerDispatch];
        for (int i = 0; i < dropletsPerDispatch; i++)
        {
            randomStates[i] = (uint)(random.Next());
        }

        // Upload data
        heightMapBuffer.SetData(heightMap);
        heightChangesBuffer.SetData(new float[spherePoints.Length]);
        spherePointsBuffer.SetData(spherePoints);
        neighborOffsetsBuffer.SetData(neighborOffsets);
        neighborCountsBuffer.SetData(neighborCounts);
        neighborStartsBuffer.SetData(neighborStarts);
        randomStatesBuffer.SetData(randomStates);

        // Get kernels
        int hydraulicKernel = erosionComputeShader.FindKernel("HydraulicErosion");
        int thermalKernel = erosionComputeShader.FindKernel("ThermalErosion");
        int applyKernel = erosionComputeShader.FindKernel("ApplyErosionChanges");
        int clearKernel = erosionComputeShader.FindKernel("ClearChangeBuffer");

        // Set buffers for all kernels
        int[] kernels = { hydraulicKernel, thermalKernel, applyKernel, clearKernel };
        foreach (int kernel in kernels)
        {
            erosionComputeShader.SetBuffer(kernel, "heightMap", heightMapBuffer);
            erosionComputeShader.SetBuffer(kernel, "heightChanges", heightChangesBuffer);
            erosionComputeShader.SetBuffer(kernel, "spherePoints", spherePointsBuffer);
            erosionComputeShader.SetBuffer(kernel, "neighborOffsets", neighborOffsetsBuffer);
            erosionComputeShader.SetBuffer(kernel, "neighborCounts", neighborCountsBuffer);
            erosionComputeShader.SetBuffer(kernel, "neighborStarts", neighborStartsBuffer);
            erosionComputeShader.SetBuffer(kernel, "randomStates", randomStatesBuffer);
        }

        // Set parameters
        erosionComputeShader.SetInt("vertexCount", spherePoints.Length);
        erosionComputeShader.SetInt("dropletsPerDispatch", dropletsPerDispatch);
        erosionComputeShader.SetFloat("erosionStrength", erosionStrength);
        erosionComputeShader.SetFloat("depositionRate", depositionRate);
        erosionComputeShader.SetFloat("evaporationRate", evaporationRate);
        erosionComputeShader.SetFloat("sedimentCapacity", sedimentCapacity);
        erosionComputeShader.SetFloat("minSedimentCapacity", minSedimentCapacity);
        erosionComputeShader.SetFloat("inertia", inertia);
        erosionComputeShader.SetFloat("gravity", gravity);
        erosionComputeShader.SetInt("maxLifetime", dropletLifetime);
        erosionComputeShader.SetFloat("talusAngle", talusAngle);
        erosionComputeShader.SetFloat("thermalRate", thermalRate);

        int threadGroups = Mathf.CeilToInt(dropletsPerDispatch / 256f);
        int vertexThreadGroups = Mathf.CeilToInt(spherePoints.Length / 256f);
        int numDispatches = Mathf.CeilToInt((float)erosionIterations / dropletsPerDispatch);

        if (showProgress)
            Debug.Log($"Running {numDispatches} GPU erosion dispatches ({erosionIterations} total droplets)...");

        // Run hydraulic erosion
        for (int i = 0; i < numDispatches; i++)
        {
            // Update random states each dispatch
            for (int j = 0; j < dropletsPerDispatch; j++)
            {
                randomStates[j] = (uint)(random.Next() + i * dropletsPerDispatch + j);
            }
            randomStatesBuffer.SetData(randomStates);

            erosionComputeShader.Dispatch(hydraulicKernel, threadGroups, 1, 1);
            erosionComputeShader.Dispatch(applyKernel, vertexThreadGroups, 1, 1);
            erosionComputeShader.Dispatch(clearKernel, vertexThreadGroups, 1, 1);

            if (showProgress && i % 10 == 0)
            {
                Debug.Log($"Hydraulic erosion: {(i + 1) * 100 / numDispatches}%");
            }
        }

        // Run thermal erosion
        if (thermalErosion)
        {
            for (int i = 0; i < thermalIterations; i++)
            {
                erosionComputeShader.Dispatch(thermalKernel, vertexThreadGroups, 1, 1);
                erosionComputeShader.Dispatch(applyKernel, vertexThreadGroups, 1, 1);
                erosionComputeShader.Dispatch(clearKernel, vertexThreadGroups, 1, 1);
            }
        }

        // Read back results
        heightMapBuffer.GetData(heightMap);

        sw.Stop();
        if (showProgress)
            Debug.Log($"GPU erosion completed in {sw.ElapsedMilliseconds}ms");

        ReleaseBuffers();
    }

    #endregion

    #region CPU Erosion (Fallback)

    void SimulateHydraulicErosionCPU()
    {
        Dictionary<int, List<int>> neighbors = new Dictionary<int, List<int>>();
        for (int i = 0; i < spherePoints.Length; i++)
        {
            neighbors[i] = new List<int>();
            int start = neighborStarts[i];
            int count = neighborCounts[i];
            for (int j = 0; j < count; j++)
            {
                neighbors[i].Add(neighborOffsets[start + j]);
            }
        }

        for (int iter = 0; iter < erosionIterations; iter++)
        {
            if (showProgress && iter % 10000 == 0)
            {
                Debug.Log($"CPU erosion progress: {iter}/{erosionIterations}");
            }

            SimulateDropletCPU(neighbors);
        }
    }

    void SimulateDropletCPU(Dictionary<int, List<int>> neighbors)
    {
        int currentIndex = random.Next(spherePoints.Length);

        Vector3 dir = Vector3.zero;
        float speed = 1f;
        float water = 1f;
        float sediment = 0f;

        for (int lifetime = 0; lifetime < dropletLifetime; lifetime++)
        {
            if (currentIndex < 0 || !neighbors.ContainsKey(currentIndex))
                break;

            var currentNeighbors = neighbors[currentIndex];
            if (currentNeighbors.Count == 0) break;

            float currentHeight = heightMap[currentIndex];

            int lowestNeighbor = -1;
            float lowestHeight = currentHeight;
            Vector3 gradient = Vector3.zero;

            foreach (int n in currentNeighbors)
            {
                float nHeight = heightMap[n];
                Vector3 toNeighbor = spherePoints[n] - spherePoints[currentIndex];
                gradient += toNeighbor.normalized * (currentHeight - nHeight);

                if (nHeight < lowestHeight)
                {
                    lowestHeight = nHeight;
                    lowestNeighbor = n;
                }
            }

            if (lowestNeighbor < 0) break;

            if (gradient.sqrMagnitude > 0)
                gradient.Normalize();

            dir = Vector3.Lerp(gradient, dir, inertia).normalized;

            float heightDiff = currentHeight - lowestHeight;
            float capacity = Mathf.Max(heightDiff * speed * water * sedimentCapacity, minSedimentCapacity);

            if (sediment > capacity || heightDiff < 0)
            {
                float depositAmount = (heightDiff < 0)
                    ? Mathf.Min(sediment, -heightDiff)
                    : (sediment - capacity) * depositionRate;

                sediment -= depositAmount;
                heightMap[currentIndex] += depositAmount;
            }
            else
            {
                float erodeAmount = Mathf.Min((capacity - sediment) * erosionStrength, heightDiff);

                float erodePerVertex = erodeAmount / (currentNeighbors.Count + 1);
                heightMap[currentIndex] -= erodePerVertex;
                foreach (int n in currentNeighbors)
                {
                    heightMap[n] -= erodePerVertex;
                }

                sediment += erodeAmount;
            }

            speed = Mathf.Sqrt(Mathf.Max(0, speed * speed + heightDiff * gravity));
            water *= (1f - evaporationRate);

            if (water < 0.01f) break;

            currentIndex = lowestNeighbor;
        }
    }

    void SimulateThermalErosionCPU()
    {
        for (int iter = 0; iter < thermalIterations; iter++)
        {
            float[] newHeightMap = (float[])heightMap.Clone();

            for (int i = 0; i < spherePoints.Length; i++)
            {
                float currentHeight = heightMap[i];
                int start = neighborStarts[i];
                int count = neighborCounts[i];

                for (int j = 0; j < count; j++)
                {
                    int n = neighborOffsets[start + j];
                    float diff = currentHeight - heightMap[n];

                    if (diff > talusAngle)
                    {
                        float transfer = (diff - talusAngle) * thermalRate / count;
                        newHeightMap[i] -= transfer;
                        newHeightMap[n] += transfer;
                    }
                }
            }

            heightMap = newHeightMap;
        }
    }

    #endregion

    #region Terrain Generation

    float CalculateBaseElevation(Vector3 point)
    {
        float elevation = 0f;

        // Get continent mask (-1 to 1, where positive = land)
        float continentValue = CalculateContinentMask(point);

        // More gradual land/ocean transition with noise
        float transitionNoise = FractalNoise(point * 3f, continentScale * 2f, 2, 2f, 0.5f) * 0.1f;
        float landMask = Mathf.Clamp01((continentValue + transitionNoise) * 1.5f + 0.5f);

        // Use a sharper but still smooth transition
        landMask = Mathf.Pow(landMask, 0.8f);
        landMask = Mathf.SmoothStep(0.1f, 0.9f, landMask);

        // OCEAN
        if (landMask < 0.5f)
        {
            // Deep ocean floor
            float oceanFloor = -oceanDepth;
            float oceanNoise = FractalNoise(point, 1f, 3, 2f, 0.5f) * 0.15f;
            elevation = oceanFloor + oceanNoise;

            // Coastal shelf (gradual rise near land)
            float shelfBlend = Mathf.InverseLerp(0f, 0.5f, landMask);
            float shelfHeight = Mathf.Lerp(oceanFloor, -oceanDepth * 0.2f, shelfBlend);
            elevation = Mathf.Lerp(elevation, shelfHeight, shelfBlend);
        }
        // LAND
        else
        {
            float landBlend = Mathf.InverseLerp(0.5f, 1f, landMask);

            // Base land elevation (above sea level)
            elevation = landBlend * 0.2f;

            // Coastal lowlands vs interior highlands
            float interiorMask = Mathf.Pow(landBlend, 2f);

            // Mountains only in continental interiors
            float mountains = CalculateMountains(point);
            mountains *= interiorMask; // No mountains at coasts

            // Add mountain ranges
            elevation += mountains;

            // Plains between mountains
            float plains = CalculatePlains(point) * 0.15f;
            plains *= (1f - Mathf.Clamp01(mountains * 2f)); // Flatten where mountains are
            elevation += plains;

            // Subtle detail
            float detail = CalculateDetailNoise(point);
            detail *= (0.3f + mountains * 0.3f); // More detail on mountains
            elevation += detail;
        }

        // Craters affect both land and ocean
        if (hasCraters)
        {
            elevation += CalculateCraterEffect(point);
        }

        return elevation;
    }

    float CalculateBaseShape(Vector3 point)
    {
        return FractalNoise(point, 0.5f, 3, 2f, 0.5f);
    }

    float CalculateMountains(Vector3 point)
    {
        // Mountain range mask - creates linear/arc patterns
        float rangeMask = 0f;

        // Create several mountain range "spines"
        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = new Vector3(i * 50f, i * 73f, i * 31f);
            float spine = RidgedNoise(point + offset, mountainScale * 0.3f, 2, 2f, 0.5f);
            spine = Mathf.Pow(Mathf.Max(0, spine), 2f);
            rangeMask = Mathf.Max(rangeMask, spine);
        }

        // Detail within ranges
        float ridged = RidgedNoise(point, mountainScale, 5, mountainLacunarity, 0.5f);
        float peaks = Mathf.Pow(Mathf.Max(0, ridged), 1.5f); // Sharper peaks

        // Combine range mask with peak detail
        float mountains = peaks * rangeMask * mountainHeight;

        // Add some variation
        float variation = FractalNoise(point + Vector3.one * 200f, mountainScale * 2f, 3, 2f, 0.5f);
        mountains *= (0.7f + variation * 0.6f);

        return mountains;
    }

    float CalculatePlains(Vector3 point)
    {
        float plains = FractalNoise(point, plainsScale, 4, 2f, 0.6f);
        float terraced = Mathf.Floor(plains * 4f) / 4f;
        plains = Mathf.Lerp(plains, terraced, 0.3f);
        return plains;
    }

    float CalculateBasins(Vector3 point)
    {
        float basins = FractalNoise(point + Vector3.one * 100f, basinScale, 3, 2f, 0.5f);
        float voronoi = VoronoiNoise(point, basinScale * 0.5f);
        basins = Mathf.Lerp(basins, voronoi, 0.5f);
        return Mathf.Max(0, -basins);
    }

    float CalculateDetailNoise(Vector3 point)
    {
        return FractalNoise(point, detailScale, detailOctaves, lacunarity, persistence) * detailStrength;
    }

    float CalculateCraterEffect(Vector3 point)
    {
        float totalEffect = 0f;

        foreach (var crater in craters)
        {
            float dist = Vector3.Distance(point, crater.center);
            if (dist > crater.radius * 2f) continue;

            float normalizedDist = dist / crater.radius;

            if (normalizedDist < 1f)
            {
                float bowl = Mathf.Pow(normalizedDist, 2f) - 1f;
                totalEffect += bowl * crater.depth;
            }
            else if (normalizedDist < 1f + crater.rimWidth / crater.radius)
            {
                float rimDist = (normalizedDist - 1f) / (crater.rimWidth / crater.radius);
                float rim = Mathf.Sin(rimDist * Mathf.PI) * crater.rimHeight;
                totalEffect += rim;
            }
        }

        return totalEffect;
    }

    Vector2 CalculateCubeUV(Vector3 pointOnSphere, Vector3 localUp, Vector3 axisA, Vector3 axisB)
    {
        // Project back to cube face
        float scale = 1f / Mathf.Max(
            Mathf.Abs(pointOnSphere.x),
            Mathf.Max(Mathf.Abs(pointOnSphere.y), Mathf.Abs(pointOnSphere.z))
        );
        Vector3 pointOnCube = pointOnSphere * scale;

        // Get UV from the face axes
        float u = Vector3.Dot(pointOnCube, axisA) * 0.5f + 0.5f;
        float v = Vector3.Dot(pointOnCube, axisB) * 0.5f + 0.5f;

        return new Vector2(u, v);
    }

    #endregion

    #region Noise Functions

    float FractalNoise(Vector3 point, float scale, int octaves, float lacunarity, float persistence)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = scale;
        float maxValue = 0f;

        for (int i = 0; i < octaves; i++)
        {
            value += PerlinNoise3D(point * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return value / maxValue;
    }

    float RidgedNoise(Vector3 point, float scale, int octaves, float lacunarity, float persistence)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = scale;
        float weight = 1f;

        for (int i = 0; i < octaves; i++)
        {
            float n = PerlinNoise3D(point * frequency);
            n = 1f - Mathf.Abs(n);
            n *= n;
            n *= weight;
            weight = Mathf.Clamp01(n * 2f);

            value += n * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return value;
    }

    float VoronoiNoise(Vector3 point, float scale)
    {
        point *= scale;

        Vector3 baseCell = new Vector3(
            Mathf.Floor(point.x),
            Mathf.Floor(point.y),
            Mathf.Floor(point.z)
        );

        float minDist = 10f;
        float secondMinDist = 10f;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 cell = baseCell + new Vector3(x, y, z);
                    Vector3 cellPoint = cell + Hash3D(cell);
                    float dist = Vector3.Distance(point, cellPoint);

                    if (dist < minDist)
                    {
                        secondMinDist = minDist;
                        minDist = dist;
                    }
                    else if (dist < secondMinDist)
                    {
                        secondMinDist = dist;
                    }
                }
            }
        }

        return secondMinDist - minDist;
    }

    Vector3 Hash3D(Vector3 p)
    {
        p = new Vector3(
            p.x * 127.1f + p.y * 311.7f + p.z * 74.7f,
            p.x * 269.5f + p.y * 183.3f + p.z * 246.1f,
            p.x * 113.5f + p.y * 271.9f + p.z * 124.6f
        );

        return new Vector3(
            Frac(Mathf.Sin(p.x) * 43758.5453f),
            Frac(Mathf.Sin(p.y) * 43758.5453f),
            Frac(Mathf.Sin(p.z) * 43758.5453f)
        );
    }

    float Frac(float x) => x - Mathf.Floor(x);

    float PerlinNoise3D(Vector3 point)
    {
        point += new Vector3(100f + seed, 100f + seed * 2, 100f + seed * 3);

        float xy = Mathf.PerlinNoise(point.x, point.y);
        float xz = Mathf.PerlinNoise(point.x, point.z);
        float yz = Mathf.PerlinNoise(point.y, point.z);
        float yx = Mathf.PerlinNoise(point.y, point.x);
        float zx = Mathf.PerlinNoise(point.z, point.x);
        float zy = Mathf.PerlinNoise(point.z, point.y);

        return ((xy + xz + yz + yx + zx + zy) / 6f) * 2f - 1f;
    }

    #endregion


    void CalculateSmoothNormals(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3[] normals = new Vector3[vertices.Length];

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector3 edge1 = vertices[i1] - vertices[i0];
            Vector3 edge2 = vertices[i2] - vertices[i0];
            Vector3 faceNormal = Vector3.Cross(edge1, edge2);

            normals[i0] += faceNormal;
            normals[i1] += faceNormal;
            normals[i2] += faceNormal;
        }

        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = normals[i].normalized;
        }

        mesh.normals = normals;
    }

    Vector3 RoundVector(Vector3 v, int decimals)
    {
        float multiplier = Mathf.Pow(10, decimals);
        return new Vector3(
            Mathf.Round(v.x * multiplier) / multiplier,
            Mathf.Round(v.y * multiplier) / multiplier,
            Mathf.Round(v.z * multiplier) / multiplier
        );
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimplePlanetGenerator))]
public class SimplePlanetGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SimplePlanetGenerator generator = (SimplePlanetGenerator)target;

        EditorGUILayout.Space();

        if (generator.erosionComputeShader == null)
        {
            EditorGUILayout.HelpBox("Assign a Compute Shader for GPU erosion (much faster)", MessageType.Warning);
        }

        if (GUILayout.Button("Generate Planet", GUILayout.Height(30)))
        {
            generator.GeneratePlanet();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Performance Tips:\n" +
            "• GPU erosion: ~100x faster than CPU\n" +
            "• Resolution 128: ~98k vertices\n" +
            "• Resolution 256: ~393k vertices\n" +
            "• 100k droplets GPU: ~500ms\n" +
            "• 100k droplets CPU: ~30-60 seconds",
            MessageType.Info);
    }
}
#endif