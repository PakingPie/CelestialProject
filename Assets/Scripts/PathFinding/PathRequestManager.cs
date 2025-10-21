using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading;

public class PathRequestManager : MonoBehaviour
{
    Queue<PathResult> results = new Queue<PathResult>();
    static PathRequestManager instance;
    PathFinding pathFinding;

    void Awake()
    {
        instance = this;
        pathFinding = GetComponent<PathFinding>();
    }

    void Update()
    {
        if (results.Count > 0)
        {
            int itemInQueue = results.Count;
            lock (results)
            {
                for (int i = 0; i < itemInQueue; i++)
                {
                    PathResult result = results.Dequeue();
                    result.callback(result.path, result.success);
                }
            }
        }
    }

    public static void RequestPath(PathRequest request) // (Vector3 pathStart, Vector3 pathEnd, Action<Vector3[], bool> callback)
    {
        ThreadStart threadStart = delegate
        {
            instance.pathFinding.FindPath(request, instance.FinishedProcessingPath);
        };

        threadStart.Invoke(); // Start the pathfinding in a separate thread
    }
    
    public void FinishedProcessingPath(PathResult result)
    {
        lock (results)
        {
            results.Enqueue(result);
        }
    }
}

public struct PathResult
{
    public Vector3[] path;
    public bool success;
    public Action<Vector3[], bool> callback;

    public PathResult(Vector3[] path, bool success, Action<Vector3[], bool> callback)
    {
        this.path = path;
        this.success = success;
        this.callback = callback;
    }
}

public struct PathRequest
{
    public Vector3 start;
    public Vector3 end;
    public Action<Vector3[], bool> callback;

    public PathRequest(Vector3 start, Vector3 end, Action<Vector3[], bool> callback)
    {
        this.start = start;
        this.end = end;
        this.callback = callback;
    }
}