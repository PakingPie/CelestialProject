// using UnityEngine;
// using Unity.Collections;
// using Unity.Jobs;
// using System.Collections.Generic;

// /// <summary>
// /// Manages obstacle detection for boids using batched raycasting with Unity's Job system.
// /// 
// /// This system spreads collision detection across multiple frames by dividing boids into batches.
// /// Instead of raycasting all boids every frame, it processes one batch per frame, reducing
// /// performance overhead while maintaining collision awareness.
// /// 
// /// Example: 512 boids with 4 batches = ~128 raycasts per frame instead of 512.
// /// </summary>
// public class BoidObstacleSystem : MonoBehaviour
// {
//     private static BoidObstacleSystem _instance;
//     public static BoidObstacleSystem Instance => _instance;

//     /// <summary>Maximum number of boids this system can track.</summary>
//     [SerializeField] private int _maxBoids = 512;
    
//     /// <summary>Number of batches to divide boids into for frame distribution.</summary>
//     [SerializeField] private int _batchCount = 4;

//     /// <summary>List of all registered boids being tracked for collision detection.</summary>
//     private List<Boid> _boids = new List<Boid>();

//     /// <summary>Native array of raycast commands scheduled as a job. Reused each frame for efficiency.</summary>
//     private NativeArray<RaycastCommand> _commands;
    
//     /// <summary>Native array of raycast results from the most recent job. Contains collision hit data.</summary>
//     private NativeArray<RaycastHit> _results;
    
//     /// <summary>Handle to the scheduled raycast job. Used to wait for completion before processing results.</summary>
//     private JobHandle _jobHandle;

//     /// <summary>Which batch (0 to _batchCount-1) is currently being processed. Increments each frame.</summary>
//     private int _currentBatch;
    
//     /// <summary>Number of boids processed in the most recent batch (for iteration in ProcessResults).</summary>
//     private int _lastProcessedCount;
    
//     /// <summary>Starting index in the _boids list of the most recent batch (for result mapping).</summary>
//     private int _lastStartIdx;

//     /// <summary>Initialize the singleton instance and allocate Native arrays for raycasting.</summary>
//     void Awake()
//     {
//         _instance = this;

//         // Pre-allocate Native arrays large enough to hold one batch of raycasts
//         int maxPerBatch = Mathf.CeilToInt((float)_maxBoids / _batchCount);
//         _commands = new NativeArray<RaycastCommand>(maxPerBatch, Allocator.Persistent);
//         _results = new NativeArray<RaycastHit>(maxPerBatch, Allocator.Persistent);
//     }

//     /// <summary>Cleanup: Wait for any pending jobs and free Native array memory.</summary>
//     void OnDestroy()
//     {
//         // Ensure the job completes before we dispose the arrays
//         _jobHandle.Complete();

//         // Free Native array memory
//         if (_commands.IsCreated) _commands.Dispose();
//         if (_results.IsCreated) _results.Dispose();
//     }

//     /// <summary>Add a boid to the collision detection system.</summary>
//     public void RegisterBoid(Boid boid)
//     {
//         // Only add if we haven't hit max capacity and the boid isn't already registered
//         if (_boids.Count < _maxBoids && !_boids.Contains(boid))
//             _boids.Add(boid);
//     }

//     /// <summary>Remove a boid from the collision detection system.</summary>
//     public void UnregisterBoid(Boid boid)
//     {
//         _boids.Remove(boid);
//     }

//     /// <summary>Main update loop: wait for previous frame's job, process results, then schedule next batch.</summary>
//     void LateUpdate()
//     {
//         // Block until the raycast job from last frame completes
//         _jobHandle.Complete();
        
//         // Update each boid with its collision detection result
//         ProcessResults();
        
//         // Clean up any destroyed boids from the list
//         RemoveNullBoids();

//         // Exit early if no boids to process
//         if (_boids.Count == 0) return;

//         // Schedule raycasts for the next batch of boids
//         ScheduleNextBatch();
//     }

//     /// <summary>Remove any null (destroyed) boids from the list using swap-removal to avoid gaps.</summary>
//     private void RemoveNullBoids()
//     {
//         // Compact the list by moving valid boids to the front (two-pointer technique)
//         int writeIdx = 0;
//         for (int i = 0; i < _boids.Count; i++)
//         {
//             if (_boids[i] != null)
//             {
//                 if (writeIdx != i)
//                     _boids[writeIdx] = _boids[i];
//                 writeIdx++;
//             }
//         }

//         // Remove nulls from the end
//         while (_boids.Count > writeIdx)
//             _boids.RemoveAt(_boids.Count - 1);
//     }

//     /// <summary>Create raycast commands for the next batch of boids and schedule them as a parallel job.</summary>
//     private void ScheduleNextBatch()
//     {
//         // Calculate which subset of boids to process this frame
//         int totalBoids = _boids.Count;
//         int boidsPerBatch = Mathf.CeilToInt((float)totalBoids / _batchCount);
//         int startIdx = _currentBatch * boidsPerBatch;
//         int endIdx = Mathf.Min(startIdx + boidsPerBatch, totalBoids);
//         int batchBoidCount = endIdx - startIdx;

//         // If this batch is empty, skip to next batch
//         if (batchBoidCount <= 0)
//         {
//             _currentBatch = (_currentBatch + 1) % _batchCount;
//             _lastProcessedCount = 0;
//             return;
//         }

//         // Cap batch size to array capacity
//         batchBoidCount = Mathf.Min(batchBoidCount, _commands.Length);

//         // Create raycast commands for each boid in this batch
//         for (int i = 0; i < batchBoidCount; i++)
//         {
//             Boid boid = _boids[startIdx + i];
//             if (boid != null)
//             {
//                 // Set up physics query parameters specific to this boid
//                 var queryParams = new QueryParameters
//                 {
//                     layerMask = boid.GetObstacleMask(),           // Only detect obstacles on specific layers
//                     hitMultipleFaces = false,                     // Single hit is enough
//                     hitTriggers = QueryTriggerInteraction.Ignore, // Ignore trigger colliders
//                     hitBackfaces = false                          // Don't raycast through backfaces
//                 };

//                 // Create a raycast in front of the boid to detect upcoming obstacles
//                 _commands[i] = new RaycastCommand(
//                     boid.position,
//                     boid.forward,
//                     queryParams,
//                     boid.GetCollisionDistance()  // How far ahead to raycast
//                 );
//             }
//         }

//         // Store batch metadata for result processing next frame
//         _lastStartIdx = startIdx;
//         _lastProcessedCount = batchBoidCount;

//         // Schedule the raycasts as a parallel job (runs on worker threads, completes by next frame)
//         // Batch size of 32 balances job scheduling overhead with parallelism
//         _jobHandle = RaycastCommand.ScheduleBatch(_commands, _results, 32);

//         // Move to next batch for next frame
//         _currentBatch = (_currentBatch + 1) % _batchCount;
//     }

//     /// <summary>Take the raycast results from the previous frame's job and update each boid's collision state.</summary>
//     private void ProcessResults()
//     {
//         // Exit early if no results to process
//         if (_lastProcessedCount == 0) return;

//         // Iterate through the batch that was processed last frame
//         for (int i = 0; i < _lastProcessedCount; i++)
//         {
//             int boidIdx = _lastStartIdx + i;
//             if (boidIdx >= _boids.Count) continue;

//             Boid boid = _boids[boidIdx];
//             if (boid == null) continue;

//             // Check if the raycast hit anything (collider != null means collision detected)
//             bool isColliding = _results[i].collider != null;
            
//             // Tell the boid whether it's about to hit an obstacle
//             boid.SetCollisionState(isColliding);
//         }
//     }
// }