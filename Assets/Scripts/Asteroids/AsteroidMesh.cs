using UnityEngine;

public static class AsteroidMeshOptimized
{
    public static void ConstructMesh(Mesh mesh, AsteroidShapeGenerator shapeGenerator, int resolution, Vector3 localUp)
    {
        Vector3 axisA = new Vector3(localUp.y, localUp.z, localUp.x);
        Vector3 axisB = Vector3.Cross(localUp, axisA);

        int vertexCount = resolution * resolution;
        int triangleCount = (resolution - 1) * (resolution - 1) * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        int[] triangles = new int[triangleCount];
        
        int triIndex = 0;
        float resolutionMinusOne = resolution - 1;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = x + y * resolution;
                
                float percentX = x / resolutionMinusOne;
                float percentY = y / resolutionMinusOne;
                
                Vector3 pointOnUnitCube = localUp 
                    + (percentX - 0.5f) * 2f * axisA 
                    + (percentY - 0.5f) * 2f * axisB;
                    
                Vector3 pointOnUnitSphere = pointOnUnitCube.normalized;
                vertices[i] = shapeGenerator.CalculatePointOnAsteroid(pointOnUnitSphere);

                if (x < resolution - 1 && y < resolution - 1)
                {
                    triangles[triIndex]     = i;
                    triangles[triIndex + 1] = i + resolution + 1;
                    triangles[triIndex + 2] = i + resolution;
                    triangles[triIndex + 3] = i;
                    triangles[triIndex + 4] = i + 1;
                    triangles[triIndex + 5] = i + resolution + 1;
                    triIndex += 6;
                }
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}