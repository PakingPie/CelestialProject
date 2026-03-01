using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class AtmosphericLightsManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Renderer component on the atmosphere sphere")]
    public Renderer atmosphereRenderer;

    [Tooltip("The Renderer component on the volumetric cloud sphere")]
    public Renderer cloudRenderer;

    [Header("Collection")]
    [Tooltip("Only collect lights on these layers")]
    public LayerMask lightLayerMask = -1;

    [Range(1, 16)]
    [Tooltip("Maximum lights sent to the shader")]
    public int maxLights = 16;

    [Header("Debug")]
    [SerializeField] private int _activeLightCount;

    // Must match the HLSL struct exactly — 4 × float4 = 64 bytes
    struct ShaderLightData
    {
        public Vector4 positionAndRange;
        public Vector4 colorAndType;
        public Vector4 directionAndAngles;
        public Vector4 extraParams;

        public static int Stride => sizeof(float) * 16;
    }

    private ComputeBuffer _lightBuffer;
    private ShaderLightData[] _lightDataArray;
    private MaterialPropertyBlock _atmoBlock;
    private MaterialPropertyBlock _cloudBlock;
    private readonly List<(Light light, float score)> _candidates = new();

    private static readonly int ID_Buffer = Shader.PropertyToID("_AdditionalLights");
    private static readonly int ID_Count  = Shader.PropertyToID("_AdditionalLightCount");

    // ───────────────────────────────────────────────
    // Lifecycle
    // ───────────────────────────────────────────────

    private void OnEnable()  => EnsureResources();
    private void OnDisable() => ReleaseResources();
    private void OnDestroy() => ReleaseResources();

    private void LateUpdate()
    {
        bool hasAtmo  = atmosphereRenderer != null;
        bool hasCloud = cloudRenderer != null;
        if (!hasAtmo && !hasCloud) return;

        EnsureResources();

        // ---- Determine culling volume (world space) ----
        Vector3 planetCenter  = Vector3.zero;
        float   planetRadius  = 0f;
        float   cullingRadius = 0f;

        // Atmosphere renderer: authoritative source for planet geometry
        if (hasAtmo)
        {
            Material mat = atmosphereRenderer.sharedMaterial;
            if (mat != null)
            {
                planetCenter  = atmosphereRenderer.transform.position;
                planetRadius  = mat.GetFloat("_PlanetRadius");
                cullingRadius = planetRadius + mat.GetFloat("_AtmosphereHeight");
            }
        }

        // Cloud renderer: derive world-space bounds from object-space radii × scale
        if (hasCloud)
        {
            Material cmat = cloudRenderer.sharedMaterial;
            if (cmat != null)
            {
                float outerR   = cmat.GetFloat("_OuterRadius");
                float innerR   = cmat.GetFloat("_InnerRadius");
                Vector3 scale  = cloudRenderer.transform.lossyScale;
                float maxScale = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));

                float cloudWorldOuter = outerR * maxScale;
                float cloudWorldInner = innerR * maxScale;

                if (!hasAtmo || cullingRadius < 0.001f)
                {
                    // No atmosphere — use cloud bounds for everything
                    planetCenter  = cloudRenderer.transform.position;
                    planetRadius  = cloudWorldInner;
                    cullingRadius = cloudWorldOuter;
                }
                else
                {
                    // Expand culling to encompass clouds if they extend further
                    cullingRadius = Mathf.Max(cullingRadius, cloudWorldOuter);
                }
            }
        }

        if (cullingRadius < 0.001f) return;

        CollectAndSort(planetCenter, planetRadius, cullingRadius);
        PackBuffer();
        UploadToGPU();
    }

    // ───────────────────────────────────────────────
    // Resource management
    // ───────────────────────────────────────────────

    private void EnsureResources()
    {
        int count = Mathf.Max(1, maxLights);

        if (_lightBuffer == null || _lightBuffer.count != count)
        {
            ReleaseResources();
            _lightBuffer    = new ComputeBuffer(count, ShaderLightData.Stride);
            _lightDataArray = new ShaderLightData[count];
        }

        _atmoBlock  ??= new MaterialPropertyBlock();
        _cloudBlock ??= new MaterialPropertyBlock();
    }

    private void ReleaseResources()
    {
        if (_lightBuffer != null)
        {
            _lightBuffer.Release();
            _lightBuffer = null;
        }
    }

    // ───────────────────────────────────────────────
    // Light collection
    // ───────────────────────────────────────────────

    private void CollectAndSort(Vector3 planetCenter, float planetRadius, float cullingRadius)
    {
        _candidates.Clear();

#if UNITY_2022_2_OR_NEWER
        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        Light[] allLights = FindObjectsOfType<Light>();
#endif

        foreach (Light light in allLights)
        {
            if (!IsCandidate(light, planetCenter, cullingRadius))
                continue;

            float dist  = Mathf.Max(
                Vector3.Distance(light.transform.position, planetCenter) - planetRadius, 1f);
            float score = light.intensity / (dist * dist + 1f);

            _candidates.Add((light, score));
        }

        _candidates.Sort((a, b) => b.score.CompareTo(a.score));
    }

    private bool IsCandidate(Light light, Vector3 planetCenter, float cullingRadius)
    {
        if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
            return false;

        if (light.type is LightType.Directional or LightType.Rectangle or LightType.Disc)
            return false;

        if ((lightLayerMask & (1 << light.gameObject.layer)) == 0)
            return false;

        float distToCenter = Vector3.Distance(light.transform.position, planetCenter);
        if (distToCenter - light.range > cullingRadius)
            return false;

        return true;
    }

    // ───────────────────────────────────────────────
    // Buffer packing
    // ───────────────────────────────────────────────

    private void PackBuffer()
    {
        _activeLightCount = Mathf.Min(_candidates.Count, maxLights);

        for (int i = 0; i < _activeLightCount; i++)
        {
            Light l = _candidates[i].light;
            Vector3 pos = l.transform.position;
            Color linear = l.color.linear;

            _lightDataArray[i] = new ShaderLightData
            {
                positionAndRange = new Vector4(pos.x, pos.y, pos.z, l.range),

                colorAndType = new Vector4(
                    linear.r * l.intensity,
                    linear.g * l.intensity,
                    linear.b * l.intensity,
                    l.type == LightType.Spot ? 1f : 0f),

                directionAndAngles = new Vector4(
                    l.transform.forward.x,
                    l.transform.forward.y,
                    l.transform.forward.z,
                    Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad)),

                extraParams = new Vector4(
                    Mathf.Cos(l.innerSpotAngle * 0.5f * Mathf.Deg2Rad),
                    0f, 0f, 0f)
            };
        }

        for (int i = _activeLightCount; i < _lightDataArray.Length; i++)
            _lightDataArray[i] = default;
    }

    // ───────────────────────────────────────────────
    // GPU upload — send the same buffer to both renderers
    // ───────────────────────────────────────────────

    private void UploadToGPU()
    {
        _lightBuffer.SetData(_lightDataArray);

        if (atmosphereRenderer != null)
        {
            atmosphereRenderer.GetPropertyBlock(_atmoBlock);
            _atmoBlock.SetBuffer(ID_Buffer, _lightBuffer);
            _atmoBlock.SetInt(ID_Count, _activeLightCount);
            atmosphereRenderer.SetPropertyBlock(_atmoBlock);
        }

        if (cloudRenderer != null)
        {
            cloudRenderer.GetPropertyBlock(_cloudBlock);
            _cloudBlock.SetBuffer(ID_Buffer, _lightBuffer);
            _cloudBlock.SetInt(ID_Count, _activeLightCount);
            cloudRenderer.SetPropertyBlock(_cloudBlock);
        }
    }
}