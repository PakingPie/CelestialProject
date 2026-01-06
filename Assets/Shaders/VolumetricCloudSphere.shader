Shader "Custom/VolumetricCloudSphere"
{
    Properties
    {
        [Header(Cloud Shape)]
        _CloudDensity ("Cloud Density", Range(0, 5)) = 2.0
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudScale ("Cloud Scale", Range(0.1, 20)) = 4.0
        _DetailScale ("Detail Scale", Range(1, 10)) = 3.0
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.3
        _ErosionStrength ("Erosion Strength", Range(0, 1)) = 0.4
        
        [Header(Sphere Settings)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.4
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.5
        
        [Header(Raymarching)]
        _MaxSteps ("Max Steps", Range(8, 256)) = 128
        _StepSize ("Step Size", Range(0.001, 0.05)) = 0.01
        
        [Header(Lighting)]
        _LightAbsorption ("Light Absorption", Range(0, 5)) = 1.5
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.3
        _ScatteringForward ("Forward Scattering", Range(0, 0.99)) = 0.7
        _ScatteringBack ("Back Scattering", Range(0, 0.99)) = 0.3
        _ScatteringBlend ("Scattering Blend", Range(0, 1)) = 0.5
        _SilverLiningIntensity ("Silver Lining", Range(0, 2)) = 0.8
        _SilverLiningSpread ("Silver Lining Spread", Range(1, 10)) = 3.0
        
        [Header(Color)]
        _CloudColorBright ("Cloud Color Bright", Color) = (1, 1, 1, 1)
        _CloudColorDark ("Cloud Color Dark", Color) = (0.4, 0.45, 0.5, 1)
        _AmbientColorTop ("Ambient Color Top", Color) = (0.6, 0.7, 0.9, 1)
        _AmbientColorBottom ("Ambient Color Bottom", Color) = (0.4, 0.4, 0.5, 1)
        
        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 0.5)) = 0.05
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.5, 0)
        
        [Header(Textures)]
        _NoiseTexture ("3D Noise Texture", 3D) = "white" {}
        _BlueNoise ("Blue Noise", 2D) = "gray" {}
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
            
            Blend SrcAlpha One
            ZWrite Off
            Cull Front
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Properties
            float _CloudDensity;
            float _CloudCoverage;
            float _CloudScale;
            float _DetailScale;
            float _DetailStrength;
            float _ErosionStrength;
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
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
            };
            
            // // Remap value from one range to another
            // float Remap(float value, float oldMin, float oldMax, float newMin, float newMax)
            // {
            //     return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
            // }
            
            // Henyey-Greenstein phase function for scattering
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
            }
            
            // Dual-lobe phase function
            float DualLobePhase(float cosTheta)
            {
                float forward = HenyeyGreenstein(cosTheta, _ScatteringForward);
                float back = HenyeyGreenstein(cosTheta, -_ScatteringBack);
                return lerp(back, forward, _ScatteringBlend);
            }
            
            // Ray-sphere intersection
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
            
            // Height gradient for realistic cloud shaping
            float GetHeightGradient(float heightFraction)
            {
                // Creates puffy cumulus-like shape: thin at bottom, thick in middle, wispy at top
                float bottomFade = saturate(Remap(heightFraction, 0.0, 0.15, 0.0, 1.0));
                float topFade = saturate(Remap(heightFraction, 0.7, 1.0, 1.0, 0.0));
                return bottomFade * topFade;
            }
            
            // Sample cloud density with multiple noise octaves
            float SampleCloudDensity(float3 positionOS, bool cheap)
            {
                float radius = length(positionOS);
                
                // Early out if outside cloud shell
                if (radius < _InnerRadius || radius > _OuterRadius)
                    return 0.0;
                
                float3 normalizedPos = positionOS / radius;
                float heightFraction = saturate((radius - _InnerRadius) / (_OuterRadius - _InnerRadius));
                
                // Height-based density
                float heightGradient = GetHeightGradient(heightFraction);
                
                // Animated wind offset
                float3 windOffset = normalize(_WindDirection.xyz) * _Time.y * _WindSpeed;
                
                // Base shape noise (large scale)
                float3 baseUVW = positionOS * _CloudScale + windOffset;
                float4 baseNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, baseUVW, 0);
                
                // Build FBM from noise channels (assuming RGBA contains different octaves)
                float baseShape = baseNoise.r * 0.625 + baseNoise.g * 0.25 + baseNoise.b * 0.125;
                
                // Apply height gradient to base shape
                float baseDensity = Remap(baseShape * heightGradient, 1.0 - _CloudCoverage, 1.0, 0.0, 1.0);
                baseDensity = saturate(baseDensity);
                
                // Early out for cheap samples (light marching)
                if (cheap || baseDensity <= 0.0)
                    return baseDensity * _CloudDensity;
                
                // Detail noise for erosion (smaller scale)
                float3 detailUVW = positionOS * _CloudScale * _DetailScale + windOffset * 1.5;
                float4 detailNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, detailUVW, 0);
                
                float detailFBM = detailNoise.r * 0.625 + detailNoise.g * 0.25 + detailNoise.b * 0.125;
                
                // Erode base shape with detail
                // More erosion at edges (low density), less at core (high density)
                float erosionModifier = lerp(detailFBM, 1.0 - detailFBM, saturate(heightFraction * 2.0));
                float erosion = _ErosionStrength * erosionModifier;
                
                float finalDensity = saturate(Remap(baseDensity, erosion, 1.0, 0.0, 1.0));
                
                // Apply detail strength
                float detailModifier = lerp(1.0, detailFBM, _DetailStrength);
                finalDensity *= detailModifier;
                
                return finalDensity * _CloudDensity;
            }
            
            // Beer-Powder approximation for cloud lighting
            float BeerPowder(float density)
            {
                float beer = exp(-density * _LightAbsorption);
                float powder = 1.0 - exp(-density * _LightAbsorption * 2.0);
                return beer * lerp(1.0, powder, 0.5);
            }
            
            // Sample light energy toward sun
            float SampleLightEnergy(float3 positionOS, float3 lightDirOS)
            {
                const int LIGHT_STEPS = 6;
                float totalDensity = 0.0;
                float stepSize = (_OuterRadius - _InnerRadius) / float(LIGHT_STEPS);
                
                // Cone sampling for softer shadows
                float3 randomOffset = float3(0.5, 0.3, 0.7) * stepSize * 0.5;
                
                for (int i = 0; i < LIGHT_STEPS; i++)
                {
                    float3 samplePos = positionOS + lightDirOS * (float(i) + 0.5) * stepSize;
                    
                    // Add slight cone spread
                    if (i > 0)
                        samplePos += randomOffset * (float(i) / float(LIGHT_STEPS));
                    
                    float density = SampleCloudDensity(samplePos, true);
                    totalDensity += density * stepSize;
                }
                
                return BeerPowder(totalDensity);
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                float3 cameraPositionOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDirWS = normalize(-input.viewDirWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                
                // Calculate intersections
                float2 outerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _OuterRadius);
                float2 innerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0, 0, 0), _InnerRadius);
                
                if (outerHit.x < 0.0 && outerHit.y < 0.0)
                    discard;
                
                // Determine ray bounds
                float rayStart, rayEnd;
                float cameraRadius = length(cameraPositionOS);
                
                if (cameraRadius > _OuterRadius)
                {
                    rayStart = outerHit.x;
                    rayEnd = (innerHit.x > 0.0) ? innerHit.x : outerHit.y;
                    
                    // Also trace the back side if we hit the inner sphere
                    // (This handles the shell around the planet)
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
                
                // Blue noise offset for temporal stability
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, screenUV * _ScreenParams.xy / 256.0).r;
                rayStart += blueNoise * _StepSize;
                
                // Get light info
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float cosTheta = dot(rayDirOS, lightDirOS);
                float phase = DualLobePhase(cosTheta);
                
                // Silver lining effect (bright edges when looking toward sun)
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread) * _SilverLiningIntensity;
                
                // Raymarching
                float transmittance = 1.0;
                float3 luminance = float3(0, 0, 0);
                float depthAccum = 0.0;
                
                float rayLength = rayEnd - rayStart;
                float dynamicStepSize = min(_StepSize, rayLength / float(_MaxSteps));
                int steps = min(_MaxSteps, int(rayLength / dynamicStepSize));
                
                for (int i = 0; i < steps; i++)
                {
                    if (transmittance < 0.01)
                        break;
                    
                    float t = rayStart + (float(i) + 0.5) * dynamicStepSize;
                    if (t > rayEnd)
                        break;
                    
                    float3 samplePos = cameraPositionOS + rayDirOS * t;
                    float density = SampleCloudDensity(samplePos, false);
                    
                    if (density > 0.001)
                    {
                        // Height for ambient color
                        float radius = length(samplePos);
                        float heightFraction = saturate((radius - _InnerRadius) / (_OuterRadius - _InnerRadius));
                        
                        // Light sampling
                        float lightEnergy = SampleLightEnergy(samplePos, lightDirOS);
                        
                        // Ambient based on height
                        float3 ambientColor = lerp(_AmbientColorBottom.rgb, _AmbientColorTop.rgb, heightFraction);
                        float3 ambient = ambientColor * _AmbientLight;
                        
                        // Direct light with phase function
                        float3 directLight = mainLight.color.rgb * lightEnergy * phase;
                        
                        // Silver lining on bright edges
                        directLight += mainLight.color.rgb * lightEnergy * silverLining;
                        
                        // Cloud color based on lighting
                        float3 cloudColor = lerp(_CloudColorDark.rgb, _CloudColorBright.rgb, lightEnergy);
                        
                        // Final sample contribution
                        float sampleDensity = density * dynamicStepSize;
                        float sampleTransmittance = exp(-sampleDensity * _LightAbsorption);
                        
                        float3 sampleLighting = (directLight + ambient) * cloudColor;
                        float3 integScatter = sampleLighting * (1.0 - sampleTransmittance);
                        
                        luminance += integScatter * transmittance;
                        transmittance *= sampleTransmittance;
                        depthAccum += sampleDensity;
                    }
                }
                
                float alpha = 1.0 - transmittance;
                
                if (alpha < 0.001)
                    discard;
                
                return float4(luminance, alpha);
            }
            
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}