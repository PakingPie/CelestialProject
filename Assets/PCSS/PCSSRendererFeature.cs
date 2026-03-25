using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

[DisallowMultipleRendererFeature("PCSS Soft Shadows")]
public class PCSSRendererFeature : ScriptableRendererFeature
{
    [Header("Shadow Resolution")]
    public int resolution = 4096;
    public bool customShadowResolution = false;

    [Header("Sampling")]
    [Range(1, 64)]
    public int blockerSampleCount = 16;
    [Range(1, 64)]
    public int pcfSampleCount = 16;

    [Header("Noise")]
    public Texture2D noiseTexture;

    [Header("Softness")]
    [Range(0f, 7.5f)]
    public float softness = 1f;
    [Range(0f, 5f)]
    public float softnessFalloff = 4f;

    [Header("Bias")]
    [Range(0f, 0.15f)]
    public float maxStaticGradientBias = 0.05f;
    [Range(0f, 1f)]
    public float blockerGradientBias = 0f;
    [Range(0f, 1f)]
    public float pcfGradientBias = 1f;

    [Header("Cascade Blending")]
    [Range(0f, 1f)]
    public float cascadeBlendDistance = 0.5f;

    [Header("Orthographic")]
    public bool supportOrthographicProjection;

    [Header("Shadow Copy")]
    public RenderTextureFormat format = RenderTextureFormat.RFloat;
    public FilterMode filterMode = FilterMode.Bilinear;

    [Header("Shader")]
    public Shader pcssShader;

    private PCSSPass _pcssPass;
    private Material _pcssMaterial;

    public override void Create()
    {
        if (pcssShader == null)
            pcssShader = Shader.Find("Hidden/PCSS");

        if (pcssShader == null)
            return;

        if (_pcssMaterial == null)
            _pcssMaterial = CoreUtils.CreateEngineMaterial(pcssShader);

        _pcssPass = new PCSSPass(_pcssMaterial);
        // Run before opaques: shadow map and depth prepass are both available by this point
        _pcssPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pcssPass == null || _pcssMaterial == null)
            return;

        if (renderingData.cameraData.cameraType > CameraType.SceneView)
            return;

        var mainLight = renderingData.lightData.mainLightIndex >= 0
            ? renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex]
            : default;

        if (mainLight.light == null || mainLight.light.shadows == LightShadows.None)
            return;

        if (customShadowResolution)
        {
            int res = Mathf.ClosestPowerOfTwo(resolution);
            mainLight.light.shadowCustomResolution = res;
        }

        UpdateShaderValues();

        renderer.EnqueuePass(_pcssPass);
    }

    private void UpdateShaderValues()
    {
        Shader.SetGlobalInt("Blocker_Samples", blockerSampleCount);
        Shader.SetGlobalInt("PCF_Samples", pcfSampleCount);

        Shader.SetGlobalFloat("Softness", softness / 64f / Mathf.Sqrt(QualitySettings.shadowDistance));
        Shader.SetGlobalFloat("SoftnessFalloff", Mathf.Exp(softnessFalloff));
        SetKeyword("USE_FALLOFF", softnessFalloff > Mathf.Epsilon);

        Shader.SetGlobalFloat("RECEIVER_PLANE_MIN_FRACTIONAL_ERROR", maxStaticGradientBias);
        Shader.SetGlobalFloat("Blocker_GradientBias", blockerGradientBias);
        Shader.SetGlobalFloat("PCF_GradientBias", pcfGradientBias);

        SetKeyword("USE_STATIC_BIAS", maxStaticGradientBias > 0);
        SetKeyword("USE_BLOCKER_BIAS", blockerGradientBias > 0);
        SetKeyword("USE_PCF_BIAS", pcfGradientBias > 0);

        if (noiseTexture != null)
        {
            Shader.SetGlobalVector("NoiseCoords", new Vector4(1f / noiseTexture.width, 1f / noiseTexture.height, 0f, 0f));
            Shader.SetGlobalTexture("_NoiseTexture", noiseTexture);
        }

        // Cascade scale factors for receiver plane bias
        Shader.SetGlobalVector("unity_ShadowCascadeScales", new Vector4(1f, 1f, 1f, 1f));

        int maxSamples = Mathf.Max(blockerSampleCount, pcfSampleCount);
        SetKeyword("POISSON_32", maxSamples < 33);
        SetKeyword("POISSON_64", maxSamples >= 33);
    }

    private static void SetKeyword(string keyword, bool enabled)
    {
        if (enabled)
            Shader.EnableKeyword(keyword);
        else
            Shader.DisableKeyword(keyword);
    }

    protected override void Dispose(bool disposing)
    {
        if (_pcssMaterial != null)
        {
            CoreUtils.Destroy(_pcssMaterial);
            _pcssMaterial = null;
        }
    }

    /// <summary>
    /// Runs the PCSS shader fullscreen to produce screen-space soft shadows.
    /// The shader reads URP's _MainLightShadowmapTexture directly:
    ///   - LOAD_TEXTURE2D for raw depth (blocker search)
    ///   - SAMPLE_TEXTURE2D_SHADOW with URP's comparison sampler (PCF)
    /// Sets the result as _ScreenSpaceShadowmapTexture for URP's lit shaders.
    /// </summary>
    class PCSSPass : ScriptableRenderPass
    {
        private const string ProfilerTag = "PCSS Screen Space Shadows";
        private static readonly int ScreenSpaceShadowmapID = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");

        private readonly Material _material;

        public PCSSPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler(ProfilerTag);
        }

        private class PassData
        {
            internal Material material;
            internal TextureHandle mainShadowsTexture;
            internal TextureHandle cameraDepthHandle;
            internal TextureHandle screenSpaceShadowHandle;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(ProfilerTag, out PassData passData, new ProfilingSampler(ProfilerTag)))
            {
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                // Screen-space shadow output texture
                var screenDesc = cameraData.cameraTargetDescriptor;
                screenDesc.depthBufferBits = 0;
                screenDesc.colorFormat = RenderTextureFormat.R8;
                screenDesc.msaaSamples = 1;
                passData.screenSpaceShadowHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, screenDesc, "_PCSSScreenSpaceShadow", false);

                passData.mainShadowsTexture = resourceData.mainShadowsTexture;
                passData.cameraDepthHandle = resourceData.cameraDepthTexture;
                passData.material = _material;

                // Declare texture dependencies
                builder.UseTexture(passData.screenSpaceShadowHandle, AccessFlags.ReadWrite);
                if (passData.mainShadowsTexture.IsValid())
                    builder.UseTexture(passData.mainShadowsTexture, AccessFlags.Read);
                if (passData.cameraDepthHandle.IsValid())
                    builder.UseTexture(passData.cameraDepthHandle);

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private static void ExecutePass(PassData passData, UnsafeGraphContext context)
        {
            if (!passData.mainShadowsTexture.IsValid())
                return;

            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

            // Run PCSS shader fullscreen — shader reads _MainLightShadowmapTexture directly
            Blitter.BlitCameraTexture(cmd, passData.cameraDepthHandle, passData.screenSpaceShadowHandle, passData.material, pass: 0);

            // Set result as URP's screen-space shadow texture
            cmd.SetGlobalTexture(ScreenSpaceShadowmapID, passData.screenSpaceShadowHandle);

            // Switch URP keywords so lit shaders sample our texture
            cmd.DisableShaderKeyword("_MAIN_LIGHT_SHADOWS");
            cmd.DisableShaderKeyword("_MAIN_LIGHT_SHADOWS_CASCADE");
            cmd.EnableShaderKeyword("_MAIN_LIGHT_SHADOWS_SCREEN");
        }
    }
}
