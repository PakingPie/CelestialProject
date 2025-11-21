using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

public class BoidsManager : MonoBehaviour
{
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader computeShader;
    List<Boid> boids;

    public Transform target;
    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);

    void Start()
    {
        var spawners = GetComponentsInChildren<BoidSpawner>();
        boids = new List<Boid>();
        foreach (BoidSpawner spawner in spawners)
        {
            var spawnedBoids = spawner.SpawnedObjects;
            foreach (GameObject boidObj in spawnedBoids)
            {
                var boid = boidObj.GetComponent<Boid>();
                if (boid != null)
                {
                    boids.Add(boid);
                    boid.Initialize(settings, target); // target can be null
                    boid.transform.gameObject.GetComponent<VehicleBase>().BoidManager = this;
                }
            }
        }

        // foreach (Boid boid in boids)
        // {
        //     boid.Initialize(settings, target); // target can be null
        //     boid.transform.gameObject.GetComponent<VehicleBase>().BoidManager = this;
        // }
    }

    void Update()
    {
        if (boids == null)
            return;

        int numBoids = boids.Count;

        if (numBoids <= 0)
            return;
            
        var boidData = new BoidData[numBoids];
        for (int i = 0; i < numBoids; i++)
        {
            boidData[i].position = boids[i].position;
            boidData[i].direction = boids[i].forward;
        }

        var boidBuffer = new ComputeBuffer(numBoids, BoidData.Size);
        boidBuffer.SetData(boidData);

        computeShader.SetBuffer(0, "boids", boidBuffer);
        computeShader.SetInt("numBoids", numBoids);
        computeShader.SetFloat("viewRadius", settings.perceptionRadius);
        computeShader.SetFloat("avoidRadius", settings.avoidanceRadius);
        computeShader.SetVector("heightRange", HeightRange);
        int threadGroups = Mathf.CeilToInt(numBoids / (float)threadGroupSize);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        boidBuffer.GetData(boidData);

        for (int i = 0; i < numBoids; i++)
        {
            boids[i].avgFlockHeading = boidData[i].flockHeading;
            boids[i].avgAvoidanceHeading = boidData[i].seperationHeading;
            boids[i].flockmatesCenter = boidData[i].flockCenter;
            boids[i].numPerceivedFlockmates = boidData[i].numFlockmates;

            boids[i].UpdateBoid();
        }

        boidBuffer.Release();
    }


    public void UpdateBoidList()
    {
        var spawners = GetComponentsInChildren<BoidSpawner>();
        boids = new List<Boid>();
        foreach (BoidSpawner spawner in spawners)
        {
            var spawnedBoids = spawner.SpawnedObjects;
            foreach (GameObject boidObj in spawnedBoids)
            {
                var boid = boidObj.GetComponent<Boid>();
                if (boid != null)
                {
                    boids.Add(boid);
                    boid.Initialize(settings, target); // target can be null
                    boid.transform.gameObject.GetComponent<VehicleBase>().BoidManager = this;
                }
            }
        }
    }

    public void RemoveBoid(Boid boid)
    {
        boids.Remove(boid);
    }

    public struct BoidData
    {
        public Vector3 position;
        public Vector3 direction;
        public Vector3 flockHeading;
        public Vector3 flockCenter;
        public Vector3 seperationHeading;
        public int numFlockmates;

        public static int Size => sizeof(float) * 3 * 5 + sizeof(int); // 5 Vector3 + 1 int
    }
}
