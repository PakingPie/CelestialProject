using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;


public class TerrainGrid : MonoBehaviour
{
    public bool displayGridGizmos;
    public LayerMask UnwalkableMask;
    public Vector2 GridWorldSize;
    public float NodeRadius;

    // [HideInInspector]
    public List<Node> Path;

    public TerrainType[] walkableRegions;

    public int obstacleProximityPenalty = 10;
    LayerMask walkableMask;
    Dictionary<int, int> walkableRegionsDictionary = new Dictionary<int, int>();

    Node[,] grid;

    float nodeDiameter;
    int gridSizeX, gridSizeY;

    int minPenalty = int.MaxValue;
    int maxPenalty = int.MinValue;

    // Awake is called when the script instance is being loaded
    void Awake()
    {
        nodeDiameter = NodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(GridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(GridWorldSize.y / nodeDiameter);

        foreach (TerrainType region in walkableRegions)
        {
            walkableMask.value |= region.mask.value;
            walkableRegionsDictionary.Add((int)Mathf.Log(region.mask.value, 2), region.penalty);
        }

        CreateGrid();
    }

    public int MaxGridSize
    {
        get
        {
            return gridSizeX * gridSizeY;
        }
    }

    void Update()
    {
        CreateGrid();
    }

    void CreateGrid()
    {
        grid = new Node[gridSizeX, gridSizeY];
        Vector3 worldBottomLeft = transform.position - Vector3.right * GridWorldSize.x / 2 - Vector3.forward * GridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + NodeRadius) + Vector3.forward * (y * nodeDiameter + NodeRadius);
                bool walkable = !Physics.CheckSphere(worldPoint, NodeRadius, UnwalkableMask);

                int movementPenalty = 0;

                Ray ray = new Ray(worldPoint + Vector3.up * 50, Vector3.down);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 100, walkableMask))
                {
                    walkableRegionsDictionary.TryGetValue(hit.collider.gameObject.layer, out movementPenalty);
                }

                if (!walkable)
                {
                    movementPenalty += obstacleProximityPenalty;
                }

                grid[x, y] = new Node(walkable, worldPoint, new Vector2Int(x, y), movementPenalty);
            }
        }

        BlurPenaltyMap(3);
    }

    // https://www.youtube.com/watch?v=Tb-rM3wGwv4&list=PLFt_AvWsXl0cq5Umv3pMC9SPnKjfp9eGW&index=7&ab_channel=SebastianLague
    void BlurPenaltyMap(int blurSize)
    {
        int kernelSize = blurSize * 2 + 1;
        int kernelExtents = (kernelSize - 1) / 2;

        int[,] penaltiesHorizontal = new int[gridSizeX, gridSizeY];
        int[,] penaltiesVertical = new int[gridSizeX, gridSizeY];

        for (int y = 0; y < gridSizeY; y++)
        {
            // pre-acquaire one column of grid before calculate the entire blur array, calculate grid[:, y]'s penalties
            for (int x = -kernelExtents; x <= kernelExtents; x++)
            {
                int sampleX = Mathf.Clamp(x, 0, kernelExtents - 1);
                penaltiesHorizontal[0, y] += grid[sampleX, y].movementPenalty;
            }

            for (int x = 1; x < gridSizeX; x++)
            {
                int removeIndex = Mathf.Clamp(x - kernelExtents - 1, 0, gridSizeX);
                int addIndex = Mathf.Clamp(x + kernelExtents, 0, gridSizeX - 1);
                penaltiesHorizontal[x, y] = penaltiesHorizontal[x - 1, y] - grid[removeIndex, y].movementPenalty + grid[addIndex, y].movementPenalty;
            }
        }

        for (int x = 0; x < gridSizeX; x++)
        {
            // pre-acquaire one row of grid, calculate grid[x, :]'s penalties
            for (int y = -kernelExtents; y <= kernelExtents; y++)
            {
                int sampleY = Mathf.Clamp(y, 0, kernelExtents - 1);
                penaltiesVertical[x, 0] += penaltiesHorizontal[x, sampleY];
            }

            int blurPenalty = Mathf.RoundToInt((float)penaltiesVertical[x, 0] / (kernelSize * kernelSize));
            grid[x, 0].movementPenalty = blurPenalty;

            for (int y = 1; y < gridSizeY; y++)
            {
                int removeIndex = Mathf.Clamp(y - kernelExtents - 1, 0, gridSizeY);
                int addIndex = Mathf.Clamp(y + kernelExtents, 0, gridSizeY - 1);
                penaltiesVertical[x, y] = penaltiesVertical[x, y - 1] - penaltiesHorizontal[x, removeIndex] + penaltiesHorizontal[x, addIndex];

                blurPenalty = Mathf.RoundToInt((float)penaltiesVertical[x, y] / (kernelSize * kernelSize));
                grid[x, y].movementPenalty = blurPenalty;

                if (blurPenalty < minPenalty)
                {
                    minPenalty = blurPenalty;
                }

                if (blurPenalty > maxPenalty)
                {
                    maxPenalty = blurPenalty;
                }
            }
        }
    }

    public List<Node> GetNeighbours(Node node)
    {
        List<Node> neighbours = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridPosition.x + x;
                int checkY = node.gridPosition.y + y;
                // Ensure we don't go out of bounds
                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbours.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbours;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        float percentX = (worldPosition.x + GridWorldSize.x / 2) / GridWorldSize.x;
        float percentY = (worldPosition.z + GridWorldSize.y / 2) / GridWorldSize.y;

        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(GridWorldSize.x, 1, GridWorldSize.y));

        if (grid != null && displayGridGizmos)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = Color.Lerp(Color.white, Color.black, Mathf.InverseLerp(minPenalty, maxPenalty, n.movementPenalty));
                Gizmos.color = n.isWalkable ? Gizmos.color : Color.red;
                Gizmos.DrawWireCube(n.wolrdPosition, Vector3.one * nodeDiameter);
            }
        }
    }

    [System.Serializable]
    public class TerrainType
    {
        public LayerMask mask;
        public int penalty;

        public TerrainType(LayerMask mask, int penalty)
        {
            this.mask = mask;
            this.penalty = penalty;
        }
    }
}