using UnityEngine;

public class AsteroidShapeGenerator
{
    public float AsteroidRadius { get; private set; }
    private readonly NoiseFilter _noiseFilter;

    public AsteroidShapeGenerator(float asteroidRadius, float strength, int numLayers, 
        float baseRoughness, float roughness, float persistence)
    {
        AsteroidRadius = asteroidRadius;
        _noiseFilter = new NoiseFilter(strength, numLayers, baseRoughness, roughness, persistence, Vector3.zero, 0f);
    }

    public Vector3 CalculatePointOnAsteroid(Vector3 pointOnUnitSphere)
    {
        float elevation = 1f + _noiseFilter.Evaluate(pointOnUnitSphere);
        return pointOnUnitSphere * AsteroidRadius * elevation;
    }
}