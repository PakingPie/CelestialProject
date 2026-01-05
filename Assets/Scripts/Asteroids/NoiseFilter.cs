using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NoiseFilter
{
    public float Strength = 1;
    [Range(1, 8)]
    public int NumLayers = 1;
    public float BaseRoughness = 1;
    public float Roughness = 2;
    public float Persistence = .5f;
    public Vector3 Centre;
    public float MinValue;

    Noise noise = new Noise();

    public NoiseFilter()
    {
    }

    public NoiseFilter(float strength, int numLayers, float baseRoughness, float roughness, float persistence, Vector3 centre, float minValue)
    {
        Strength = strength;
        NumLayers = numLayers;
        BaseRoughness = baseRoughness;
        Roughness = roughness;
        Persistence = persistence;
        Centre = centre;
        MinValue = minValue;
    }

    public float Evaluate(Vector3 point)
    {
        float noiseValue = 0;
        float frequency = BaseRoughness;
        float amplitude = 1;

        for (int i = 0; i < NumLayers; i++)
        {
            float v = noise.Evaluate(point * frequency + Centre);
            noiseValue += (v + 1) * .5f * amplitude;
            frequency *= Roughness;
            amplitude *= Persistence;
        }

        noiseValue = Mathf.Max(0, noiseValue - MinValue);
        return noiseValue * Strength;
    }
}