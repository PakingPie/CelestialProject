#ifndef VOLUMETRIC_FOG_UTILS_INCLUDED
    #define VOLUMETRIC_FOG_UTILS_INCLUDED

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

    #include "./VolumetricFogInputs.hlsl"

    static const float KernelWeights[] = { 0.2026, 0.1790, 0.1240, 0.0672, 0.0285 };

    // Samples the downsampled camera depth texture.
    float SampleDownsampledSceneDepth(float2 uv)
    {
        return SAMPLE_TEXTURE2D_X(_DownsampledCameraDepthTexture, sampler_PointClamp, uv).r;
    }

    // Returns the linear eye depth for orthographic projection.
    float LinearEyeDepthOrthographic(float rawDepth)
    {
    #if UNITY_REVERSED_Z
        return lerp(_ProjectionParams.z, _ProjectionParams.y, rawDepth);
    #else
        return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
    #endif
    }

    // Returns the linear eye depth considering the camera projection type.
    float LinearEyeDepthConsiderProjection(float rawDepth)
    {
        float perspectiveDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        float orthographicDepth = LinearEyeDepthOrthographic(rawDepth);

        return lerp(perspectiveDepth, orthographicDepth, unity_OrthoParams.w);
    }

    // Blurs the RGB channels of the given texture using depth aware gaussian blur, which uses the downsampled camera depth to apply weights to the blur.
    // The alpha channel is not blurred so the original value is returned.
    float4 DepthAwareGaussianBlur(float2 uv, float2 dir, TEXTURE2D_X(textureToBlur), SAMPLER(sampler_TextureToBlur), float2 textureToBlurTexelSizeXy)
    {
        float4 centerSample = SAMPLE_TEXTURE2D_X(textureToBlur, sampler_TextureToBlur, uv);
        float centerDepth = SampleDownsampledSceneDepth(uv);
        float centerLinearEyeDepth = LinearEyeDepthConsiderProjection(centerDepth);

        int i = 0;
        float3 rgbResult = centerSample.rgb * KernelWeights[i];
        float weights = KernelWeights[i];

        float2 texelSizeTimesDir = textureToBlurTexelSizeXy * dir;

        UNITY_UNROLL
        for (i = -KERNEL_RADIUS; i < 0; ++i)
        {
            float2 uvOffset = (float)i * texelSizeTimesDir;
            float2 uvSample = uv + uvOffset;

            float depth = SampleDownsampledSceneDepth(uvSample);
            float linearEyeDepth = LinearEyeDepthConsiderProjection(depth);
            float depthDiff = abs(centerLinearEyeDepth - linearEyeDepth);
            float r2 = BLUR_DEPTH_FALLOFF * depthDiff;
            float g = exp(-r2 * r2);
            float weight = g * KernelWeights[-i];

            float3 rgb = SAMPLE_TEXTURE2D_X(textureToBlur, sampler_TextureToBlur, uvSample).rgb;
            rgbResult += (rgb * weight);
            weights += weight;
        }

        UNITY_UNROLL
        for (i = 1; i <= KERNEL_RADIUS; ++i)
        {
            float2 uvOffset = (float)i * texelSizeTimesDir;
            float2 uvSample = uv + uvOffset;

            float depth = SampleDownsampledSceneDepth(uvSample);
            float linearEyeDepth = LinearEyeDepthConsiderProjection(depth);
            float depthDiff = abs(centerLinearEyeDepth - linearEyeDepth);
            float r2 = BLUR_DEPTH_FALLOFF * depthDiff;
            float g = exp(-r2 * r2);
            float weight = g * KernelWeights[i];

            float3 rgb = SAMPLE_TEXTURE2D_X(textureToBlur, sampler_TextureToBlur, uvSample).rgb;
            rgbResult += (rgb * weight);
            weights += weight;
        }

        return float4(rgbResult * rcp(weights), centerSample.a);
    }

    // Upsamples the volumetric fog using both the downsampled and full resolution depth information.
    float4 DepthAwareUpsample(float2 uv)
    {
        float2 downsampledTexelSize = _DownsampledCameraDepthTexture_TexelSize.xy;
        float2 downsampledTopLeftCornerUv = uv - (downsampledTexelSize * 0.5);
        float2 uvs[4] =
        {
            downsampledTopLeftCornerUv + float2(0.0, downsampledTexelSize.y),
            downsampledTopLeftCornerUv + downsampledTexelSize.xy,
            downsampledTopLeftCornerUv + float2(downsampledTexelSize.x, 0.0),
            downsampledTopLeftCornerUv
        };

        float4 downsampledDepths;
        
    #if SHADER_TARGET >= 45
        downsampledDepths = GATHER_RED_TEXTURE2D_X(_DownsampledCameraDepthTexture, sampler_PointClamp, uv);
    #else
        downsampledDepths.x = SampleDownsampledSceneDepth(uvs[0]);
        downsampledDepths.y = SampleDownsampledSceneDepth(uvs[1]);
        downsampledDepths.z = SampleDownsampledSceneDepth(uvs[2]);
        downsampledDepths.w = SampleDownsampledSceneDepth(uvs[3]);
    #endif

        float fullResDepth = SampleSceneDepth(uv);
        float fullResLinearEyeDepth = LinearEyeDepthConsiderProjection(fullResDepth);
        float relativeDepthThreshold = fullResLinearEyeDepth * 0.1;

        float linearEyeDepth = LinearEyeDepthConsiderProjection(downsampledDepths[0]);
        float minLinearEyeDepthDist = abs(fullResLinearEyeDepth - linearEyeDepth);

        float2 nearestUv = uvs[0];
        int numValidDepths = minLinearEyeDepthDist < relativeDepthThreshold;
        
        UNITY_UNROLL
        for (int i = 0; i < 4; ++i)
        {
            linearEyeDepth = LinearEyeDepthConsiderProjection(downsampledDepths[i]);
            float linearEyeDepthDist = abs(fullResLinearEyeDepth - linearEyeDepth);

            bool updateNearest = linearEyeDepthDist < minLinearEyeDepthDist;
            minLinearEyeDepthDist = updateNearest ? linearEyeDepthDist : minLinearEyeDepthDist;
            nearestUv = updateNearest ? uvs[i] : nearestUv;
            
            numValidDepths += (linearEyeDepthDist < relativeDepthThreshold);
        }

        UNITY_BRANCH
        if (numValidDepths == 4)
            return SAMPLE_TEXTURE2D_X(_VolumetricFogTexture, sampler_LinearClamp, uv);
        else
            return SAMPLE_TEXTURE2D_X(_VolumetricFogTexture, sampler_PointClamp, nearestUv);
    }

#endif