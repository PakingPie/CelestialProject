using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System.Collections.Generic;

public class BoidObstacleSystem : MonoBehaviour
{
    private static BoidObstacleSystem _instance;
    public static BoidObstacleSystem Instance => _instance;

    [SerializeField] private int _maxBoids = 512;
    [SerializeField] private int _batchCount = 4;

    private List<Boid> _boids = new List<Boid>();

    private NativeArray<RaycastCommand> _commands;
    private NativeArray<RaycastHit> _results;
    private JobHandle _jobHandle;

    private int _currentBatch;
    private int _lastProcessedCount;
    private int _lastStartIdx;

    void Awake()
    {
        _instance = this;

        int maxPerBatch = Mathf.CeilToInt((float)_maxBoids / _batchCount);
        _commands = new NativeArray<RaycastCommand>(maxPerBatch, Allocator.Persistent);
        _results = new NativeArray<RaycastHit>(maxPerBatch, Allocator.Persistent);
    }

    void OnDestroy()
    {
        _jobHandle.Complete();

        if (_commands.IsCreated) _commands.Dispose();
        if (_results.IsCreated) _results.Dispose();
    }

    public void RegisterBoid(Boid boid)
    {
        if (_boids.Count < _maxBoids && !_boids.Contains(boid))
            _boids.Add(boid);
    }

    public void UnregisterBoid(Boid boid)
    {
        _boids.Remove(boid);
    }

    void LateUpdate()
    {
        _jobHandle.Complete();
        ProcessResults();
        RemoveNullBoids();

        if (_boids.Count == 0) return;

        ScheduleNextBatch();
    }

    private void RemoveNullBoids()
    {
        int writeIdx = 0;
        for (int i = 0; i < _boids.Count; i++)
        {
            if (_boids[i] != null)
            {
                if (writeIdx != i)
                    _boids[writeIdx] = _boids[i];
                writeIdx++;
            }
        }

        while (_boids.Count > writeIdx)
            _boids.RemoveAt(_boids.Count - 1);
    }

    private void ScheduleNextBatch()
    {
        int totalBoids = _boids.Count;
        int boidsPerBatch = Mathf.CeilToInt((float)totalBoids / _batchCount);
        int startIdx = _currentBatch * boidsPerBatch;
        int endIdx = Mathf.Min(startIdx + boidsPerBatch, totalBoids);
        int batchBoidCount = endIdx - startIdx;

        if (batchBoidCount <= 0)
        {
            _currentBatch = (_currentBatch + 1) % _batchCount;
            _lastProcessedCount = 0;
            return;
        }

        batchBoidCount = Mathf.Min(batchBoidCount, _commands.Length);

        for (int i = 0; i < batchBoidCount; i++)
        {
            Boid boid = _boids[startIdx + i];
            if (boid != null)
            {
                // New API using QueryParameters
                var queryParams = new QueryParameters
                {
                    layerMask = boid.GetObstacleMask(),
                    hitMultipleFaces = false,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false
                };

                _commands[i] = new RaycastCommand(
                    boid.position,
                    boid.forward,
                    queryParams,
                    boid.GetCollisionDistance()
                );
            }
        }

        _lastStartIdx = startIdx;
        _lastProcessedCount = batchBoidCount;

        _jobHandle = RaycastCommand.ScheduleBatch(_commands, _results, 32);

        _currentBatch = (_currentBatch + 1) % _batchCount;
    }

    private void ProcessResults()
    {
        if (_lastProcessedCount == 0) return;

        for (int i = 0; i < _lastProcessedCount; i++)
        {
            int boidIdx = _lastStartIdx + i;
            if (boidIdx >= _boids.Count) continue;

            Boid boid = _boids[boidIdx];
            if (boid == null) continue;

            bool isColliding = _results[i].collider != null;
            boid.SetCollisionState(isColliding);
        }
    }
}