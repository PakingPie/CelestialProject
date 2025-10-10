using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CelestialBodyGenerator : MonoBehaviour
{
    public enum PreviewMode { LOD0, LOD1, LOD2, CollisionRes }
    public ResolutionSettings resolutionSettings;
    public PreviewMode previewMode;
    // public CelestialBodySettings body;
    // Private variables

    Mesh previewMesh;
    Mesh collisionMesh;
    Mesh[] lodMeshes;

    ComputeBuffer vertexBuffer;


    [System.Serializable]
    public class ResolutionSettings
    {
        public const int numLODLevels = 3;
        const int maxAllowedResolution = 500;

        public int lod0 = 300;
        public int lod1 = 100;
        public int lod2 = 50;
        public int collider = 100;

        public int GetLODResolution(int lodLevel)
        {
            switch (lodLevel)
            {
                case 0:
                    return lod0;
                case 1:
                    return lod1;
                case 2:
                    return lod2;
            }
            return lod2;
        }

        public void ClampResolutions()
        {
            lod0 = Mathf.Min(maxAllowedResolution, lod0);
            lod1 = Mathf.Min(maxAllowedResolution, lod1);
            lod2 = Mathf.Min(maxAllowedResolution, lod2);
            collider = Mathf.Min(maxAllowedResolution, collider);
        }
    }

}