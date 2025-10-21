#define USE_HEAP
// define USE_STOPWATCH

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

#if USE_STOPWATCH
using System.Diagnostics;
#endif
public class PathFinding : MonoBehaviour
{
    TerrainGrid grid;

    void Awake()
    {
        grid = GetComponent<TerrainGrid>();
        if (grid == null)
        {
            print("Grid component not found on PathFinding object.");
        }
    }

    public void FindPath(PathRequest request, Action<PathResult> callback)
    {
#if USE_STOPWATCH
        Stopwatch sw = new Stopwatch();
        sw.Start();
#endif

        Vector3[] waypoints = new Vector3[0];
        bool pathSuccess = false;

        Node startNode = grid.NodeFromWorldPoint(request.start);
        Node endNode = grid.NodeFromWorldPoint(request.end);

        if (startNode.isWalkable && endNode.isWalkable)
        {
#if USE_HEAP
            Heap<Node> openSet = new Heap<Node>(grid.MaxGridSize);
#else
        List<Node> openSet = new List<Node>();
#endif
            HashSet<Node> closedSet = new HashSet<Node>();
            openSet.Add(startNode);

            while (openSet.Count > 0)
            {
#if USE_HEAP
                // Use Heap 
                Node currentNode = openSet.RemoveFirst(); // Get the node with the lowest fCost from the heap
                closedSet.Add(currentNode);
#else
            // // Origin Method: Linear Search
            Node currentNode = openSet[0];
            // Find the node with the lowest fCost
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || openSet[i].fCost == currentNode.fCost)
                {
                    if (openSet[i].hCost < currentNode.hCost)
                        currentNode = openSet[i];
                }
            }
            openSet.Remove(currentNode);
            closedSet.Add(currentNode);
#endif

                if (currentNode == endNode)
                {
#if USE_STOPWATCH
                sw.Stop();
                UnityEngine.Debug.Log("Path found in " + sw.ElapsedMilliseconds + " ms");
#endif
                    pathSuccess = true;
                    break;
                }

                foreach (Node neighbour in grid.GetNeighbours(currentNode))
                {
                    if (!neighbour.isWalkable || closedSet.Contains(neighbour))
                    {
                        continue; // Skip if not walkable or already evaluated
                    }

                    int newCostToNeighbour = currentNode.gCost + GetDistance(currentNode, neighbour) + neighbour.movementPenalty;
                    if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                    {
                        neighbour.gCost = newCostToNeighbour;
                        neighbour.hCost = GetDistance(neighbour, endNode);

                        neighbour.parent = currentNode;
                        if (!openSet.Contains(neighbour))
                        {
                            openSet.Add(neighbour);
                        }
                        else
                        {
                            openSet.UpdateItem(neighbour); // Update the node in the heap if it already exists
                        }
                    }
                }
            }
        }
        if (pathSuccess)
        {
            waypoints = RetracePath(startNode, endNode);
            pathSuccess = waypoints.Length > 0;
        }

        callback(new PathResult(waypoints, pathSuccess, request.callback));
    }

    Vector3[] RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        Vector3[] waypoints = SimplifyPath(path);
        Array.Reverse(waypoints); // Reverse the path to get it from start to end
        return waypoints;
    }

    // Only record waypoint that has a significant change in direction
    Vector3[] SimplifyPath(List<Node> path)
    {
        List<Vector3> waypoints = new List<Vector3>();
        Vector2 directionOld = Vector2.zero;
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 directionNew = new Vector2(path[i].gridPosition.x - path[i - 1].gridPosition.x, path[i].gridPosition.y - path[i - 1].gridPosition.y);
            if (directionNew != directionOld)
            {
                waypoints.Add(path[i].wolrdPosition);
            }
            directionOld = directionNew;
        }

        return waypoints.ToArray();
    }

    int GetDistance(Node a, Node b)
    {
        int dstX = Mathf.Abs(a.gridPosition.x - b.gridPosition.x);
        int dstY = Mathf.Abs(a.gridPosition.y - b.gridPosition.y);
        // Consider 14(cost) for diagonal movement and 10 for straight movement, when x > y. Origin from dstY + (dstX - dstY) <<<diagonal movement steps +  horizontal movement steps>>>

        // When dstX > dstY, we have more horizontal steps than vertical steps, which will result in more horizontal movements. Vertical and diagonal movements are cost more.
        if (dstX > dstY)
        {
            return 14 * dstY + 10 * (dstX - dstY);
        }
        // When dstY >= dstX, we have more vertical steps than horizontal steps.
        return 14 * dstX + 10 * (dstY - dstX);
    }
}