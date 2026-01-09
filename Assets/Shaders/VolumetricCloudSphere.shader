// © Haochen Zhang, 2025. All rights reserved. No part of this VolumetricCloudSphere.shader may be reproduced, distributed, displayed, sold, or used in any commercial or non-commercial project without prior written permission. This Work may not be used to train AI or machine learning models, nor minted as an NFT.

Shader "Custom/VolumetricCloudSphere"
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
        _CloudLayerDensity ("Layer Density Falloff", Range(0.1, 5)) = 1.5
        
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
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "VolumetricCloudPass"
            
            // Cull Off
            // ZWrite Off
            // Blend One OneMinusSrcAlpha
            Cull Off
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha One
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Properties
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
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float3 objectCenter : TEXCOORD5;
            };

            struct FragmentOutput
            {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };
            
            // Improved Henyey-Greenstein with better normalization
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
            }
            
            // Dual-lobe phase function with Schlick approximation for speed
            float DualLobePhase(float cosTheta)
            {
                float forward = HenyeyGreenstein(cosTheta, _ScatteringForward);
                float back = HenyeyGreenstein(cosTheta, -_ScatteringBack);
                float phase = lerp(back, forward, _ScatteringBlend);
                
                // Add a constant term for multi-scattering approximation
                float multiScatter = 0.25 / PI;
                phase = lerp(phase, multiScatter, _MultiScatter * 0.5);
                
                return max(phase, 0.03);
            }
            
            float2 RaySphereIntersect(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius)
            {
                float3 oc = rayOrigin - sphereCenter;
                float b = dot(oc, rayDir);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b * b - c;
                
                if (discriminant < 0.0)
                    return float2(-1.0, -1.0);
                
                float sqrtDisc = sqrt(discriminant);
                return float2(-b - sqrtDisc, -b + sqrtDisc);
            }

            // Height-based density gradient for realistic cumulus clouds
            float GetHeightGradient(float heightFraction, float cloudType)
            {
                // Cumulus-like: dense at bottom, fluffy top
                float cumulus = saturate(Remap(heightFraction, 0.0, 0.1, 0.0, 1.0)) 
                              * saturate(Remap(heightFraction, 0.2, 0.5, 1.0, 0.9))
                              * saturate(Remap(heightFraction, 0.5, 1.0, 0.9, 0.0));
                
                // Stratus-like: flatter distribution
                float stratus = saturate(Remap(heightFraction, 0.0, 0.1, 0.0, 1.0))
                              * saturate(Remap(heightFraction, 0.3, 0.95, 1.0, 0.0));
                
                return lerp(stratus, cumulus, cloudType);
            }
            
            // Improved 3D UV sampling to reduce polar stretching
            float3 GetCloudUV(float3 posOS, float scale)
            {
                // Triplanar-ish approach with spherical blend
                float3 normalizedPos = normalize(posOS);
                float radius = length(posOS);
                
                // Use position directly but with spherical height encoding
                float3 uvw = posOS * scale;
                
                // Add subtle spherical distortion to break up patterns
                uvw += normalizedPos * sin(radius * 20.0) * 0.02;
                
                return uvw * _NoiseTiling.xyz + _NoiseOffset.xyz;
            }
            
            float SampleCloudDensity(float3 positionOS, bool cheap, float blueNoise)
            {
                float radius = length(positionOS);
                
                // Early out for outside cloud layer
                if (radius < _InnerRadius || radius > _OuterRadius)
                    return 0.0;
                
                float shellThickness = _OuterRadius - _InnerRadius;
                float heightFraction = saturate((radius - _InnerRadius) / max(shellThickness, 0.0001));
                
                // Wind animation
                float3 windDir = normalize(_WindDirection.xyz + float3(0.0001, 0, 0));
                float time = _Time.y * _WindSpeed;
                float3 windOffset = windDir * time;
                
                // Large-scale weather/coverage map
                float3 weatherUV = positionOS * _PatchScale * 0.5 + windOffset * 0.2;
                float4 weatherNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, weatherUV, 3);
                float weatherValue = weatherNoise.r * 0.6 + weatherNoise.g * 0.25 + weatherNoise.a * 0.15;
                
                // Create distinct cloud patches
                float weatherMask = smoothstep(0.35, 0.65, weatherValue);
                weatherMask = lerp(1.0, weatherMask, _Patchiness);
                
                // Very early out for clear regions
                if (weatherMask < 0.05)
                    return 0.0;
                
                // Cloud type variation (affects height gradient)
                float cloudType = saturate(weatherNoise.b * 0.7 + 0.3);
                float heightGradient = GetHeightGradient(heightFraction, lerp(0.3, 0.8, cloudType));
                
                // Base shape noise
                float3 baseUV = GetCloudUV(positionOS, _CloudScale) + windOffset;
                float4 baseNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, baseUV, 0);
                
                // Perlin-Worley blend (R channel is already Perlin-Worley in your compute shader)
                float baseShape = baseNoise.r;
                
                // Add lower frequency variations
                float3 lowFreqUV = positionOS * _CloudScale * 0.4 + windOffset * 0.5;
                float4 lowFreqNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, lowFreqUV, 1);
                float lowFreq = lowFreqNoise.r * 0.5 + lowFreqNoise.g * 0.3 + lowFreqNoise.b * 0.2;
                
                // Combine base shapes with billowing effect
                baseShape = lerp(baseShape, baseShape * lowFreq * 2.0, _Billowness * 0.5);
                baseShape = saturate(baseShape);
                
                // Apply height and weather
                float shapedDensity = baseShape * heightGradient * weatherMask;
                
                // Coverage threshold with soft edges
                float coverageMin = 1.0 - _CloudCoverage;
                float coverageMax = coverageMin + 0.2;
                float baseDensity = smoothstep(coverageMin, coverageMax, shapedDensity);
                
                // Early out for cheap samples (light marching)
                if (cheap)
                    return baseDensity * _CloudDensity * _CloudLayerDensity;
                
                // Detail erosion for fluffy edges
                if (baseDensity > 0.01)
                {
                    float3 detailUV = GetCloudUV(positionOS, _CloudScale * _DetailScale) + windOffset * _DetailWindMultiplier;
                    float4 detailNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, detailUV, 0);
                    
                    // Use G and B channels for detail (different Worley frequencies)
                    float detailFBM = detailNoise.g * 0.5 + detailNoise.b * 0.35 + detailNoise.a * 0.15;
                    
                    // More erosion at cloud edges, height-dependent
                    float edgeFactor = 1.0 - pow(baseDensity, 0.5);
                    float heightErosion = lerp(0.3, 1.0, heightFraction); // More erosion at top
                    float erosion = _ErosionStrength * detailFBM * edgeFactor * heightErosion;
                    
                    baseDensity = saturate(baseDensity - erosion);
                    
                    // Detail modulation for interior texture
                    float detailMod = lerp(1.0, 0.75 + 0.5 * detailFBM, _DetailStrength);
                    baseDensity *= detailMod;
                }
                
                // Height-based density falloff
                float densityFalloff = lerp(1.0, 0.3, pow(heightFraction, 1.5));
                
                return baseDensity * _CloudDensity * densityFalloff;
            }
            
            // Beer-Lambert with powder effect for realistic cloud lighting
            float BeerPowder(float density, float cosTheta, float heightFraction)
            {
                float beer = exp(-density * _CloudAbsorption);
                
                // Powder effect - darkening when looking away from light through thin clouds
                float powder = 1.0 - exp(-density * _CloudAbsorption * 2.0);
                powder = lerp(powder, 1.0, saturate(heightFraction * 0.8));
                
                // Apply powder effect more when looking away from light
                float powderBlend = _PowderStrength * saturate(-cosTheta * 0.5 + 0.5);
                
                return beer * lerp(1.0, powder, powderBlend);
            }
            
            float3 SampleLightEnergy(float3 positionOS, float3 lightDirOS, float heightFraction, float cosTheta)
            {
                float totalDensity = 0.0;
                float shellThickness = _OuterRadius - _InnerRadius;
                
                // Cone sampling - slight offset to simulate light scatter
                float3 perpDir = normalize(cross(lightDirOS, float3(0, 1, 0.001)));
                
                // Adaptive step size
                float stepSize = shellThickness * 0.5 / float(_LightSteps);
                
                for (int i = 0; i < _LightSteps; i++)
                {
                    float t = (float(i) + 0.5) * stepSize;
                    
                    // Slight cone spread for softer shadows
                    float coneRadius = t * 0.05 * (1.0 + float(i) * 0.1);
                    float3 coneOffset = perpDir * coneRadius * sin(float(i) * 2.39996);
                    
                    float3 samplePos = positionOS + lightDirOS * t + coneOffset;
                    float density = SampleCloudDensity(samplePos, true, 0.5);
                    
                    // Exponential weighting - closer samples matter more
                    float weight = exp(-float(i) * 0.15);
                    totalDensity += density * stepSize * weight;
                }
                
                float lightEnergy = BeerPowder(totalDensity, cosTheta, heightFraction);
                
                // Multi-scattering approximation - light bounces inside cloud
                float multiScatterEnergy = exp(-totalDensity * _CloudAbsorption * 0.25);
                float3 multiScatter = lerp(float3(0.5, 0.6, 0.7), float3(1, 1, 1), multiScatterEnergy) * _MultiScatter;
                
                return lightEnergy + multiScatter * 0.15;
            }

            float WorldToDepth(float3 worldPos)
            {
                float4 clipPos = TransformWorldToHClip(worldPos);
                #if UNITY_REVERSED_Z
                    return clipPos.z / clipPos.w;
                #else
                    return (clipPos.z / clipPos.w) * 0.5 + 0.5;
                #endif
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.objectCenter = TransformObjectToWorld(float3(0, 0, 0));
                
                return output;
            }
            
            FragmentOutput frag(Varyings input)
            {
                FragmentOutput output;
                
                float3 cameraPositionOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDirWS = normalize(-input.viewDirWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                
                // Ray-sphere intersection
                float2 outerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _OuterRadius);
                float2 innerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _InnerRadius);
                
                if (outerHit.x < 0.0 && outerHit.y < 0.0)
                {
                    output.color = float4(0, 0, 0, 0);
                    return output;
                }
                
                float rayStart, rayEnd;
                float cameraRadius = length(cameraPositionOS);
                
                if (cameraRadius > _OuterRadius)
                {
                    rayStart = outerHit.x;
                    rayEnd = (innerHit.x > 0.0) ? innerHit.x : outerHit.y;
                }
                else if (cameraRadius < _InnerRadius)
                {
                    rayStart = innerHit.y;
                    rayEnd = outerHit.y;
                }
                else
                {
                    rayStart = 0.0;
                    rayEnd = (innerHit.x > 0.0) ? innerHit.x : outerHit.y;
                }
                
                if (rayStart >= rayEnd)
                {
                    output.color = float4(0, 0, 0, 0);
                    return output;
                }
                
                // Blue noise dithering for temporal stability
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 blueNoiseUV = screenUV * _ScreenParams.xy / 256.0;
                blueNoiseUV = blueNoiseUV * _BlueNoiseTiling.xy + _BlueNoiseOffset.xy;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, blueNoiseUV).r;
                
                // Jitter ray start
                rayStart += blueNoise * _StepSize;
                
                // Lighting setup
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float3 lightColor = mainLight.color.rgb * _SunColor.rgb;
                
                float cosTheta = dot(rayDirOS, lightDirOS);
                float phase = DualLobePhase(cosTheta);
                
                // Silver lining (bright edge when looking toward sun)
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread) * _SilverLiningIntensity;
                
                // Raymarching
                float transmittance = 1.0;
                float3 luminance = float3(0, 0, 0);
                float depthAccum = 0.0;
                float hitDepth = 0.0;
                bool firstHit = true;
                
                float rayLength = rayEnd - rayStart;
                float dynamicStepSize = max(_StepSize, rayLength / float(_MaxSteps));
                int actualSteps = min(_MaxSteps, int(rayLength / dynamicStepSize));
                
                // Accumulated density for depth estimation
                float totalDensity = 0.0;
                
                for (int i = 0; i < actualSteps; i++)
                {
                    if (transmittance < 0.01)
                        break;
                    
                    float t = rayStart + (float(i) + blueNoise) * dynamicStepSize;
                    if (t > rayEnd)
                        break;
                    
                    float3 samplePos = cameraPositionOS + rayDirOS * t;
                    float density = SampleCloudDensity(samplePos, false, blueNoise);
                    
                    if (density > 0.001)
                    {
                        // Track first hit for depth
                        if (firstHit)
                        {
                            hitDepth = t;
                            firstHit = false;
                        }
                        
                        float radius = length(samplePos);
                        float heightFraction = saturate((radius - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.0001));
                        
                        // Sample lighting toward sun
                        float3 lightEnergy = SampleLightEnergy(samplePos, lightDirOS, heightFraction, cosTheta);
                        
                        // Height-based ambient color
                        float3 ambientColor = lerp(_AmbientColorBottom.rgb, _AmbientColorTop.rgb, heightFraction);
                        
                        // Ground bounce approximation
                        float3 groundBounce = _AmbientColorBottom.rgb * 0.2 * (1.0 - heightFraction);
                        
                        float3 ambient = (ambientColor + groundBounce) * _AmbientLight;
                        
                        // Direct lighting with phase function
                        float3 directLight = lightColor * lightEnergy * phase;
                        
                        // Add silver lining more at cloud edges
                        float edgeFactor = 1.0 - pow(saturate(density * 2.0), 0.5);
                        directLight += lightColor * lightEnergy.x * silverLining * edgeFactor * (0.5 + 0.5 * heightFraction);
                        
                        // Cloud albedo based on lighting
                        float lightIntensity = dot(lightEnergy, float3(0.33, 0.33, 0.33));
                        float3 cloudAlbedo = lerp(_CloudColorDark.rgb, _CloudColorBright.rgb, pow(saturate(lightIntensity), 0.6));
                        
                        // Integrate scattering
                        float stepDensity = density * dynamicStepSize;
                        float stepTransmittance = exp(-stepDensity * _LightAbsorption);
                        
                        // Energy-conserving scattering integration
                        float3 scatteringIntegral = (directLight + ambient) * cloudAlbedo;
                        float3 inScattering = scatteringIntegral * (1.0 - stepTransmittance);
                        
                        luminance += inScattering * transmittance;
                        transmittance *= stepTransmittance;
                        
                        totalDensity += stepDensity;
                    }
                }
                
                float alpha = 1.0 - transmittance;
                
                if (alpha < 0.003)
                {
                    output.color = float4(0, 0, 0, 0);
                    output.depth = WorldToDepth(_WorldSpaceCameraPos.xyz + rayDirWS * hitDepth);
                    return output;
                }
                
                // Tone mapping to prevent overexposure
                luminance = 1.0 - exp(-luminance * 1.2);
                
                // Premultiplied alpha output
                output.color = float4(luminance * alpha, alpha);

                // Gamma correction
                output.color.rgb = pow(output.color.rgb, 1.0 / 2.2);
                // Light attenuation based on normal for soft appearance
                float NoL = saturate(dot(input.normalWS, mainLight.direction));
                output.color.rgb *= clamp(NoL, 0.0, 1.0);

                float3 endPos = _WorldSpaceCameraPos.xyz + rayDirWS * hitDepth;
                
                output.depth = WorldToDepth(endPos);
                return output;
            }
            
            ENDHLSL
        }
    }
    
    FallBack Off
}