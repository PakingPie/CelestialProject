Shader "Custom/VolumetricCloudsSphere"
{
    Properties
    {
        [Header(Cloud Shape)]
        _CloudDensity ("Cloud Density", Range(0, 50)) = 8.0
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
    }
    
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "VolumetricCloudPass"
            
            // [FIX 5] Back-face only, no depth test, no depth write
            Cull Front
            ZTest Always
            ZWrite Off
            // [FIX 3] Correct premultiplied-alpha blend (was SrcAlpha One)
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

                // Fire effect properties (currently unused in shader code, but defined for future implementation)
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
                
                TEXTURE3D(_NoiseTexture);
                SAMPLER(sampler_NoiseTexture);
                TEXTURE2D(_BlueNoise);
                SAMPLER(sampler_BlueNoise);
                
                // [FIX 7] Depth buffer for scene occlusion
                TEXTURE2D_X_FLOAT(_CameraDepthTexture);
                SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_END

            #include "./VolumetricCloudsUtilities.hlsl"
            
            // ============================================================
            // Structures (cleaned — removed unused interpolators)
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
            // Vertex shader (slimmed — only outputs what fragment needs)
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
            // [FIX 2] Removed NoL post-multiply (was destroying volumetric lighting)
            // [FIX 5] Removed SV_Depth output
            // [FIX 6] Removed manual pow(1/2.2) gamma (URP handles this)
            // [FIX 7] Added depth buffer occlusion
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
                //
                //   Outside, ray hits inner sphere:
                //     Seg 0: outerHit.x  → innerHit.x   (near shell)
                //     Seg 1: innerHit.y  → outerHit.y   (far shell)  ← WAS MISSING
                //
                //   Outside, grazing (no inner hit):
                //     Seg 0: outerHit.x  → outerHit.y
                //
                //   Inside inner sphere:
                //     Seg 0: innerHit.y  → outerHit.y
                //
                //   Inside shell, looking inward:
                //     Seg 0: 0           → innerHit.x
                //     Seg 1: innerHit.y  → outerHit.y   (far shell)
                //
                //   Inside shell, looking outward:
                //     Seg 0: 0           → outerHit.y
                // =========================================================
                float4 segments = float4(0, 0, 0, 0); // (start0, end0, start1, end1)
                int numSegments = 0;
                float cameraRadius = length(cameraPositionOS);
                
                if (cameraRadius > _OuterRadius)
                {
                    if (outerHit.x < 0.0)
                    return float4(0, 0, 0, 0);
                    
                    segments.x = outerHit.x;
                    if (innerHit.x > 0.0)
                    {
                        segments.y = innerHit.x;       // near shell ends at inner sphere
                        segments.z = innerHit.y;       // far shell starts where ray exits inner sphere
                        segments.w = outerHit.y;       // far shell ends at outer sphere exit
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
                
                // Check if there is actual scene geometry (skip far-plane / sky pixels)
                float linearDepth01 = Linear01Depth(sceneDepthRaw, _ZBufferParams);
                bool hasSceneGeometry = linearDepth01 < 0.99;
                
                float sceneDistOS = 1e20; // default: nothing blocking
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
                    
                    // Depth occlusion: if scene geometry is in front of this segment,
                    // this segment and everything beyond is hidden.
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
                            float dayFactor = smoothstep(-0.1, 0.3, NoL);     // soft terminator ramp
                            float ambientScale = lerp(0.08, 1.0, dayFactor);   // night side keeps ~8% ambient

                            float3 ambient = (ambientColor + groundBounce) * _AmbientLight * ambientScale;
                            
                            float3 directLight = lightColor * lightEnergy * phase;
                            
                            float edgeFactor = 1.0 - pow(saturate(density * 2.0), 0.5);
                            directLight += lightColor * lightEnergy.x * silverLining
                            * edgeFactor * (0.5 + 0.5 * heightFraction);
                            
                            float lightIntensity = dot(lightEnergy, float3(0.33, 0.33, 0.33));
                            float3 cloudAlbedo = lerp(
                            _CloudColorDark.rgb,
                            _CloudColorBright.rgb,
                            pow(saturate(lightIntensity), 0.6));
                            
                            float stepDensity = density * segStepSize;
                            float stepTransmittance = exp(-stepDensity * _LightAbsorption);
                            
                            float3 scatteringIntegral = (directLight + ambient) * cloudAlbedo;
                            float3 inScattering = scatteringIntegral * (1.0 - stepTransmittance);

                            // ---- Fire emission ----
                            #ifdef _FIREENABLED_ON
                                {
                                    float3 fireEmission = SampleFireEmission(samplePos, heightFraction, density);

                                    // Fire glow fades on sun-lit side (hard to see glow in daylight)
                                    float fireDayMask = lerp(1.0, 1.0 - dayFactor, _FireDayFade);
                                    fireEmission *= fireDayMask;

                                    // Emission contribution: weighted by optical depth of this step
                                    // and accumulated transmittance (same integration as scattering)
                                    inScattering += fireEmission * (1.0 - stepTransmittance);

                                    // Fire tints cloud albedo toward warm hues where emission is strong
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
    }
    
    FallBack Off
}