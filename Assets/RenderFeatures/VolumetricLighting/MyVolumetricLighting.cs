using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;

[Tooltip("Adds support to render my volumetric lighting.")]
[DisallowMultipleRendererFeature("My Volumetric Lighting")]
public class MyVolumetricLighting : ScriptableRendererFeature
{
    // private Shader _shader;
    [SerializeField] private Material _material;
    private const string _shaderName = "Unlit/MyVolumetricLighting";
    private MyVolumetricLightingRenderPass _volumetricLightingRenderPass;

    public override void Create()
    {
        if (_material == null || _material.shader != Shader.Find(_shaderName))
        {
            return;
        }
        if (_volumetricLightingRenderPass == null)
        {
            _volumetricLightingRenderPass = new MyVolumetricLightingRenderPass(_material);
            _volumetricLightingRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents + 1;
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_volumetricLightingRenderPass == null || _material == null)
        {
            return;
        }

        bool isPostProcessEnabled = renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled;
        MyVolumetricLightingVolume volume = VolumeManager.instance.stack.GetComponent<MyVolumetricLightingVolume>();

        bool validCameraState = isPostProcessEnabled && volume.IsActive();

        if (validCameraState)
        {
            renderer.EnqueuePass(_volumetricLightingRenderPass);
        }
    }

    // protected override void Dispose(bool disposing)
    // {
    //     Destroy(_material);
    // }

    public class MyVolumetricLightingRenderPass : ScriptableRenderPass
    {
        const string _profilerTag = "Volumetric Lighting Render Pass";
        public Material _material;

        private enum DownSampleFactor : byte
        {
            Half = 2,
        }

        private const string DownSampledCameraDepth = "_DownsampledCameraDepth";
        private const string VolumetricFog = "_VolumetricFog";
        private const string VolumetricFogBlur = "_VolumetricFogBlur";
        private const string VolumetricFogUpsampleComposition = "_VolumetricFogUpsampleComposition";

        private static readonly int DownSampledCameraDepthTextureId = Shader.PropertyToID("_DownsampledCameraDepthTexture");
        private static readonly int VolumetricFogTextureId = Shader.PropertyToID("_VolumetricFogTexture");

        private static readonly int FrameCountId = Shader.PropertyToID("_FrameCount");
        private static readonly int CustomAdditionalLightsCountId = Shader.PropertyToID("_CustomAdditionalLightsCount");
        private static readonly int DistanceId = Shader.PropertyToID("_Distance");
        private static readonly int BaseHeightId = Shader.PropertyToID("_BaseHeight");
        private static readonly int MaximumHeightId = Shader.PropertyToID("_MaximumHeight");
        private static readonly int GroundHeightId = Shader.PropertyToID("_GroundHeight");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int AbsortionId = Shader.PropertyToID("_Absortion");
        private static readonly int APVContributionWeigthId = Shader.PropertyToID("_APVContributionWeight");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MaxStepsId = Shader.PropertyToID("_MaxSteps");
        private static readonly int MaxPhaseIntensityId = Shader.PropertyToID("_MaxPhaseIntensity");
        private static readonly int DensityFalloffId = Shader.PropertyToID("_DensityFalloff");

        private static readonly int AnisotropiesArrayId = Shader.PropertyToID("_Anisotropies");
        private static readonly int ScatteringsArrayId = Shader.PropertyToID("_Scatterings");
        private static readonly int RadiiSqArrayId = Shader.PropertyToID("_RadiiSq");

        private const string _LIGHT_COOKIES = "_LIGHT_COOKIES";

        private static readonly float[] Anisotropies = new float[UniversalRenderPipeline.maxVisibleAdditionalLights + 1];
        private static readonly float[] Scatterings = new float[UniversalRenderPipeline.maxVisibleAdditionalLights + 1];
        private static readonly float[] RadiiSq = new float[UniversalRenderPipeline.maxVisibleAdditionalLights];


        public MyVolumetricLightingRenderPass(Material material)
        {
            _material = material;
        }

        private class PassData
        {
            internal Material material;
            internal UniversalLightData lightData;

            internal TextureHandle cameraColorHandle;
            internal TextureHandle cameraDepthHandle;
            internal TextureHandle downsampledCameraDepthHandle;
            internal TextureHandle volumetricFogHandle;
            internal TextureHandle volumetricFogBlurHandle;
            internal TextureHandle volumetricFogUpsampleCompositionHandle;
            // internal TextureHandle intermediateTextureHandle;
        }

        // This is where the renderGraph handle can be accessed.
        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {

            using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Volumetric Fog Blur Pass", out PassData passData, new ProfilingSampler("Volumetric Fog Blur")))
            {
                // Initialize the pass data
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = (int)DepthBits.None;

                RenderTextureFormat originalColorFormat = desc.colorFormat;
                Vector2Int originalResolution = new Vector2Int(desc.width, desc.height);

                desc.width /= (int)DownSampleFactor.Half;
                desc.height /= (int)DownSampleFactor.Half;
                desc.graphicsFormat = GraphicsFormat.R32_SFloat;
                passData.downsampledCameraDepthHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, DownSampledCameraDepth, false);

                desc.colorFormat = RenderTextureFormat.ARGBHalf;
                passData.volumetricFogHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, VolumetricFog, false);
                passData.volumetricFogBlurHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, VolumetricFogBlur, false);

                desc.width = originalResolution.x;
                desc.height = originalResolution.y;
                desc.colorFormat = originalColorFormat;
                passData.volumetricFogUpsampleCompositionHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, VolumetricFogUpsampleComposition, false);

                passData.cameraColorHandle = resourceData.cameraColor;
                passData.cameraDepthHandle = resourceData.cameraDepthTexture;

                passData.material = _material;
                passData.lightData = lightData;

                builder.UseTexture(resourceData.cameraColor);
                builder.UseTexture(resourceData.cameraDepthTexture);
                if (resourceData.mainShadowsTexture.IsValid())
                    builder.UseTexture(resourceData.mainShadowsTexture);
                if (resourceData.additionalShadowsTexture.IsValid())
                    builder.UseTexture(resourceData.additionalShadowsTexture);

                builder.UseTexture(passData.cameraColorHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.cameraDepthHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.volumetricFogHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.volumetricFogBlurHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.volumetricFogUpsampleCompositionHandle, AccessFlags.ReadWrite);
                builder.UseTexture(passData.downsampledCameraDepthHandle, AccessFlags.ReadWrite);

                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
            }
        }

        private static void ExecutePass(PassData passData, UnsafeGraphContext context)
        {
            CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            // Down Sample Depth
            Blitter.BlitCameraTexture(cmd, passData.cameraDepthHandle, passData.downsampledCameraDepthHandle, passData.material, 4);

            // Render Volumetric Fog
            passData.material.SetTexture(DownSampledCameraDepthTextureId, passData.downsampledCameraDepthHandle);
            UpdateMyVolumetricFogMaterialParameters(passData.material, passData.lightData.mainLightIndex, passData.lightData.additionalLightsCount, passData.lightData.visibleLights);

            Blitter.BlitCameraTexture(cmd, passData.downsampledCameraDepthHandle, passData.volumetricFogHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, pass: 0);

            // Blur Volumetric Fog
            int blurIterations = VolumeManager.instance.stack.GetComponent<MyVolumetricLightingVolume>().blurIterations.value;

            for (int i = 0; i < blurIterations; i++)
            {
                Blitter.BlitCameraTexture(cmd, passData.volumetricFogHandle, passData.volumetricFogBlurHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, pass: 1); // Horizontal blur
                Blitter.BlitCameraTexture(cmd, passData.volumetricFogBlurHandle, passData.volumetricFogHandle, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, pass: 2); // Vertical blur
            }

            // Upsample Volumetric Fog and Composite
            passData.material.SetTexture(VolumetricFogTextureId, passData.volumetricFogHandle);
            Blitter.BlitCameraTexture(cmd, passData.cameraColorHandle, passData.volumetricFogUpsampleCompositionHandle, passData.material, pass: 3);

            // // Copy Volumetric Fog to Camera Color
            Blitter.BlitCameraTexture(cmd, passData.volumetricFogUpsampleCompositionHandle, passData.cameraColorHandle);

            cmd.EnableShaderKeyword(_LIGHT_COOKIES);
        }

        private static void UpdateMyVolumetricFogMaterialParameters(Material material, int mainLightIndex, int additionalLightsCount, NativeArray<VisibleLight> visibleLights)
        {
            MyVolumetricLightingVolume volume = VolumeManager.instance.stack.GetComponent<MyVolumetricLightingVolume>();

            bool enableMainLightContribution = volume.enableMainLightContribution.value && volume.scattering.value > 0.0f && mainLightIndex > -1;
            bool enableAdditionalLightsContribution = volume.enableAdditionalLightsContribution.value && additionalLightsCount > 0;

            bool enableAPVContribution = volume.enableAPVContribution.value && volume.APVContributionWeight.value > 0.0f;
            if (enableAPVContribution)
                material.EnableKeyword("_APV_CONTRIBUTION_ENABLED");
            else
                material.DisableKeyword("_APV_CONTRIBUTION_ENABLED");

            if (enableMainLightContribution)
                material.DisableKeyword("_MAIN_LIGHT_CONTRIBUTION_DISABLED");
            else
                material.EnableKeyword("_MAIN_LIGHT_CONTRIBUTION_DISABLED");

            if (enableAdditionalLightsContribution)
                material.DisableKeyword("_ADDITIONAL_LIGHTS_CONTRIBUTION_DISABLED");
            else
                material.EnableKeyword("_ADDITIONAL_LIGHTS_CONTRIBUTION_DISABLED");

            if (enableMainLightContribution)
            {
                Anisotropies[visibleLights.Length - 1] = volume.anisotropy.value;
                Scatterings[visibleLights.Length - 1] = volume.scattering.value;
            }

            if (enableAdditionalLightsContribution)
            {
                int additionalLightIndex = 0;
                for (int i = 0; i < visibleLights.Length; ++i)
                {
                    if (i == mainLightIndex)
                        continue;

                    float anisotropy = 0.0f;
                    float scattering = 0.0f;
                    float radius = 0.0f;

                    if (visibleLights[i].light.TryGetComponent(out VolumetricLightingAdditionalLightAttributes volumetricLight))
                    {
                        if (volumetricLight.gameObject.activeInHierarchy && volumetricLight.enabled)
                        {
                            anisotropy = volumetricLight.Anisotropy;
                            scattering = volumetricLight.Scattering;
                            radius = volumetricLight.Radius;
                        }
                    }

                    Anisotropies[additionalLightIndex] = anisotropy;
                    Scatterings[additionalLightIndex] = scattering;
                    RadiiSq[additionalLightIndex++] = radius * radius;
                }
            }

            if (enableMainLightContribution || enableAdditionalLightsContribution)
            {
                material.SetFloatArray(AnisotropiesArrayId, Anisotropies);
                material.SetFloatArray(ScatteringsArrayId, Scatterings);
                material.SetFloatArray(RadiiSqArrayId, RadiiSq);
            }

            material.SetInteger(FrameCountId, Time.renderedFrameCount % 64);
            material.SetInteger(CustomAdditionalLightsCountId, additionalLightsCount);
            material.SetFloat(DistanceId, volume.distance.value);
            material.SetFloat(BaseHeightId, volume.baseHeight.value);
            material.SetFloat(MaximumHeightId, volume.maximumHeight.value);
            material.SetFloat(GroundHeightId, (volume.enableGround.overrideState && volume.enableGround.value) ? volume.groundHeight.value : float.MinValue);
            material.SetFloat(DensityId, volume.density.value);
            material.SetFloat(AbsortionId, 1.0f / volume.attenuationDistance.value);
            material.SetFloat(APVContributionWeigthId, volume.enableAPVContribution.value ? volume.APVContributionWeight.value : 0.0f);
            material.SetColor(TintId, volume.tint.value);
            material.SetInteger(MaxStepsId, volume.maxSteps.value);
            material.SetFloat(MaxPhaseIntensityId, volume.maxPhaseIntensity.value);
            material.SetFloat(DensityFalloffId, volume.densityFalloff.value);
        }
    }
}