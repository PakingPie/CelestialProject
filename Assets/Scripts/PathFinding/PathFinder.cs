using UnityEngine;
using System.Collections;
using System.IO;

public class PathFinder : MonoBehaviour
{
    const float minPathUpdateTime = 0.2f; // Minimum time between path updates
    const float pathUpdateMoveThreshold = 0.5f;
    public Transform target;
    public float speed = 20;
    public float turnSpeed = 3;
    public float turnDistance = 5;
    public float stoppingDistance = 10f; // Distance to stop before reaching the target
    // Vector3[] path;
    // int targetIndex;
    PathFinderPath path;

    void Start()
    {
        StartCoroutine("UpdatePath");
    }

    public void OnPathFound(Vector3[] waypoints, bool pathSuccessful)
    {
        if (pathSuccessful)
        {
            path = new PathFinderPath(waypoints, transform.position, turnDistance, stoppingDistance);
            // targetIndex = 0;
            StopCoroutine("FollowPath");
            StartCoroutine("FollowPath");
        }
    }

    IEnumerator UpdatePath()
    {
        if (Time.timeSinceLevelLoad < 0.3f) // Avoid large delta times on start game
        {
            yield return new WaitForSeconds(0.3f);
        }
        PathRequestManager.RequestPath(new PathRequest(transform.position, target.position, OnPathFound));

        float sqrMoveThreshold = pathUpdateMoveThreshold * pathUpdateMoveThreshold;
        Vector3 oldTargetPos = target.position;
        // print("Transform position: " + transform.position + ", Target position: " + target.position);
        while (true)
        {
            yield return new WaitForSeconds(minPathUpdateTime);
            if ((target.position - oldTargetPos).sqrMagnitude > sqrMoveThreshold)
            {
                PathRequestManager.RequestPath(new PathRequest(transform.position, target.position, OnPathFound));
                oldTargetPos = target.position;
            }
        }
    }

    IEnumerator FollowPath()
    {
        // Vector3 currentWaypoint = path[0];
        bool followingPath = true;
        int pathIndex = 0;
        transform.LookAt(path.lookPoints[0]);

        float speedPercent = 1;

        while (followingPath)
        {
            Vector2 pos2D = new Vector2(transform.position.x, transform.position.z);
            while (path.turnBoundaries[pathIndex].HasCrossedLine(pos2D))
            {
                if (pathIndex == path.finishLineIndex)
                {
                    followingPath = false;
                    break;
                }
                else
                {
                    pathIndex++;
                }
            }

            if (followingPath)
            {
                if (pathIndex >= path.slowDownIndex && stoppingDistance > 0)
                {
                    speedPercent = Mathf.Clamp01(path.turnBoundaries[path.finishLineIndex].DistanceFromPoint(pos2D) / stoppingDistance);
                    if(speedPercent < 0.01f)
                    {
                        followingPath = false; // Stop following the path if we are too close to the end
                        speedPercent = 0;
                    }
                }
                Quaternion targetRotation = Quaternion.LookRotation(path.lookPoints[pathIndex] - transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
                transform.Translate(Vector3.forward * speed * Time.deltaTime * speedPercent, Space.Self);
            }
            // if (transform.position == currentWaypoint)
            // {
            //     targetIndex++;
            //     if (targetIndex >= path.Length)
            //     {
            //         yield break; // Reached the end of the path
            //     }
            //     currentWaypoint = path[targetIndex];
            // }

            // transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);
            yield return null; // Wait for the next frame
        }
    }

    void OnDrawGizmos()
    {
        if (path != null)
        {
            path.DrawWithGizmos();
            // for (int i = targetIndex; i < path.Length; i++)
            // {
            //     Gizmos.color = Color.green;
            //     Gizmos.DrawCube(path[i], Vector3.one * 0.2f);

            //     if (i == targetIndex)
            //     {
            //         Gizmos.DrawLine(transform.position, path[i]);
            //     }
            //     else
            //     {
            //         Gizmos.DrawLine(path[i - 1], path[i]);
            //     }
            // }
        }
    }

}