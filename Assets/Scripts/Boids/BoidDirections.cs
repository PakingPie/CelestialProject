using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Generate a set of evenly distributed directions in 3D space using the golden ratio
public static class BoidDirections
{
    const int numViewDirections = 300;
    public static readonly Vector3[] viewDirections;

    static BoidDirections()
    {
        viewDirections = new Vector3[numViewDirections];
        float goldenRatio = (1 + Mathf.Sqrt(5)) / 2; // Approximate value of the golden ratio
        float angleStep = Mathf.PI * 2 * goldenRatio; // Angle step based on the golden ratio
        for (int i = 0; i < numViewDirections; i++)
        {
            float t = (float)i / numViewDirections;
            float inclination = Mathf.Acos(1 - 2 * t); // Inclination angle
            float azimuth = angleStep * i; // Azimuth angle based on the golden ratio

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            viewDirections[i] = new Vector3(x, y, z).normalized; // Normalize the direction vector
        }
    }
}