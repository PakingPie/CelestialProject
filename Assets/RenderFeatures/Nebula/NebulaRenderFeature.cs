// NebulaRenderFeature.cs
// Requires Unity 6+ / URP 17+ with the RenderGraph API.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

// ═══════════════════════════════════════════════════════════════════════════════
//  Feature
// ═══════════════════════════════════════════════════════════════════════════════

public class NebulaRenderFeature : ScriptableRendererFeature
{
    [Serializable]
    public class NebulaSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        [Tooltip("Assign the Hidden/Nebula/Raymarch shader.")]
        public Shader nebulaShader;

        [Range(1, 4)]
        [Tooltip("Downscale factor for the volumetric raymarch. 2 = half res.")]
        public int downscale = 2;

        [Header("Temporal Accumulation")]
        public bool enableTemporal = true;

        [Range(0.02f, 0.3f)]
        [Tooltip("Blend factor for new frame. Lower = smoother but more ghosting.")]
        public float temporalBlend = 0.05f;
    }

    public NebulaSettings settings = new NebulaSettings();

    private NebulaRenderPass renderPass;
    private Material nebulaMaterial;

    public override void Create()
    {
        if (settings.nebulaShader == null) return;

        nebulaMaterial = CoreUtils.CreateEngineMaterial(settings.nebulaShader);

        renderPass = new NebulaRenderPass(settings, nebulaMaterial)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer,
                                         ref RenderingData renderingData)
    {
        if (nebulaMaterial == null || renderPass == null) return;
        if (NebulaVolume.activeVolumes.Count == 0) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        renderPass.ConfigureInput(ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
        if (nebulaMaterial != null)
            CoreUtils.Destroy(nebulaMaterial);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  Render Pass
// ═══════════════════════════════════════════════════════════════════════════════

public class NebulaRenderPass : ScriptableRenderPass, IDisposable
{
    // ─── Fields ───

    private readonly NebulaRenderFeature.NebulaSettings settings;
    private readonly Material material;

    // *** FIX: Persistent RT for the raymarch result (not transient) ***
    private RTHandle nebulaRT;

    // Temporal history (persistent across frames)
    private RTHandle historyA;
    private RTHandle historyB;
    private bool pingPong;
    private Matrix4x4 prevViewProjMatrix = Matrix4x4.identity;
    private bool hasPrevFrame;
    private int prevWidth, prevHeight;

    // Shader property IDs
    private static readonly int _NebulaWorldToLocal = Shader.PropertyToID("_NebulaWorldToLocal");
    private static readonly int _NebulaLocalToWorld = Shader.PropertyToID("_NebulaLocalToWorld");
    private static readonly int _AxisStretch = Shader.PropertyToID("_AxisStretch");
    private static readonly int _NebulaColor = Shader.PropertyToID("_NebulaColor");
    private static readonly int _Power = Shader.PropertyToID("_Power");
    private static readonly int _FadeInnerRadius = Shader.PropertyToID("_FadeInnerRadius");
    private static readonly int _FadeOuterRadius = Shader.PropertyToID("_FadeOuterRadius");
    private static readonly int _FadeNoiseStrength = Shader.PropertyToID("_FadeNoiseStrength");
    private static readonly int _FadeBoxMargin = Shader.PropertyToID("_FadeBoxMargin");
    private static readonly int _ShapeNoiseScale = Shader.PropertyToID("_ShapeNoiseScale");
    private static readonly int _ShapeTendrilStrength = Shader.PropertyToID("_ShapeTendrilStrength");
    private static readonly int _NoiseDomainHalf = Shader.PropertyToID("_NoiseDomainHalf");
    private static readonly int _NoiseVolume = Shader.PropertyToID("_NoiseVolume");
    private static readonly int _StepsPrimary = Shader.PropertyToID("_StepsPrimary");
    private static readonly int _StepsLight = Shader.PropertyToID("_StepsLight");
    private static readonly int _EnableStars = Shader.PropertyToID("_EnableStars");
    private static readonly int _StarDensity = Shader.PropertyToID("_StarDensity");
    private static readonly int _StarBrightness = Shader.PropertyToID("_StarBrightness");
    private static readonly int _BlueNoise = Shader.PropertyToID("_BlueNoise");
    private static readonly int _DitherSpeed = Shader.PropertyToID("_DitherSpeed");
    private static readonly int _EmissionStrength = Shader.PropertyToID("_EmissionStrength");
    private static readonly int _ColorLowDensity = Shader.PropertyToID("_ColorLowDensity");
    private static readonly int _ColorMidDensity = Shader.PropertyToID("_ColorMidDensity");
    private static readonly int _ColorHighDensity = Shader.PropertyToID("_ColorHighDensity");
    private static readonly int _DetailStrength = Shader.PropertyToID("_DetailStrength");
    private static readonly int _VoidStrength = Shader.PropertyToID("_VoidStrength");
    private static readonly int _DensityContrast = Shader.PropertyToID("_DensityContrast");
    private static readonly int _NebulaTexture = Shader.PropertyToID("_NebulaTexture");
    private static readonly int _HistoryTexture = Shader.PropertyToID("_HistoryTexture");
    private static readonly int _TemporalBlendFactor = Shader.PropertyToID("_TemporalBlendFactor");
    private static readonly int _NebulaPrevVP = Shader.PropertyToID("_NebulaPrevVP");

    // ─── Constructor ───

    public NebulaRenderPass(NebulaRenderFeature.NebulaSettings settings, Material material)
    {
        this.settings = settings;
        this.material = material;
    }

    // ─── Pass data structs ───

    private class NebulaPassData
    {
        public Material material;
        public NebulaVolumeGPU[] volumes;
        public int volumeCount;
    }

    private class TemporalPassData
    {
        public Material material;
        public TextureHandle currentFrame;
        public TextureHandle historyIn;
        public float blendFactor;
        public Matrix4x4 prevVP;
    }

    private class CompositePassData
    {
        public Material material;
        public TextureHandle source;
    }

    private struct NebulaVolumeGPU
    {
        public Matrix4x4 worldToLocal;
        public Matrix4x4 localToWorld;
        public Vector4 axisStretch;
        public Color nebulaColor;
        public float lightPower;
        public float fadeInnerRadius, fadeOuterRadius;
        public float fadeNoiseStrength, fadeBoxMargin;
        public float shapeNoiseScale, shapeTendrilStrength;
        public float noiseDomainHalf;
        public int stepsPrimary, stepsLight;
        public float enableStars;
        public float starDensity, starBrightness;
        public float ditherSpeed;
        public float emissionStrength;
        public Color colorLowDensity, colorMidDensity, colorHighDensity;
        public float detailStrength, voidStrength, densityContrast;
        public Texture3D noiseTexture;
        public Texture2D blueNoiseTexture;
    }

    // ─── Resource management ───

    /// <summary>
    /// Ensures the persistent half-res nebula RT exists at the required resolution.
    /// </summary>
    private void EnsureNebulaRT(int w, int h)
    {
        if (nebulaRT != null && prevWidth == w && prevHeight == h)
            return;

        nebulaRT?.Release();

        var desc = new RenderTextureDescriptor(w, h,
            RenderTextureFormat.ARGBHalf, 0)
        {
            enableRandomWrite = false,
            msaaSamples = 1,
            sRGB = false
        };

        nebulaRT = RTHandles.Alloc(desc, FilterMode.Bilinear,
            TextureWrapMode.Clamp, name: "NebulaHalfRes");

        ClearRT(nebulaRT);

        // *** ADD THESE TWO LINES ***
        prevWidth = w;
        prevHeight = h;
    }

    /// <summary>
    /// Ensures the persistent temporal history buffers exist.
    /// </summary>
    private void EnsureTemporalBuffers(int w, int h)
    {
        // Re-use prevWidth/prevHeight check (already set by EnsureNebulaRT)
        if (historyA != null && historyA.rt != null
            && historyA.rt.width == w && historyA.rt.height == h)
            return;

        historyA?.Release();
        historyB?.Release();

        var desc = new RenderTextureDescriptor(w, h,
            RenderTextureFormat.ARGBHalf, 0)
        {
            enableRandomWrite = false,
            msaaSamples = 1,
            sRGB = false
        };

        historyA = RTHandles.Alloc(desc, FilterMode.Bilinear,
            TextureWrapMode.Clamp, name: "NebulaHistoryA");
        historyB = RTHandles.Alloc(desc, FilterMode.Bilinear,
            TextureWrapMode.Clamp, name: "NebulaHistoryB");

        // *** FIX: clear history so first frame doesn't read garbage ***
        ClearRT(historyA);
        ClearRT(historyB);

        hasPrevFrame = false;
        pingPong = false;
    }

    private static void ClearRT(RTHandle rt)
    {
        if (rt == null || rt.rt == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = rt.rt;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    // ─── RecordRenderGraph ───

    public override void RecordRenderGraph(RenderGraph renderGraph,
                                           ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        Camera camera = cameraData.camera;

        int halfW = Mathf.Max(1, camera.pixelWidth / settings.downscale);
        int halfH = Mathf.Max(1, camera.pixelHeight / settings.downscale);

        // ── Collect active volumes ──

        List<NebulaVolume> src = NebulaVolume.activeVolumes;
        NebulaVolumeGPU[] volumes = new NebulaVolumeGPU[src.Count];
        int count = 0;

        for (int i = 0; i < src.Count; i++)
        {
            NebulaVolume v = src[i];
            if (v == null || v.noiseTexture == null) continue;

            volumes[count++] = new NebulaVolumeGPU
            {
                worldToLocal = v.transform.worldToLocalMatrix,
                localToWorld = v.transform.localToWorldMatrix,
                axisStretch = new Vector4(v.axisStretch.x, v.axisStretch.y, v.axisStretch.z, 0),
                nebulaColor = v.nebulaColor,
                lightPower = v.lightPower,
                fadeInnerRadius = v.fadeInnerRadius,
                fadeOuterRadius = v.fadeOuterRadius,
                fadeNoiseStrength = v.fadeNoiseStrength,
                fadeBoxMargin = v.fadeBoxMargin,
                shapeNoiseScale = v.shapeNoiseScale,
                shapeTendrilStrength = v.shapeTendrilStrength,
                noiseDomainHalf = v.noiseDomainHalf,
                stepsPrimary = v.stepsPrimary,
                stepsLight = v.stepsLight,
                enableStars = v.enableStars ? 1f : 0f,
                starDensity = v.starDensity,
                starBrightness = v.starBrightness,
                ditherSpeed = v.ditherSpeed,
                emissionStrength = v.emissionStrength,
                colorLowDensity = v.colorLowDensity,
                colorMidDensity = v.colorMidDensity,
                colorHighDensity = v.colorHighDensity,
                detailStrength = v.detailStrength,
                voidStrength = v.voidStrength,
                densityContrast = v.densityContrast,
                noiseTexture = v.noiseTexture,
                blueNoiseTexture = v.blueNoiseTexture
            };
        }
        if (count == 0) return;

        // ── Ensure persistent render targets ──

        EnsureNebulaRT(halfW, halfH);

        // *** FIX: import the persistent RT into the render graph ***
        TextureHandle nebulaHalfRes = renderGraph.ImportTexture(nebulaRT);

        // ── Pass 0: Nebula Raymarch → half-res RT ──

        using (var builder = renderGraph.AddRasterRenderPass<NebulaPassData>(
                   "Nebula Raymarch", out var passData))
        {
            passData.material = material;
            passData.volumes = volumes;
            passData.volumeCount = count;

            builder.SetRenderAttachment(nebulaHalfRes, 0);

            if (resourceData.cameraDepthTexture.IsValid())
                builder.UseTexture(resourceData.cameraDepthTexture);

            builder.SetRenderFunc(static (NebulaPassData data, RasterGraphContext ctx) =>
                ExecuteNebulaPass(data, ctx));
        }

        // // ── Pass 1: Temporal Blend (optional) ──

        TextureHandle compositeSource = nebulaHalfRes;

        if (settings.enableTemporal)
        {
            EnsureTemporalBuffers(halfW, halfH);

            RTHandle histIn  = pingPong ? historyB : historyA;
            RTHandle histOut = pingPong ? historyA : historyB;

            TextureHandle histInHandle  = renderGraph.ImportTexture(histIn);
            TextureHandle histOutHandle = renderGraph.ImportTexture(histOut);

            using (var builder = renderGraph.AddRasterRenderPass<TemporalPassData>(
                       "Nebula Temporal", out var passData))
            {
                passData.material       = material;
                passData.currentFrame   = nebulaHalfRes;
                passData.historyIn      = histInHandle;
                passData.blendFactor    = hasPrevFrame ? settings.temporalBlend : 1f;
                passData.prevVP         = prevViewProjMatrix;

                builder.UseTexture(nebulaHalfRes);
                builder.UseTexture(histInHandle);
                builder.SetRenderAttachment(histOutHandle, 0);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (TemporalPassData data, RasterGraphContext ctx) =>
                    ExecuteTemporalPass(data, ctx));
            }

            compositeSource = histOutHandle;

            // Advance state for next frame
            prevViewProjMatrix = GL.GetGPUProjectionMatrix(
                camera.projectionMatrix, true) * camera.worldToCameraMatrix;
            pingPong     = !pingPong;
            hasPrevFrame = true;
        }

        // ── Pass 2: Composite → camera colour (additive) ──

        using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                   "Nebula Composite", out var passData))
        {
            passData.material = material;
            passData.source   = compositeSource;

            builder.UseTexture(compositeSource);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext ctx) =>
                ExecuteCompositePass(data, ctx));
        }
    }

    // ─── Execute functions ───

    private static void ExecuteNebulaPass(NebulaPassData data, RasterGraphContext ctx)
    {
        var cmd = ctx.cmd;
        var mat = data.material;

        // *** FIX: clear the persistent RT before additive draws ***
        cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);

        for (int i = 0; i < data.volumeCount; i++)
        {
            ref NebulaVolumeGPU vol = ref data.volumes[i];

            mat.SetMatrix(_NebulaWorldToLocal, vol.worldToLocal);
            mat.SetMatrix(_NebulaLocalToWorld, vol.localToWorld);
            mat.SetVector(_AxisStretch, vol.axisStretch);
            mat.SetColor(_NebulaColor, vol.nebulaColor);
            mat.SetFloat(_Power, vol.lightPower);
            mat.SetFloat(_FadeInnerRadius, vol.fadeInnerRadius);
            mat.SetFloat(_FadeOuterRadius, vol.fadeOuterRadius);
            mat.SetFloat(_FadeNoiseStrength, vol.fadeNoiseStrength);
            mat.SetFloat(_FadeBoxMargin, vol.fadeBoxMargin);
            mat.SetFloat(_ShapeNoiseScale, vol.shapeNoiseScale);
            mat.SetFloat(_ShapeTendrilStrength, vol.shapeTendrilStrength);
            mat.SetFloat(_NoiseDomainHalf, vol.noiseDomainHalf);
            mat.SetInteger(_StepsPrimary, vol.stepsPrimary);
            mat.SetInteger(_StepsLight, vol.stepsLight);
            mat.SetFloat(_EnableStars, vol.enableStars);
            mat.SetFloat(_StarDensity, vol.starDensity);
            mat.SetFloat(_StarBrightness, vol.starBrightness);
            mat.SetFloat(_DitherSpeed, vol.ditherSpeed);
            mat.SetFloat(_EmissionStrength, vol.emissionStrength);
            mat.SetColor(_ColorLowDensity, vol.colorLowDensity);
            mat.SetColor(_ColorMidDensity, vol.colorMidDensity);
            mat.SetColor(_ColorHighDensity, vol.colorHighDensity);
            mat.SetFloat(_DetailStrength, vol.detailStrength);
            mat.SetFloat(_VoidStrength, vol.voidStrength);
            mat.SetFloat(_DensityContrast, vol.densityContrast);
            mat.SetTexture(_NoiseVolume, vol.noiseTexture);

            if (vol.blueNoiseTexture != null)
                mat.SetTexture(_BlueNoise, vol.blueNoiseTexture);

            cmd.DrawProcedural(Matrix4x4.identity, mat, 0,
                               MeshTopology.Triangles, 3);
        }
    }

    private static void ExecuteTemporalPass(TemporalPassData data, RasterGraphContext ctx)
    {
        var cmd = ctx.cmd;
        var mat = data.material;

        cmd.SetGlobalTexture("_NebulaTexture",  data.currentFrame);
        cmd.SetGlobalTexture("_HistoryTexture", data.historyIn);
        mat.SetFloat(_TemporalBlendFactor, data.blendFactor);
        mat.SetMatrix(_NebulaPrevVP, data.prevVP);

        cmd.DrawProcedural(Matrix4x4.identity, mat, 1,
                           MeshTopology.Triangles, 3);
    }

    private static void ExecuteCompositePass(CompositePassData data, RasterGraphContext ctx)
    {
        ctx.cmd.SetGlobalTexture("_NebulaTexture", data.source);
        ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, 2,
                               MeshTopology.Triangles, 3);
    }

    // ─── Cleanup ───

    public void Dispose()
    {
        nebulaRT?.Release();
        historyA?.Release();
        historyB?.Release();
        nebulaRT = null;
        historyA = null;
        historyB = null;
    }
}