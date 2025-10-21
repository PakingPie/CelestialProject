using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

public class Node : IHeapItem<Node>
{
    public int gCost;
    public int hCost;
    public int fCost
    {
        get { return gCost + hCost; }
    }
    public bool isWalkable;
    public Vector3 wolrdPosition;
    public Vector2Int gridPosition;
    public int movementPenalty;
    public Node parent;


    int heapIndex;

    public Node(bool isWalkable, Vector3 position, Vector2Int gridPosition, int penalty)
    {
        this.isWalkable = isWalkable;
        this.wolrdPosition = position;
        this.gridPosition = gridPosition;
        this.movementPenalty = penalty;
    }

    public int HeapIndex
    {
        get { return heapIndex; }
        set { heapIndex = value; }
    }

    public int CompareTo(Node nodeToCompare)
    {
        int compare = fCost.CompareTo(nodeToCompare.fCost);
        if (compare == 0)
        {
            compare = hCost.CompareTo(nodeToCompare.hCost);
        }
        return -compare;
    }
}

public class PathFinderPath
{
    public readonly Vector3[] lookPoints;
    public readonly PathFinderLine[] turnBoundaries;
    public readonly int finishLineIndex;
    public readonly int slowDownIndex;

    public PathFinderPath(Vector3[] waypoints, Vector3 startPos, float turnDist, float stoppingDist)
    {
        lookPoints = waypoints;
        turnBoundaries = new PathFinderLine[lookPoints.Length];
        finishLineIndex = turnBoundaries.Length - 1;

        Vector2 previousPoint = new Vector2(startPos.x, startPos.z);

        for (int i = 0; i < lookPoints.Length; i++)
        {
            Vector2 currentPoint = new Vector2(lookPoints[i].x, lookPoints[i].z);
            Vector2 dirToCurrentPoint = (currentPoint - previousPoint).normalized;
            Vector2 turnBoundryPoint = (i == finishLineIndex) ? currentPoint : currentPoint - dirToCurrentPoint * turnDist;
            turnBoundaries[i] = new PathFinderLine(turnBoundryPoint, previousPoint - dirToCurrentPoint * turnDist);
            previousPoint = turnBoundryPoint;
        }

        float distFromEndPoint = 0;
        for (int i = lookPoints.Length - 1; i > 0; i--)
        {
            distFromEndPoint += Vector3.Distance(lookPoints[i], lookPoints[i - 1]);
            if (distFromEndPoint > stoppingDist)
            {
                slowDownIndex = i; break;
            }
        }
    }

    public void DrawWithGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Vector3 p in lookPoints)
        {
            Gizmos.DrawCube(p + Vector3.up, Vector3.one * 0.4f);
        }
        Gizmos.color = Color.green;
        foreach (PathFinderLine line in turnBoundaries)
        {
            line.DrawWithGizmos(10);
        }
    }
}

public struct PathFinderLine
{
    const float verticalLineGradient = 1e5f;
    float gradient;
    float y_intercept;
    Vector2 pointOnLine_1;
    Vector2 pointOnLine_2;

    float gradietPerpendicular;

    bool approachSide; // True if the point is on the side of the line that we are approaching from

    // Use linear equation y = ax + b to represent the line
    public PathFinderLine(Vector2 pointOnLine, Vector2 pointPerpendicularToLine)
    {
        float dx = pointOnLine.x - pointPerpendicularToLine.x;
        float dy = pointOnLine.y - pointPerpendicularToLine.y;

        // Calculare (x2-x1) and (y2-y1)
        if (dx == 0)
        {
            gradietPerpendicular = verticalLineGradient;
        }
        else
        {
            gradietPerpendicular = dy / dx;
        }

        // gradient is the slope of the line perpendicular to the line defined by pointOnLine and pointPerpendicularToLine(slope: a in y = ax + b)
        if (gradietPerpendicular == 0)
        {
            gradient = verticalLineGradient;
        }
        else // Convert (x2 - x1)/(y2-y1) to (x1-x2)/(y2-y1)
        {
            gradient = -1 / gradietPerpendicular;
        }
        // y = ax + b 
        // b = y - ax
        y_intercept = pointOnLine.y - gradient * pointOnLine.x;

        pointOnLine_1 = pointOnLine;
        pointOnLine_2 = pointOnLine + new Vector2(1, gradient); // Get a second point on the line to draw it

        approachSide = false;
        approachSide = GetSide(pointPerpendicularToLine);
    }

    bool GetSide(Vector2 p)
    {
        // Check which side of the line the point p is on
        return (p.x - pointOnLine_1.x) * (pointOnLine_2.y - pointOnLine_1.y) > (p.y - pointOnLine_1.y) * (pointOnLine_2.x - pointOnLine_1.x);
    }

    public float DistanceFromPoint(Vector2 p)
    {
        float yInterceptPerpendicular = p.y - gradietPerpendicular * p.x;
        float intersectX = (yInterceptPerpendicular - y_intercept) / (gradient - gradietPerpendicular);
        float intersectY = gradient * intersectX + y_intercept;
        return Vector2.Distance(p, new Vector2(intersectX, intersectY));
    }

    public bool HasCrossedLine(Vector2 p)
    {
        return GetSide(p) != approachSide;
    }

    public void DrawWithGizmos(float length)
    {
        Vector3 lineDir = new Vector3(1, 0, gradient).normalized;
        Vector3 lineCenter = new Vector3(pointOnLine_1.x, 0, pointOnLine_1.y) + Vector3.up;
        Gizmos.DrawLine(lineCenter - lineDir * length / 2, lineCenter + lineDir * length / 2);
    }
}