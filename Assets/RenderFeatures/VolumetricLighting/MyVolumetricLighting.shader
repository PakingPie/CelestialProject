Shader "Unlit/MyVolumetricLighting"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass // Pass 0: Volumetric Fog 
        {
            Name "VolumetricFog"

            Cull Off
            ZTest Always 
            ZWrite Off 
            Blend Off

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Macros.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"

            #include "./Includes/VolumetricFogInputs.hlsl"
            #include "./Includes/VolumetricFogUtils.hlsl"
            #include "./Includes/VolumetricFogShadows.hlsl"

            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #pragma multi_compile_fragment _ PROBE_VOLUME_L1 PROBE_VOLUME_L2

            #pragma multi_compile_local_fragment _ _MAIN_LIGHT_CONTRIBUTION_DISABLED
            #pragma multi_compile_local_fragment _ _ADDITIONAL_LIGHTS_CONTRIBUTION_DISABLED
            #pragma multi_compile_local_fragment _ _APV_CONTRIBUTION_ENABLED
            #pragma vertex Vert
            #pragma fragment Frag

            int _FrameCount;
            uint _CustomAdditionalLightsCount;
            float _Distance;
            float _BaseHeight;
            float _MaximumHeight;
            float _GroundHeight;
            float _Density;
            float _Absortion;
            float _APVContributionWeight;
            float3 _Tint;
            int _MaxSteps;
            float _MaxPhaseIntensity;
            float _DensityFalloff;

            float _Anisotropies[MAX_VISIBLE_LIGHTS + 1];
            float _Scatterings[MAX_VISIBLE_LIGHTS + 1];
            float _RadiiSq[MAX_VISIBLE_LIGHTS];

            // Gets the fog density at the given world height and distance from camera.
            float GetFogDensity(float3 posWS, float distFromCamera)
            {
                // Distance-based falloff: density decays exponentially with distance from camera
                // _DensityFalloff = 0 means no falloff (uniform), higher values = faster decay
                float distanceFade = exp(-distFromCamera * _DensityFalloff);

                return _Density * distanceFade;
            }

            // Gets the main light scattering source rate at one raymarch step.
            // Returns sigma_s * phase * L_incident (the in-scattering source term, WITHOUT density)
            float3 GetStepMainLightColor(float3 currPosWS, float phaseMainLight)
            {
                #if _MAIN_LIGHT_CONTRIBUTION_DISABLED
                    return float3(0.0, 0.0, 0.0);
                #endif
                Light mainLight = GetMainLight();
                float4 shadowCoord = TransformWorldToShadowCoord(currPosWS);
                mainLight.shadowAttenuation = VolumetricMainLightRealtimeShadow(shadowCoord);
                #if _LIGHT_COOKIES
                    mainLight.color *= SampleMainLightCookie(currPosWS);
                #endif
                return (mainLight.color * _Tint) * (mainLight.shadowAttenuation * phaseMainLight * _Scatterings[_CustomAdditionalLightsCount]);
            }

            // Gets the accumulated color from additional lights at one raymarch step.
            // Returns in-scattering source rate (WITHOUT density, density is applied in the main loop).
            float3 GetStepAdditionalLightsColor(float2 uv, float3 currPosWS, float3 rd)
            {
                #if _ADDITIONAL_LIGHTS_CONTRIBUTION_DISABLED
                    return float3(0.0, 0.0, 0.0);
                #endif
                #if _CLUSTER_LIGHT_LOOP
                    // Forward+ rendering path needs this data before the light loop
                    InputData inputData = (InputData)0;
                    inputData.normalizedScreenSpaceUV = uv;
                    inputData.positionWS = currPosWS;
                #endif
                // initialize the accumulated color from additional lights
                float3 additionalLightsColor = float3(0.0, 0.0, 0.0);   
                
                // loop differently through lights in Forward+ while considering Forward and Deferred too
                LIGHT_LOOP_BEGIN(_CustomAdditionalLightsCount)
                float additionalLightScattering = _Scatterings[lightIndex];

                UNITY_BRANCH
                if (additionalLightScattering <= 0.0)
                continue;

                Light additionalLight = GetAdditionalPerObjectLight(lightIndex, currPosWS);
                additionalLight.shadowAttenuation = VolumetricAdditionalLightRealtimeShadow(lightIndex, currPosWS, additionalLight.direction);
                #if _LIGHT_COOKIES
                    // when light cookies are enabled and a cookie is set for this additional light also factor it
                    additionalLight.color *= SampleAdditionalLightCookie(lightIndex, currPosWS);
                #endif

                // See universal\ShaderLibrary\RealtimeLights.hlsl - GetAdditionalPerObjectLight
                #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                    float4 additionalLightPos = _AdditionalLightsBuffer[lightIndex].position;
                #else
                    float4 additionalLightPos = _AdditionalLightsPosition[lightIndex];
                #endif
                // Note: This is useful for both spotlights and pointlights. For the latter it is specially true when the point light is inside some geometry and casts shadows.
                // Gradually reduce additional lights scattering to zero at their origin to try to avoid flicker-aliasing.
                float3 distToPos = additionalLightPos.xyz - currPosWS;
                float distToPosMagnitudeSq = dot(distToPos, distToPos);
                float newScattering = smoothstep(0.0, _RadiiSq[lightIndex], distToPosMagnitudeSq);
                newScattering = newScattering * newScattering;
                newScattering *= _Scatterings[lightIndex];
                
                // Note: If directional lights are also considered as additional lights when more than 1 is used, ignore the previous code when it is a directional light.
                // They store direction in additionalLightPos.xyz and have .w set to 0, while point and spotlights have it set to 1.
                // newScattering = lerp(1.0, newScattering, additionalLightPos.w);

                // accumulate the total color for additional lights (phase clamped to prevent blow-out)
                float rawPhaseAL = CornetteShanksPhaseFunction(_Anisotropies[lightIndex], dot(rd, additionalLight.direction));
                float phaseAL = min(rawPhaseAL, _MaxPhaseIntensity * 0.07957747);
                additionalLightsColor += (additionalLight.color * (additionalLight.shadowAttenuation * additionalLight.distanceAttenuation * phaseAL * newScattering));
                LIGHT_LOOP_END

                return additionalLightsColor;
            }

            // Computes the needed ray origin and direction for orthographic projection.
            float3 ComputeOrthographicParams(float2 uv, float depth, out float3 ro, out float3 rd)
            {
                float4x4 viewMatrix = UNITY_MATRIX_V;
                float2 ndc = uv * 2.0 - 1.0;

                float3 camRightWs = normalize(viewMatrix[0].xyz);
                float3 camUpWs = normalize(viewMatrix[1].xyz);
                float3 camFwdWs = normalize(-viewMatrix[2].xyz);

                float3 posWs = GetCameraPositionWS() + 
                (camRightWs * (ndc.x * unity_OrthoParams.x)) +
                (camUpWs * (ndc.y * unity_OrthoParams.y)) +
                (camFwdWs * depth);
                
                rd = camFwdWs;
                ro = posWs - rd * depth;

                return posWs;
            }

            float3 GetStepAdaptiveVolumeEvaluation(float2 uv, float3 posWS, float density)
            {
                float3 apvDiffuseGI = 0;
                #if _APV_CONTRIBUTION_ENABLED
                    #if defined(PROBE_VOLUME_L1) || defined(PROBE_VOLUME_L2)
                        EvaluateAdaptiveProbeVolume(posWS, uv * _ScreenSize.xy, apvDiffuseGI);    
                        apvDiffuseGI = apvDiffuseGI * _APVContributionWeight * density;
                    #endif
                #endif

                return apvDiffuseGI;
            }

            void CalculateRayMarchingParams(float2 uv, out float3 ro, out float3 rd, out float initialOffsetToNearPlane, out float offsetLength, out float3 rdPhase)
            {
                // prepare the ray origin and direction
                float depth = SampleDownsampledSceneDepth(uv);
                float3 posWS = 0;

                UNITY_BRANCH
                if (unity_OrthoParams.w <= 0) // perspective
                {
                    ro = GetCameraPositionWS();
                    #if !UNITY_REVERSED_Z
                        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                    #endif
                    posWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                    float3 offset = posWS - ro;
                    offsetLength = length(offset);
                    rd = offset / offsetLength;
                    rdPhase = rd;

                    float3 camFwd= normalize(-UNITY_MATRIX_V[2].xyz);
                    float cos = dot(camFwd, rd);
                    float fragElongation = 1.0 / cos;
                    initialOffsetToNearPlane = fragElongation * _ProjectionParams.y;
                }
                else // orthographic
                {
                    depth = LinearEyeDepthOrthographic(depth);
                    posWS = ComputeOrthographicParams(uv, depth, ro, rd);
                    offsetLength = depth;
                    rdPhase = rd;//normalize(posWS - GetCameraPositionWS()); // fake fase?
                    initialOffsetToNearPlane = _ProjectionParams.y;
                }
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // not initialize will causing complile error in some cases
                float3 ro = 0;
                float3 rd = 0;
                float offsetLength = 0;
                float3 rdPhase = 0;
                float initialOffsetToNearPlane;

                CalculateRayMarchingParams(input.texcoord, ro, rd, initialOffsetToNearPlane, offsetLength, rdPhase);

                // Fix: Handle skybox and clamp to max fog distance
                float rawDepth = SampleDownsampledSceneDepth(input.texcoord);
                #if UNITY_REVERSED_Z
                    bool isSkybox = rawDepth <= 0.0001;
                #else
                    bool isSkybox = rawDepth >= 0.9999;
                #endif

                if (isSkybox)
                {
                    offsetLength = _Distance;
                }
                else
                {
                    offsetLength = min(offsetLength, _Distance);
                }

                // Logarithmic step distribution — denser near camera, sparser far away
                float nearStep = max(1.0, initialOffsetToNearPlane);
                float farStep = max(nearStep + 0.01, offsetLength);
                float logNear = log(nearStep);
                float logFar = log(farStep);
                float logRange = logFar - logNear;
                float jitter01 = InterleavedGradientNoise(input.positionCS, _FrameCount);

                #if _MAIN_LIGHT_CONTRIBUTION_DISABLED
                    float phaseMainLight = 0.0;
                #else
                    // Clamp phase to prevent blow-out at high anisotropy
                    float rawPhase = CornetteShanksPhaseFunction(_Anisotropies[_CustomAdditionalLightsCount], dot(rdPhase, GetMainLight().direction));
                    float maxPhase = _MaxPhaseIntensity * 0.07957747; // _MaxPhaseIntensity x isotropic (1/4pi)
                    float phaseMainLight = min(rawPhase, maxPhase);
                #endif
                
                // initialize the volumetric fog color and transmittance
                float3 volumetricFogColor = float3(0.0, 0.0, 0.0);
                float transmittance = 1.0;

                UNITY_LOOP
                for (int i = 0; i < _MaxSteps; ++i)
                {
                    // Logarithmic step: denser near camera, sparser far away
                    float t = (float(i) + jitter01) / float(_MaxSteps);
                    float dist = exp(logNear + t * logRange);

                    // perform depth test to break out early
                    UNITY_BRANCH
                    if (dist >= offsetLength)
                    break;

                    // Per-step length for exponential distribution (derivative of exp mapping)
                    float localStepLength = dist * logRange / float(_MaxSteps);

                    float3 currPosWS = ro + rd * dist;

                    // calculate density with distance-based falloff
                    float density = GetFogDensity(currPosWS, dist);
                    
                    // keep marching when there is not enough density
                    UNITY_BRANCH
                    if (density <= 0.0)
                    continue;

                    // Extinction coefficient: sigma_t = density * absorption_coeff
                    float sigmaT = density * _Absortion;

                    // Transmittance for this step (Beer-Lambert)
                    float stepAttenuation = exp(-localStepLength * sigmaT);

                    // Energy-conserving in-scattering weight (exponential integration)
                    // Analytical integral: (1 - exp(-sigmaT * ds)) / sigmaT
                    // Approaches stepLength when sigmaT*ds is small, saturates at 1/sigmaT for large steps
                    float scatterWeight = (sigmaT > 0.0001) ? (1.0 - stepAttenuation) / sigmaT : localStepLength;

                    // In-scattering source: density * (sum of light contributions)
                    // density acts as the scattering coefficient sigma_s here
                    float3 apvColor = GetStepAdaptiveVolumeEvaluation(input.texcoord, currPosWS, density);
                    float3 mainLightColor = GetStepMainLightColor(currPosWS, phaseMainLight);
                    float3 additionalLightsColor = GetStepAdditionalLightsColor(input.texcoord, currPosWS, rd);

                    float3 sourceRate = density * (mainLightColor + additionalLightsColor);

                    // Accumulate: source * transmittance * energy-conserving weight
                    volumetricFogColor += sourceRate * transmittance * scatterWeight;

                    // Attenuate transmittance AFTER accumulation (correct ordering)
                    transmittance *= stepAttenuation;
                }

                return float4(volumetricFogColor, transmittance);
            }

            ENDHLSL
        }

        Pass // Pass 1: Blur Volumetric Fog at Horizontal
        {
            Name "VolumetricFogHorizontalBlur"
            
            ZTest Always ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./Includes/VolumetricFogInputs.hlsl"
            #include "./Includes/VolumetricFogUtils.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return DepthAwareGaussianBlur(input.texcoord, float2(1.0, 0.0), _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        Pass // Pass 2: Blur Volumetric Fog at Vertical
        {
            Name "VolumetricFogVerticalBlur"
            
            ZTest Always ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./Includes/VolumetricFogInputs.hlsl"
            #include "./Includes/VolumetricFogUtils.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return DepthAwareGaussianBlur(input.texcoord, float2(0.0, 1.0), _BlitTexture, sampler_PointClamp, _BlitTexture_TexelSize.xy);
            }

            ENDHLSL
        }

        Pass // Pass 3: Upsample Volumetric Fog
        {
            Name "VolumetricFogUpsampleComposition"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./Includes/VolumetricFogInputs.hlsl"
            #include "./Includes/VolumetricFogUtils.hlsl"

            #pragma target 4.5

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 volumetricFog = DepthAwareUpsample(input.texcoord);
                float4 cameraColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord);

                return float4(cameraColor.rgb * volumetricFog.a + volumetricFog.rgb, cameraColor.a);
            }

            ENDHLSL
        }

        // Pass // Pass 4: Downsample Depth from camera depth texture
        // {
            //     Name "DownSampleCameraDepth"

            //     ZTest Always ZWrite Off 
            //     Cull Off
            //     Blend Off
            //     ColorMask R

            //     HLSLPROGRAM

            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            //     #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            //     #pragma target 4.5
            //     #pragma editor_sync_compilation

            //     #pragma vertex Vert
            //     #pragma fragment Frag

            //     float Frag(Varyings input) : SV_Target
            //     {
                //         UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                //         float4 depths = GATHER_RED_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.texcoord);
                
                //         float minDepth = Min3(depths.x, depths.y, min(depths.z, depths.w));
                //         float maxDepth = Max3(depths.x, depths.y, max(depths.z, depths.w));

                //         return (uint(input.positionCS.x + input.positionCS.y) & 1) > 0 ? minDepth : maxDepth;
            //     }

            //     ENDHLSL
        // }

        Pass // Pass 5: Downsample Depth from scene depth texture
        {
            Name "DownSampleDepth"

            ZTest Always ZWrite Off

            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            
            #pragma editor_sync_compilation

            #pragma vertex Vert
            #pragma fragment Frag

            float Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 depths;

                uint2 fullResTopLeftCorner = uint2(input.positionCS.xy * 2.0);

                depths.x = LoadSceneDepth(fullResTopLeftCorner + uint2(0, 1));
                depths.y = LoadSceneDepth(fullResTopLeftCorner + uint2(1, 1));
                depths.z = LoadSceneDepth(fullResTopLeftCorner + uint2(1, 0));
                depths.w = LoadSceneDepth(fullResTopLeftCorner);

                float minDepth = Min3(depths.x, depths.y, min(depths.z, depths.w));
                float maxDepth = Max3(depths.x, depths.y, max(depths.z, depths.w));

                return (uint(input.positionCS.x + input.positionCS.y) & 1) > 0 ? minDepth : maxDepth;
            }

            ENDHLSL
        }
    }
    Fallback Off
}
