Shader "Custom/VolumetricCloudsSphere"
{
    Properties
    {
        [Header(Cloud Shape)]
        _CloudDensity ("Cloud Density", Range(0, 100)) = 8.0
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Range(0.1, 50)) = 8.0
        _DetailScale ("Detail Scale", Range(1, 20)) = 6.0
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.4
        _ErosionStrength ("Erosion Strength", Range(0, 1)) = 0.25
        _Patchiness ("Patchiness", Range(0, 1)) = 0.7
        _PatchScale ("Patch Scale", Range(0.1, 10)) = 2.0
        _Billowness ("Billowness", Range(0, 1)) = 0.5
        
        [Header(Sphere Settings)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.50
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.54
        _CloudLayerDensity ("Layer Density Multiplier", Range(0.1, 5)) = 1.5
        
        [Header(Raymarching)]
        _MaxSteps ("Max Steps", Range(8, 256)) = 96
        _StepSize ("Step Size", Range(0.0001, 0.02)) = 0.002
        _LightSteps ("Light March Steps", Range(3, 12)) = 6
        
        [Header(Lighting)]
        _LightAbsorption ("Light Absorption", Range(0, 10)) = 1.8
        _CloudAbsorption ("Cloud Self Shadow", Range(0, 5)) = 2.5
        _AmbientLight ("Ambient Light", Range(0, 2)) = 0.35
        _ScatteringForward ("Forward Scattering", Range(0, 0.99)) = 0.85
        _ScatteringBack ("Back Scattering", Range(0, 0.99)) = 0.25
        _ScatteringBlend ("Scattering Blend", Range(0, 1)) = 0.7
        _SilverLiningIntensity ("Silver Lining", Range(0, 3)) = 1.2
        _SilverLiningSpread ("Silver Lining Spread", Range(1, 20)) = 6.0
        _PowderStrength ("Powder Effect", Range(0, 1)) = 0.4
        _MultiScatter ("Multi-Scattering", Range(0, 1)) = 0.5
        _LocalLightIntensity ("Local Light Intensity", Float) = 1.0          // ▶ NEW
        
        [Header(Color)]
        _CloudColorBright ("Cloud Color Bright", Color) = (1, 0.98, 0.95, 1)
        _CloudColorDark ("Cloud Color Dark", Color) = (0.55, 0.58, 0.65, 1)
        _AmbientColorTop ("Ambient Color Top", Color) = (0.6, 0.75, 1.0, 1)
        _AmbientColorBottom ("Ambient Color Bottom", Color) = (0.4, 0.42, 0.5, 1)
        _SunColor ("Sun Tint", Color) = (1.0, 0.95, 0.85, 1)

        [Header(Fire Effect)]
        [Toggle] _FireEnabled ("Enable Fire", Float) = 0
        _FireIntensity ("Fire Intensity", Range(0, 10)) = 2.0
        _FireColorBright ("Fire Color Bright", Color) = (1.0, 0.85, 0.3, 1)
        _FireColorDark ("Fire Color Dark", Color) = (0.7, 0.1, 0.02, 1)
        _FireScale ("Fire Pattern Scale", Range(0.1, 20)) = 5.0
        _FireDetailScale ("Fire Detail Scale", Range(1, 8)) = 3.0
        _FireCoverage ("Fire Coverage", Range(0, 1)) = 0.5
        _FireHeightFalloff ("Fire Height Falloff", Range(0.1, 5)) = 1.5
        _FireAnimSpeed ("Fire Animation Speed", Range(0, 2)) = 0.4
        _FireDayFade ("Fire Day-side Fade", Range(0, 1)) = 0.7
        
        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 0.5)) = 0.03
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.2, 0)
        _DetailWindMultiplier ("Detail Wind Speed", Range(0.5, 3)) = 1.5
        
        [Header(Textures)]
        _NoiseTexture ("3D Noise Texture", 3D) = "white" {}
        _NoiseTiling ("Noise Tiling", Vector) = (1, 1, 1, 0)
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)
        _BlueNoise ("Blue Noise", 2D) = "gray" {}
        _BlueNoiseTiling ("Blue Noise Tiling", Vector) = (1, 1, 0, 0)
        _BlueNoiseOffset ("Blue Noise Offset", Vector) = (0, 0, 0, 0)

        [Header(Shadows)]
        _ShadowDensityScale ("Shadow Density Scale", Range(0, 5)) = 1.5
    }
    
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "VolumetricCloudPass"
            
            Cull Front
            ZTest Always
            ZWrite Off
            Blend One OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #pragma shader_feature_local _FIREENABLED_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            
            
            // ============================================================
            // Properties
            // ============================================================
            CBUFFER_START(UnityPerMaterial)
                float _CloudDensity;
                float _CloudCoverage;
                float _CloudScale;
                float _DetailScale;
                float _DetailStrength;
                float _ErosionStrength;
                float _Patchiness;
                float _PatchScale;
                float _Billowness;
                float _InnerRadius;
                float _OuterRadius;
                float _CloudLayerDensity;
                int _MaxSteps;
                float _StepSize;
                int _LightSteps;
                float _LightAbsorption;
                float _CloudAbsorption;
                float _AmbientLight;
                float _ScatteringForward;
                float _ScatteringBack;
                float _ScatteringBlend;
                float _SilverLiningIntensity;
                float _SilverLiningSpread;
                float _PowderStrength;
                float _MultiScatter;
                float4 _CloudColorBright;
                float4 _CloudColorDark;
                float4 _AmbientColorTop;
                float4 _AmbientColorBottom;
                float4 _SunColor;

                float _FireIntensity;
                float4 _FireColorBright;
                float4 _FireColorDark;
                float _FireScale;
                float _FireDetailScale;
                float _FireCoverage;
                float _FireHeightFalloff;
                float _FireAnimSpeed;
                float _FireDayFade;

                float _WindSpeed;
                float4 _WindDirection;
                float _DetailWindMultiplier;
                float4 _NoiseTiling;
                float4 _NoiseOffset;
                float4 _BlueNoiseTiling;
                float4 _BlueNoiseOffset;
                
                float _ShadowDensityScale;      // ▶ NEW (moved here so both-pass CBUFFERs match)
                float _LocalLightIntensity;      // ▶ NEW

                TEXTURE3D(_NoiseTexture);
                SAMPLER(sampler_NoiseTexture);
                TEXTURE2D(_BlueNoise);
                SAMPLER(sampler_BlueNoise);
                
                TEXTURE2D_X_FLOAT(_CameraDepthTexture);
                SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_END

            #include "./VolumetricCloudsUtilities.hlsl"

            // ▶ NEW — Additional light data (set by AtmosphericLightManager)
            #define MAX_ADDITIONAL_LIGHTS 16
            #define LOCAL_LIGHT_SHADOW_STEPS 2

            struct AtmosphericLightData
            {
                float4 positionAndRange;      // xyz = world pos, w = range
                float4 colorAndType;          // xyz = color*intensity, w = 0 point / 1 spot
                float4 directionAndAngles;    // xyz = spot forward, w = cos(outerAngle/2)
                float4 extraParams;           // x = cos(innerAngle/2), yzw = reserved
            };

            StructuredBuffer<AtmosphericLightData> _AdditionalLights;
            int _AdditionalLightCount;

            // Evaluate one local light at a single cloud sample.
            // Returns pre-albedo direct lighting (same space as directLight in main loop).
            float3 EvaluateLocalLightCloud(
                AtmosphericLightData light,
                float3 samplePosOS,
                float3 samplePosWS,
                float3 lightPosOS,
                float3 rayDirWS,
                float  density,
                float  heightFraction)
            {
                float3 lightPosWS = light.positionAndRange.xyz;
                float  lightRange = light.positionAndRange.w;

                // ---- World-space distance attenuation ----
                float3 toLightWS = lightPosWS - samplePosWS;
                float  distSq    = dot(toLightWS, toLightWS);
                float  rangeSq   = lightRange * lightRange;

                if (distSq > rangeSq)
                    return 0;

                float  dist         = sqrt(distSq);
                float3 toLightDirWS = toLightWS / max(dist, 0.0001);

                // URP-style smooth falloff
                float factor       = distSq / rangeSq;
                float smoothFactor = saturate(1.0 - factor * factor);
                float distAtt      = (smoothFactor * smoothFactor) / max(distSq, 1.0);

                // ---- Spot cone ----
                float spotAtt = 1.0;
                if (light.colorAndType.w > 0.5)
                {
                    float cosAngle = dot(-toLightDirWS, light.directionAndAngles.xyz);
                    float cosOuter = light.directionAndAngles.w;
                    float cosInner = light.extraParams.x;
                    spotAtt = saturate((cosAngle - cosOuter) / max(cosInner - cosOuter, 0.0001));
                    spotAtt *= spotAtt;
                }

                // ---- Object-space direction & planet occlusion ----
                float3 toLightOS    = lightPosOS - samplePosOS;
                float  distOS       = length(toLightOS);
                float3 toLightDirOS = toLightOS / max(distOS, 0.0001);

                float2 innerHitL = RaySphereIntersect(
                    samplePosOS, toLightDirOS, float3(0,0,0), _InnerRadius);
                if (innerHitL.x > 0.0 && innerHitL.x < innerHitL.y && innerHitL.x < distOS)
                    return 0;   // planet body blocks the light

                // ---- Phase function (cloud dual-lobe) ----
                float cosTheta = dot(rayDirWS, toLightDirWS);
                float phase    = DualLobePhase(cosTheta);

                // ---- Silver lining ----
                float edgeFactor   = 1.0 - pow(saturate(density * 2.0), 0.5);
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread)
                                   * _SilverLiningIntensity;

                // ---- Self-shadow: 2-step light march through cloud shell ----
                float2 outerHitL = RaySphereIntersect(
                    samplePosOS, toLightDirOS, float3(0,0,0), _OuterRadius);
                float marchEnd = max(outerHitL.y, 0.001);

                if (innerHitL.x > 0.0 && innerHitL.x < innerHitL.y)
                    marchEnd = min(marchEnd, innerHitL.x);
                marchEnd = min(marchEnd, distOS);

                float marchStep    = marchEnd / float(LOCAL_LIGHT_SHADOW_STEPS);
                float opticalDepth = 0.0;

                [unroll]
                for (int s = 0; s < LOCAL_LIGHT_SHADOW_STEPS; s++)
                {
                    float  t     = (float(s) + 0.5) * marchStep;
                    float3 lsPos = samplePosOS + toLightDirOS * t;
                    float  r     = length(lsPos);
                    if (r >= _InnerRadius && r <= _OuterRadius)
                        opticalDepth += SampleCloudDensity(lsPos, true, 0.0) * marchStep;
                }

                float lightTransmittance = exp(-opticalDepth * _CloudAbsorption);

                // ---- Powder effect ----
                float powder = 1.0 - exp(-opticalDepth * 2.0);
                powder = lerp(1.0, powder, _PowderStrength);

                // ---- Combine ----
                float3 lightColor = light.colorAndType.xyz;
                float  totalAtt   = distAtt * spotAtt;

                float3 result = lightColor * totalAtt * lightTransmittance * phase * powder;
                result += lightColor * totalAtt * lightTransmittance * silverLining
                        * edgeFactor * (0.5 + 0.5 * heightFraction);

                return result;
            }
            // ▶ END NEW

            // ============================================================
            // Structures
            // ============================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };
            
            
            // ============================================================
            // Vertex shader
            // ============================================================
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.viewDirWS  = GetWorldSpaceViewDir(posInputs.positionWS);
                output.screenPos  = ComputeScreenPos(output.positionHCS);
                
                return output;
            }
            
            // ============================================================
            // Fragment shader
            // ============================================================
            float4 frag(Varyings input) : SV_Target
            {
                float3 cameraPositionOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDirWS = normalize(-input.viewDirWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                
                float2 outerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0,0,0), _OuterRadius);
                float2 innerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0,0,0), _InnerRadius);
                
                if (outerHit.x < 0.0 && outerHit.y < 0.0)
                return float4(0, 0, 0, 0);
                
                // =========================================================
                // Build up to 2 march segments.
                // =========================================================
                float4 segments = float4(0, 0, 0, 0);
                int numSegments = 0;
                float cameraRadius = length(cameraPositionOS);
                
                if (cameraRadius > _OuterRadius)
                {
                    if (outerHit.x < 0.0)
                    return float4(0, 0, 0, 0);
                    
                    segments.x = outerHit.x;
                    if (innerHit.x > 0.0)
                    {
                        segments.y = innerHit.x;
                        segments.z = innerHit.y;
                        segments.w = outerHit.y;
                        numSegments = 2;
                    }
                    else
                    {
                        segments.y = outerHit.y;
                        numSegments = 1;
                    }
                }
                else if (cameraRadius < _InnerRadius)
                {
                    segments.x = innerHit.y;
                    segments.y = outerHit.y;
                    numSegments = 1;
                }
                else
                {
                    segments.x = 0.0;
                    if (innerHit.x > 0.0)
                    {
                        segments.y = innerHit.x;
                        segments.z = innerHit.y;
                        segments.w = outerHit.y;
                        numSegments = 2;
                    }
                    else
                    {
                        segments.y = outerHit.y;
                        numSegments = 1;
                    }
                }
                
                // ---- Scene depth occlusion ----
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepthRaw = SAMPLE_TEXTURE2D_X(
                _CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                
                float linearDepth01 = Linear01Depth(sceneDepthRaw, _ZBufferParams);
                bool hasSceneGeometry = linearDepth01 < 0.99;
                
                float sceneDistOS = 1e20;
                if (hasSceneGeometry)
                {
                    float3 sceneWorldPos = ComputeWorldSpacePosition(
                    screenUV, sceneDepthRaw, UNITY_MATRIX_I_VP);
                    float3 sceneObjectPos = TransformWorldToObject(sceneWorldPos);
                    sceneDistOS = dot(sceneObjectPos - cameraPositionOS, rayDirOS);
                }
                
                // ---- Blue noise dithering ----
                float2 blueNoiseUV = screenUV * _ScreenParams.xy / 256.0;
                blueNoiseUV = blueNoiseUV * _BlueNoiseTiling.xy + _BlueNoiseOffset.xy;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, blueNoiseUV).r;
                
                // ---- Lighting setup ----
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float3 lightColor = mainLight.color.rgb * _SunColor.rgb;
                float cosTheta = dot(rayDirOS, lightDirOS);
                float phase = DualLobePhase(cosTheta);
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread)
                * _SilverLiningIntensity;
                
                // ▶ NEW — Pre-compute additional light data
                uint localLightCount = min((uint)_AdditionalLightCount, (uint)MAX_ADDITIONAL_LIGHTS);
                float3 localLightPosOS[MAX_ADDITIONAL_LIGHTS];
                if (localLightCount > 0)
                {
                    [loop]
                    for (uint ll = 0; ll < localLightCount; ll++)
                        localLightPosOS[ll] = TransformWorldToObject(
                            _AdditionalLights[ll].positionAndRange.xyz);
                }
                // ▶ END NEW
                
                // ---- Raymarch state (persists across both segments) ----
                float transmittance = 1.0;
                float3 luminance = float3(0, 0, 0);
                int stepsPerSeg = _MaxSteps / max(numSegments, 1);
                
                // ---- March each segment ----
                for (int seg = 0; seg < 2; seg++)
                {
                    if (seg >= numSegments) break;
                    if (transmittance < 0.01) break;
                    
                    float segStart = (seg == 0) ? segments.x : segments.z;
                    float segEnd   = (seg == 0) ? segments.y : segments.w;
                    
                    if (sceneDistOS > 0.0 && sceneDistOS <= segStart)
                    break;
                    if (sceneDistOS > 0.0)
                    segEnd = min(segEnd, sceneDistOS);
                    
                    if (segStart >= segEnd)
                    continue;
                    
                    float segLength = segEnd - segStart;
                    float segStepSize = max(_StepSize, segLength / float(stepsPerSeg));
                    int segSteps = min(stepsPerSeg, max(1, int(segLength / segStepSize)));
                    
                    float ditheredStart = segStart + blueNoise * segStepSize;
                    
                    [loop]
                    for (int i = 0; i < _MaxSteps; i++)
                    {
                        if (i >= segSteps) break;
                        if (transmittance < 0.01) break;
                        
                        float t = ditheredStart + float(i) * segStepSize;
                        if (t > segEnd) break;
                        
                        float3 samplePos = cameraPositionOS + rayDirOS * t;
                        float density = SampleCloudDensity(samplePos, false, blueNoise);
                        
                        if (density > 0.001)
                        {
                            float radius = length(samplePos);
                            float heightFraction = saturate(
                            (radius - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.0001));
                            
                            float3 lightEnergy = SampleLightEnergy(
                            samplePos, lightDirOS, heightFraction, cosTheta);
                            
                            float3 ambientColor = lerp(
                            _AmbientColorBottom.rgb, _AmbientColorTop.rgb, heightFraction);
                            float3 groundBounce = _AmbientColorBottom.rgb * 0.2 * (1.0 - heightFraction);

                            float NoL = dot(normalize(samplePos), lightDirOS);
                            float dayFactor = smoothstep(-0.1, 0.3, NoL);
                            float ambientScale = lerp(0.08, 1.0, dayFactor);

                            float3 ambient = (ambientColor + groundBounce) * _AmbientLight * ambientScale;
                            
                            float3 directLight = lightColor * lightEnergy * phase;
                            
                            float edgeFactor = 1.0 - pow(saturate(density * 2.0), 0.5);
                            directLight += lightColor * lightEnergy.x * silverLining
                            * edgeFactor * (0.5 + 0.5 * heightFraction);

                            // ▶ NEW — Additional local light contributions
                            float3 additionalDirect = 0;
                            if (localLightCount > 0)
                            {
                                float3 samplePosWS = TransformObjectToWorld(samplePos);
                                [loop]
                                for (uint l = 0; l < localLightCount; l++)
                                {
                                    additionalDirect += EvaluateLocalLightCloud(
                                        _AdditionalLights[l],
                                        samplePos,
                                        samplePosWS,
                                        localLightPosOS[l],
                                        rayDirWS,
                                        density,
                                        heightFraction);
                                }
                                additionalDirect *= _LocalLightIntensity;
                            }
                            // ▶ END NEW
                            
                            float lightIntensity = dot(lightEnergy, float3(0.33, 0.33, 0.33));
                            float3 cloudAlbedo = lerp(
                            _CloudColorDark.rgb,
                            _CloudColorBright.rgb,
                            pow(saturate(lightIntensity), 0.6));
                            
                            float stepDensity = density * segStepSize;
                            float stepTransmittance = exp(-stepDensity * _LightAbsorption);
                            
                            // ▶ MODIFIED — additionalDirect added to scattering integral
                            float3 scatteringIntegral = (directLight + ambient + additionalDirect) * cloudAlbedo;
                            float3 inScattering = scatteringIntegral * (1.0 - stepTransmittance);

                            // ---- Fire emission ----
                            #ifdef _FIREENABLED_ON
                                {
                                    float3 fireEmission = SampleFireEmission(samplePos, heightFraction, density);

                                    float fireDayMask = lerp(1.0, 1.0 - dayFactor, _FireDayFade);
                                    fireEmission *= fireDayMask;

                                    inScattering += fireEmission * (1.0 - stepTransmittance);

                                    float fireLum = dot(fireEmission, float3(0.299, 0.587, 0.114));
                                    float tintAmount = saturate(fireLum * 0.3);
                                    float3 warmTint = lerp(float3(1,1,1), normalize(fireEmission + 0.001), tintAmount);
                                    inScattering *= lerp(float3(1,1,1), warmTint, tintAmount);
                                }
                            #endif

                            luminance += inScattering * transmittance;
                            transmittance *= stepTransmittance;
                        }
                    }
                }
                
                float alpha = 1.0 - transmittance;
                
                if (alpha < 0.003)
                return float4(0, 0, 0, 0);
                
                luminance = 1.0 - exp(-luminance * 1.2);
                
                return float4(luminance * alpha, alpha);
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 4.5
            #pragma multi_compile _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ============================================================
            // CBUFFER must match the main pass layout for SRP Batcher
            // ============================================================
            CBUFFER_START(UnityPerMaterial)
                float _CloudDensity;
                float _CloudCoverage;
                float _CloudScale;
                float _DetailScale;
                float _DetailStrength;
                float _ErosionStrength;
                float _Patchiness;
                float _PatchScale;
                float _Billowness;
                float _InnerRadius;
                float _OuterRadius;
                float _CloudLayerDensity;
                int   _MaxSteps;
                float _StepSize;
                int   _LightSteps;
                float _LightAbsorption;
                float _CloudAbsorption;
                float _AmbientLight;
                float _ScatteringForward;
                float _ScatteringBack;
                float _ScatteringBlend;
                float _SilverLiningIntensity;
                float _SilverLiningSpread;
                float _PowderStrength;
                float _MultiScatter;
                float4 _CloudColorBright;
                float4 _CloudColorDark;
                float4 _AmbientColorTop;
                float4 _AmbientColorBottom;
                float4 _SunColor;
                float _FireIntensity;
                float4 _FireColorBright;
                float4 _FireColorDark;
                float _FireScale;
                float _FireDetailScale;
                float _FireCoverage;
                float _FireHeightFalloff;
                float _FireAnimSpeed;
                float _FireDayFade;
                float _WindSpeed;
                float4 _WindDirection;
                float _DetailWindMultiplier;
                float4 _NoiseTiling;
                float4 _NoiseOffset;
                float4 _BlueNoiseTiling;
                float4 _BlueNoiseOffset;
                float _ShadowDensityScale;
                float _LocalLightIntensity;      // ▶ NEW (for CBUFFER matching)
            CBUFFER_END

            // Textures needed by SampleCloudDensity in the utility file
            TEXTURE3D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_BlueNoise);
            SAMPLER(sampler_BlueNoise);
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            #include "./VolumetricCloudsUtilities.hlsl"

            #define SHADOW_SAMPLES 3

            // ============================================================
            // Structures
            // ============================================================
            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
            };

            // ============================================================
            // Interleaved gradient noise
            // ============================================================
            float InterleavedGradientNoise(float2 pixelCoord)
            {
                float3 magic = float3(0.06711056, 0.00583715, 52.9829189);
                return frac(magic.z * frac(dot(pixelCoord, magic.xy)));
            }

            // ============================================================
            // Vertex
            // ============================================================
            ShadowVaryings vertShadow(ShadowAttributes input)
            {
                ShadowVaryings output;

                output.positionOS = input.positionOS.xyz;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_MainLightPosition.xyz - positionWS);
                #else
                    float3 lightDirWS = GetMainLight(0).direction;
                #endif

                output.positionHCS = TransformWorldToHClip(
                ApplyShadowBias(positionWS, normalWS, lightDirWS));

                #if UNITY_REVERSED_Z
                    output.positionHCS.z = min(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionHCS.z = max(output.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            // ============================================================
            // Fragment
            // ============================================================
            float4 fragShadow(ShadowVaryings input) : SV_Target
            {
                float3 posOS = normalize(input.positionOS) * _OuterRadius;

                float3 lightTravelOS = -normalize(TransformWorldToObjectDir(GetMainLight(0).direction));

                float2 innerHit = RaySphereIntersect(
                posOS, lightTravelOS, float3(0, 0, 0), _InnerRadius);
                float2 outerHit = RaySphereIntersect(
                posOS, lightTravelOS, float3(0, 0, 0), _OuterRadius);

                float marchEnd = (innerHit.x > 0.001) ? innerHit.x : max(outerHit.y, 0.001);

                float stepSize    = marchEnd / float(SHADOW_SAMPLES);
                float opticalDepth = 0.0;

                [unroll]
                for (int i = 0; i < SHADOW_SAMPLES; i++)
                {
                    float t = (float(i) + 0.5) * stepSize;
                    float3 samplePos = posOS + lightTravelOS * t;

                    float density = SampleCloudDensity(samplePos, true, 0.0);
                    opticalDepth += density * stepSize;
                }

                float shadowOpacity = 1.0 - exp(-opticalDepth * _ShadowDensityScale);

                float dither = InterleavedGradientNoise(input.positionHCS.xy);
                clip(shadowOpacity - dither);

                return 0;
            }

            ENDHLSL
        }
    }
    
    FallBack Off
}