using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BoidsManager : MonoBehaviour
{
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader computeShader;
    Boid[] boids;

    public Transform target;
    public Vector2 HeightRange = new Vector2(-1.0f, 1.0f);

    void Start()
    {
        boids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
        foreach (Boid boid in boids)
        {
            boid.Initialize(settings, target); // target can be null
        }
    }

    void Update()
    {
        if (boids == null)
            return;

        int numBoids = boids.Length;

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
        boids = FindObjectsByType<Boid>(FindObjectsSortMode.None);
    }

    public void RemoveBoid(Boid boid)
    {
        List<Boid> boidList = new List<Boid>(boids);
        boidList.Remove(boid);
        boids = boidList.ToArray();
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
