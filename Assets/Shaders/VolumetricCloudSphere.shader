Shader "Custom/VolumetricCloudSphere"
{
    Properties
    {
        [Header(Cloud Shape)]
        _CloudDensity ("Cloud Density", Range(0, 20)) = 1.5
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.35
        _CloudScale ("Cloud Scale", Range(0.1, 20)) = 2.0
        _DetailScale ("Detail Scale", Range(1, 10)) = 4.0
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.35
        _ErosionStrength ("Erosion Strength", Range(0, 1)) = 0.3
        _Patchiness ("Patchiness", Range(0, 1)) = 0.6
        _PatchScale ("Patch Scale", Range(0.1, 5)) = 0.8
        
        [Header(Sphere Settings)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.48
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.52
        
        [Header(Raymarching)]
        _MaxSteps ("Max Steps", Range(8, 256)) = 128
        _StepSize ("Step Size", Range(0.001, 0.05)) = 0.005
        
        [Header(Lighting)]
        _LightAbsorption ("Light Absorption", Range(0, 5)) = 1.2
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.4
        _ScatteringForward ("Forward Scattering", Range(0, 0.95)) = 0.8
        _ScatteringBack ("Back Scattering", Range(0, 0.95)) = 0.3
        _ScatteringBlend ("Scattering Blend", Range(0, 1)) = 0.6
        _SilverLiningIntensity ("Silver Lining", Range(0, 2)) = 0.5
        _SilverLiningSpread ("Silver Lining Spread", Range(1, 10)) = 4.0
        _PowderStrength ("Powder Effect", Range(0, 1)) = 0.3
        
        [Header(Color)]
        _CloudColorBright ("Cloud Color Bright", Color) = (1, 1, 1, 1)
        _CloudColorDark ("Cloud Color Dark", Color) = (0.6, 0.65, 0.7, 1)
        _AmbientColorTop ("Ambient Color Top", Color) = (0.7, 0.8, 1.0, 1)
        _AmbientColorBottom ("Ambient Color Bottom", Color) = (0.5, 0.5, 0.55, 1)
        
        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 0.2)) = 0.02
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.3, 0)
        
        [Header(Textures)]
        _NoiseTexture ("3D Noise Texture", 3D) = "white" {}
        _BlueNoise ("Blue Noise", 2D) = "gray" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "VolumetricCloudPass"
            
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            float _CloudDensity;
            float _CloudCoverage;
            float _CloudScale;
            float _DetailScale;
            float _DetailStrength;
            float _ErosionStrength;
            float _Patchiness;
            float _PatchScale;
            float _InnerRadius;
            float _OuterRadius;
            int _MaxSteps;
            float _StepSize;
            float _LightAbsorption;
            float _AmbientLight;
            float _ScatteringForward;
            float _ScatteringBack;
            float _ScatteringBlend;
            float _SilverLiningIntensity;
            float _SilverLiningSpread;
            float _PowderStrength;
            float4 _CloudColorBright;
            float4 _CloudColorDark;
            float4 _AmbientColorTop;
            float4 _AmbientColorBottom;
            float _WindSpeed;
            float4 _WindDirection;
            
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
            };

            struct FragmentOutput
            {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };
            
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                denom = max(denom, 0.0001);
                return (1.0 - g2) / (4.0 * PI * pow(denom, 1.5));
            }
            
            float DualLobePhase(float cosTheta)
            {
                float forward = HenyeyGreenstein(cosTheta, _ScatteringForward);
                float back = HenyeyGreenstein(cosTheta, -_ScatteringBack);
                float phase = lerp(back, forward, _ScatteringBlend);
                return max(phase, 0.05); // Ensure minimum visibility
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
            
            // Improved height gradient - more realistic cumulus shape
            float GetHeightGradient(float heightFraction)
            {
                // Smooth bottom, bulging middle, wispy top
                float bottom = smoothstep(0.0, 0.2, heightFraction);
                float top = 1.0 - smoothstep(0.6, 1.0, heightFraction);
                
                // Add some extra density in the lower-middle region
                float bulge = 1.0 - abs(heightFraction - 0.35) * 1.5;
                bulge = saturate(bulge);
                
                return bottom * top * (0.7 + 0.3 * bulge);
            }
            
            // Convert position to spherical UV for more uniform sampling
            float3 GetSphericalUV(float3 pos)
            {
                float r = length(pos);
                float3 normalized = pos / max(r, 0.0001);
                
                // Spherical coordinates
                float theta = atan2(normalized.z, normalized.x); // -PI to PI
                float phi = acos(clamp(normalized.y, -1.0, 1.0)); // 0 to PI
                
                // Normalize to 0-1 range and tile
                float u = (theta + PI) / (2.0 * PI);
                float v = phi / PI;
                
                return float3(u, v, r);
            }
            
            float SampleCloudDensity(float3 positionOS, bool cheap)
            {
                float radius = length(positionOS);
                
                if (radius < _InnerRadius || radius > _OuterRadius)
                return 0.0;
                
                float heightFraction = saturate((radius - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.0001));
                float heightGradient = GetHeightGradient(heightFraction);
                
                // Wind animation
                float3 windDir = normalize(_WindDirection.xyz + float3(0.001, 0, 0));
                float3 windOffset = windDir * _Time.y * _WindSpeed;
                
                // Use a hybrid UV approach to reduce polar artifacts
                float3 sphericalUV = GetSphericalUV(positionOS);
                
                // Large-scale patch/weather map to create cloud-free areas
                float3 patchUV = positionOS * _PatchScale + windOffset * 0.3;
                float4 patchNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, patchUV, 2); // Sample at lower mip for smoothness
                float patchValue = patchNoise.r * 0.6 + patchNoise.g * 0.3 + patchNoise.b * 0.1;
                
                // Create distinct cloudy vs clear regions
                float weatherMask = smoothstep(0.3, 0.7, patchValue);
                weatherMask = lerp(1.0, weatherMask, _Patchiness);
                
                // Early out if in a clear region
                if (weatherMask < 0.1)
                return 0.0;
                
                // Base shape noise - use world position for consistent look
                float3 baseUVW = positionOS * _CloudScale + windOffset;
                float4 baseNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, baseUVW, 0);
                
                // FBM from noise channels
                float baseShape = baseNoise.r * 0.5 + baseNoise.g * 0.3 + baseNoise.b * 0.15 + baseNoise.a * 0.05;
                
                // Combine with height gradient and weather
                float shapedNoise = baseShape * heightGradient * weatherMask;
                
                // Apply coverage threshold with soft edge
                float coverageThreshold = 1.0 - _CloudCoverage;
                float baseDensity = smoothstep(coverageThreshold - 0.1, coverageThreshold + 0.2, shapedNoise);
                
                if (cheap || baseDensity <= 0.01)
                return baseDensity * _CloudDensity;
                
                // Detail noise for erosion at edges
                float3 detailUVW = positionOS * _CloudScale * _DetailScale + windOffset * 1.2;
                float4 detailNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, detailUVW, 0);
                float detailFBM = detailNoise.r * 0.5 + detailNoise.g * 0.3 + detailNoise.b * 0.2;
                
                // Edge erosion - more at cloud edges, less at core
                float edgeFactor = 1.0 - baseDensity; // More erosion at edges
                float erosion = _ErosionStrength * detailFBM * (0.5 + 0.5 * edgeFactor);
                
                float finalDensity = saturate(baseDensity - erosion);
                
                // Detail modulation
                float detailMod = lerp(1.0, 0.7 + 0.6 * detailFBM, _DetailStrength * (1.0 - heightFraction * 0.5));
                finalDensity *= detailMod;
                
                return finalDensity * _CloudDensity;
            }
            
            float BeerPowder(float density, float heightFraction)
            {
                float beer = exp(-density * _LightAbsorption);
                // Powder effect stronger in lower parts of cloud
                float powder = 1.0 - exp(-density * _LightAbsorption * 2.0);
                powder = lerp(powder, 1.0, heightFraction * 0.5);
                return beer * lerp(1.0, powder, _PowderStrength);
            }
            
            float SampleLightEnergy(float3 positionOS, float3 lightDirOS, float heightFraction)
            {
                const int LIGHT_STEPS = 5;
                float totalDensity = 0.0;
                
                // Adaptive step size based on shell thickness
                float shellThickness = _OuterRadius - _InnerRadius;
                float stepSize = shellThickness * 0.4 / float(LIGHT_STEPS);
                
                for (int i = 0; i < LIGHT_STEPS; i++)
                {
                    float t = (float(i) + 0.5) * stepSize;
                    float3 samplePos = positionOS + lightDirOS * t;
                    
                    float density = SampleCloudDensity(samplePos, true);
                    totalDensity += density * stepSize;
                }
                
                return BeerPowder(totalDensity, heightFraction);
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
                
                return output;
            }
            
            FragmentOutput frag(Varyings input)
            {
                float3 cameraPositionOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDirWS = normalize(-input.viewDirWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                
                float2 outerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _OuterRadius);
                float2 innerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _InnerRadius);
                
                if (outerHit.x < 0.0 && outerHit.y < 0.0)
                discard;
                
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
                discard;
                
                // Blue noise dithering
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, screenUV * _ScreenParams.xy / 128.0).r;
                rayStart += (blueNoise - 0.5) * _StepSize * 2.0;
                
                // Light setup
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float cosTheta = dot(rayDirOS, lightDirOS);
                float phase = DualLobePhase(cosTheta);
                
                // Silver lining
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread) * _SilverLiningIntensity;
                
                // Raymarching
                float transmittance = 1.0;
                float3 luminance = float3(0, 0, 0);
                
                float rayLength = rayEnd - rayStart;
                float dynamicStepSize = max(_StepSize, rayLength / float(_MaxSteps));
                int steps = int(min(float(_MaxSteps), rayLength / dynamicStepSize));
                
                for (int i = 0; i < steps; i++)
                {
                    if (transmittance < 0.01)
                    break;
                    
                    float t = rayStart + (float(i) + blueNoise) * dynamicStepSize;
                    if (t > rayEnd)
                    break;
                    
                    float3 samplePos = cameraPositionOS + rayDirOS * t;
                    float density = SampleCloudDensity(samplePos, false);
                    
                    if (density > 0.001)
                    {
                        float radius = length(samplePos);
                        float heightFraction = saturate((radius - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.0001));
                        
                        // Lighting
                        float lightEnergy = SampleLightEnergy(samplePos, lightDirOS, heightFraction);
                        
                        // Ambient with height gradient
                        float3 ambientColor = lerp(_AmbientColorBottom.rgb, _AmbientColorTop.rgb, heightFraction);
                        float3 ambient = ambientColor * _AmbientLight;
                        
                        // Direct lighting
                        float3 directLight = mainLight.color.rgb * lightEnergy * phase;
                        directLight += mainLight.color.rgb * lightEnergy * silverLining * (1.0 - heightFraction);
                        
                        // Cloud color
                        float3 cloudColor = lerp(_CloudColorDark.rgb, _CloudColorBright.rgb, pow(lightEnergy, 0.7));
                        
                        // Integrate
                        float sampleDensity = density * dynamicStepSize;
                        float sampleTransmittance = exp(-sampleDensity * _LightAbsorption);
                        
                        float3 sampleLighting = (directLight + ambient) * cloudColor;
                        float3 integScatter = sampleLighting * (1.0 - sampleTransmittance);
                        
                        luminance += integScatter * transmittance;
                        transmittance *= sampleTransmittance;
                    }
                }
                
                float alpha = 1.0 - transmittance;
                
                if (alpha < 0.005)
                discard;
                
                // Slight tone mapping to prevent blowout
                luminance = luminance / (luminance + 0.5);

                // Apply Normal dot Light for subtle shading
                float NdotL = saturate(dot(input.normalWS, GetMainLight().direction));
                luminance *= NdotL;

                luminance = pow(luminance, 1.0 / 2.2); // Gamma correction

                FragmentOutput output = (FragmentOutput)0;
                output.color = float4(luminance, alpha);
                output.depth = WorldToDepth(input.positionWS);
                
                return output;
            }
            
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}