Shader "Custom/Atmosphere"
{
    Properties
    {
        [KeywordEnum(USE_SUN_POSITION, USE_DIRECTIONAL)] _SUN_MODE("Sun Mode", Float) = 0
        _SunPosition("Sun Position", Vector) = (0, 0, 0, 0)
        _LightIntensity("Light Intensity", Float) = 20
        _PlanetRadius("Planet Radius (km)", Float) = 6371
        _AtmosphereHeight("Atmosphere Height (km)", Float) = 100
        
        _RayleighScaleHeight("Rayleigh Scale Height (km)", Float) = 8
        _MieScaleHeight("Mie Scale Height (km)", Float) = 1.2
        _OzoneLayerCenter("Ozone Layer Center (km)", Float) = 25
        _OzoneLayerWidth("Ozone Layer Width (km)", Float) = 15
        
        _RayleighScatteringCoeff("Rayleigh Scattering Coeff", Float) = 1.0
        _MieScatteringCoeff("Mie Scattering Coeff", Float) = 1.0
        _OzoneAbsorptionCoeff("Ozone Absorption Coeff", Float) = 1.0
        
        _G("Mie Anisotropy", Range(-0.99, 0.99)) = 0.76

        _WaveLength("Wavelength Red (nm)", Vector) = (700, 530, 440, 0)
        
        _PrimarySteps("Primary Steps", Int) = 16
        _LightSteps("Light Steps", Int) = 8
        
        _Exposure("Exposure", Float) = 2.0
        
        [Header(Clouds)]
        _CloudsEnabled("Clouds Enabled", Float) = 1
        _CloudLayerHeight("Cloud Layer Height (km)", Float) = 5
        _CloudLayerThickness("Cloud Layer Thickness (km)", Float) = 3
        _CloudDensity("Cloud Density", Range(0, 2)) = 0.5
        _CloudCoverage("Cloud Coverage", Range(0, 1)) = 0.5
        _CloudScale("Cloud Scale", Float) = 0.01
        _CloudDetailScale("Cloud Detail Scale", Float) = 0.05
        _CloudSpeed("Cloud Speed", Float) = 0.001
        _CloudSteps("Cloud Steps", Int) = 8
        _CloudLightSteps("Cloud Light Steps", Int) = 4
        _CloudLightAbsorption("Cloud Light Absorption", Range(0, 2)) = 0.5
        _CloudAmbient("Cloud Ambient Light", Range(0, 1)) = 0.2
        _CloudColor("Cloud Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Pass
        {
            Tags { "RenderPipeline"="UniversalPipeline" 
                "Queue"="Transparent" 
            "RenderType"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _LightIntensity;
                float3 _SunPosition;
                float _PlanetRadius;
                float _AtmosphereHeight;
                float _RayleighScatteringCoeff;
                float _MieScatteringCoeff;
                float _OzoneAbsorptionCoeff;
                float _G;
                float3 _WaveLength;
                float _RayleighScaleHeight;
                float _MieScaleHeight;
                float _OzoneLayerCenter;
                float _OzoneLayerWidth;
                uint _PrimarySteps;
                uint _LightSteps;
                float _Exposure;
                
                // Cloud parameters
                float _CloudsEnabled;
                float _CloudLayerHeight;
                float _CloudLayerThickness;
                float _CloudDensity;
                float _CloudCoverage;
                float _CloudScale;
                float _CloudDetailScale;
                float _CloudSpeed;
                uint _CloudSteps;
                uint _CloudLightSteps;
                float _CloudLightAbsorption;
                float _CloudAmbient;
                float4 _CloudColor;
            CBUFFER_END

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #pragma multi_compile _SUN_MODE_USE_SUN_POSITION _SUN_MODE_USE_DIRECTIONAL
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // ============================================
            // NOISE FUNCTIONS FOR CLOUDS
            // ============================================
            
            float hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z);
            }
            
            float3 hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                
                float n000 = hash13(i + float3(0, 0, 0));
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));
                
                float n00 = lerp(n000, n100, f.x);
                float n10 = lerp(n010, n110, f.x);
                float n01 = lerp(n001, n101, f.x);
                float n11 = lerp(n011, n111, f.x);
                
                float n0 = lerp(n00, n10, f.y);
                float n1 = lerp(n01, n11, f.y);
                
                return lerp(n0, n1, f.z);
            }
            
            float FBM(float3 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float totalAmplitude = 0.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * ValueNoise(p * frequency);
                    totalAmplitude += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                return value / totalAmplitude;
            }
            
            float WorleyNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                
                float minDist = 1.0;
                
                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float3 offset = float3(x, y, z);
                            float3 cellPoint = hash33(i + offset);
                            float3 diff = offset + cellPoint - f;
                            float dist = length(diff);
                            minDist = min(minDist, dist);
                        }
                    }
                }
                
                return minDist;
            }

            // ============================================
            // CLOUD DENSITY SAMPLING
            // ============================================
            
            float GetHeightFraction(float3 pos, float3 planetCenter)
            {
                float height = length(pos - planetCenter) - _PlanetRadius;
                float cloudBottom = _CloudLayerHeight;
                float cloudTop = _CloudLayerHeight + _CloudLayerThickness;
                return saturate((height - cloudBottom) / (cloudTop - cloudBottom));
            }

            float HeightGradient(float heightFraction)
            {
                // Smoother gradient using smoothstep instead of hard multipliers
                float bottom = smoothstep(0.0, 0.2, heightFraction);
                float top = smoothstep(1.0, 0.7, heightFraction);
                return bottom * top;
            }
            
            float SampleCloudDensity(float3 pos, float3 planetCenter, bool cheap)
            {
                float heightFraction = GetHeightFraction(pos, planetCenter);
                
                // Outside cloud layer
                if (heightFraction <= 0.0 || heightFraction >= 1.0)
                return 0.0;
                
                // Convert to spherical coordinates for seamless wrapping
                float3 relPos = pos - planetCenter;
                float3 normalizedPos = normalize(relPos);
                
                // Use spherical position for noise sampling
                float3 noisePos = normalizedPos * (_PlanetRadius + _CloudLayerHeight);
                float time = _Time.y * _CloudSpeed;
                
                // Base shape noise (low frequency)
                float3 baseNoisePos = noisePos * _CloudScale + float3(time, 0, time * 0.5);
                float baseNoise = FBM(baseNoisePos, 4);
                
                // Softer coverage remapping - avoid hard cutoff
                float coverage = _CloudCoverage;
                float coverageMin = 1.0 - coverage;
                baseNoise = saturate((baseNoise - coverageMin) / (1.0 - coverageMin + 0.001));
                
                // Smoother height gradient - less pronounced layers
                float heightGrad = HeightGradient(heightFraction);
                // Reduce height influence to prevent visible layering
                heightGrad = lerp(0.5, heightGrad, 0.6);
                
                float density = baseNoise * heightGrad;
                
                // Add detail noise (only for non-cheap samples)
                if (!cheap && density > 0.01)
                {
                    float3 detailNoisePos = noisePos * _CloudDetailScale + float3(time * 2.0, time, 0);
                    float detailNoise = FBM(detailNoisePos, 3);
                    
                    // Remove Worley noise - it causes the hollow outlines
                    // Instead use only FBM detail with soft erosion
                    float erosion = detailNoise * 0.4;
                    
                    // Only erode where there's already density (prevents hollow edges)
                    erosion *= smoothstep(0.0, 0.3, density);
                    
                    density = saturate(density - erosion);
                }
                
                // Soft fade at edges to prevent hard outlines
                density *= smoothstep(0.0, 0.1, density);
                
                return density * _CloudDensity;
            }

            

            // ============================================
            // CLOUD LIGHTING
            // ============================================
            
            float GetCloudLightTransmittance(float3 pos, float3 lightDir, float3 planetCenter)
            {
                float stepSize = _CloudLayerThickness / float(_CloudLightSteps);
                float transmittance = 1.0;
                
                for (uint i = 0; i < _CloudLightSteps; i++)
                {
                    float3 samplePos = pos + lightDir * stepSize * (float(i) + 0.5);
                    float density = SampleCloudDensity(samplePos, planetCenter, true);
                    transmittance *= exp(-density * stepSize * _CloudLightAbsorption);
                    
                    if (transmittance < 0.01)
                    break;
                }
                
                return transmittance;
            }
            
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
            }
            
            float4 RaymarchClouds(float3 rayOrigin, float3 rayDir, float tMin, float tMax, float3 lightDir, float3 planetCenter)
            {
                if (_CloudsEnabled < 0.5)
                return float4(0, 0, 0, 0);
                
                float stepSize = (tMax - tMin) / float(_CloudSteps);
                
                // Jitter the starting position to reduce banding
                float jitter = hash13(rayDir * 1000.0 + _Time.y) * stepSize;
                float3 pos = rayOrigin + rayDir * (tMin + jitter);
                
                float transmittance = 1.0;
                float3 luminance = 0;
                
                float cosTheta = dot(rayDir, lightDir);
                float phase = lerp(HenyeyGreenstein(cosTheta, 0.3), HenyeyGreenstein(cosTheta, -0.3), 0.5);
                
                for (uint i = 0; i < _CloudSteps; i++)
                {
                    float density = SampleCloudDensity(pos, planetCenter, false);
                    
                    if (density > 0.001)
                    {
                        float3 normal = normalize(pos - planetCenter);
                        float dayFactor = smoothstep(-0.1, 0.3, dot(normal, lightDir));
                        
                        float lightTransmittance = GetCloudLightTransmittance(pos, lightDir, planetCenter);
                        
                        float3 directLight = lightTransmittance * phase * _LightIntensity * dayFactor;
                        
                        float heightFrac = GetHeightFraction(pos, planetCenter);
                        float3 ambientLight = _CloudAmbient * lerp(float3(0.4, 0.5, 0.7), float3(0.8, 0.9, 1.0), heightFrac) * dayFactor;
                        
                        float3 cloudLight = (directLight + ambientLight) * _CloudColor.rgb;
                        
                        float sampleTransmittance = exp(-density * stepSize * _CloudLightAbsorption);
                        
                        float3 integScatter = (cloudLight - cloudLight * sampleTransmittance) / max(_CloudLightAbsorption, 0.0001);
                        luminance += transmittance * integScatter;
                        transmittance *= sampleTransmittance;
                        
                        if (transmittance < 0.01)
                        break;
                    }
                    
                    pos += rayDir * stepSize;
                }
                
                // Soften the final alpha to reduce hard edges
                float alpha = 1.0 - transmittance;
                alpha = smoothstep(0.0, 0.1, alpha) * alpha;
                
                return float4(luminance, alpha);
            }

            // ============================================
            // ATMOSPHERE FUNCTIONS (Original)
            // ============================================

            float3 GetRayleighCoefficients()
            {
                float3 wavelengths = _WaveLength;
                float3 scattering = pow(440.0 / wavelengths, 4.0);
                float baseCoeff = 0.0058 * _RayleighScatteringCoeff;
                return scattering * baseCoeff;
            }

            float3 GetMieCoefficients()
            {
                float baseCoeff = 0.0021 * _MieScatteringCoeff;
                return float3(baseCoeff, baseCoeff, baseCoeff);
            }

            float3 GetOzoneAbsorption()
            {
                float3 absorption = float3(0.4, 2.0, 0.05) * 0.00006 * _OzoneAbsorptionCoeff;
                return absorption;
            }

            float PhaseRayleigh(float cosTheta)
            {
                return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
            }

            float PhaseMie(float cosTheta, float g)
            {
                float g2 = g * g;
                float num = (1.0 - g2) * (1.0 + cosTheta * cosTheta);
                float denom = (2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5);
                return (3.0 / (8.0 * PI)) * num / denom;
            }

            float2 RaySphereIntersection(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius)
            {
                float3 oc = rayOrigin - sphereCenter;
                float b = dot(oc, rayDir);
                float c = dot(oc, oc) - sphereRadius * sphereRadius;
                float discriminant = b * b - c;
                
                if (discriminant < 0.0)
                return float2(1e6, -1e6);
                
                float sqrtDisc = sqrt(discriminant);
                return float2(-b - sqrtDisc, -b + sqrtDisc);
            }

            float DensityAtHeight(float height, float scaleHeight)
            {
                return exp(-max(0.0, height) / scaleHeight);
            }

            float OzoneDensity(float height)
            {
                float x = (height - _OzoneLayerCenter) / _OzoneLayerWidth;
                return max(0.0, exp(-x * x));
            }

            float3 OpticalDepth(float3 startPos, float3 endPos, float3 planetCenter, uint steps)
            {
                float3 step = (endPos - startPos) / float(steps);
                float stepLength = length(step);
                float3 samplePos = startPos + step * 0.5;
                
                float opticalDepthRayleigh = 0.0;
                float opticalDepthMie = 0.0;
                float opticalDepthOzone = 0.0;
                
                for (uint i = 0; i < steps; i++)
                {
                    float height = length(samplePos - planetCenter) - _PlanetRadius;
                    
                    opticalDepthRayleigh += DensityAtHeight(height, _RayleighScaleHeight);
                    opticalDepthMie += DensityAtHeight(height, _MieScaleHeight);
                    opticalDepthOzone += OzoneDensity(height);
                    
                    samplePos += step;
                }
                
                return float3(opticalDepthRayleigh, opticalDepthMie, opticalDepthOzone) * stepLength;
            }

            float3 CalculateScattering(float3 rayOrigin, float3 rayDir, float tMin, float tMax, float3 lightDir, float3 planetCenter)
            {
                float3 betaRayleigh = GetRayleighCoefficients();
                float3 betaMie = GetMieCoefficients();
                float3 betaOzone = GetOzoneAbsorption();
                
                float stepSize = (tMax - tMin) / float(_PrimarySteps);
                float3 samplePos = rayOrigin + rayDir * (tMin + stepSize * 0.5);
                
                float3 totalRayleigh = 0;
                float3 totalMie = 0;
                
                float3 opticalDepthPA = 0;
                
                float cosTheta = dot(rayDir, lightDir);
                float phaseRayleigh = PhaseRayleigh(cosTheta);
                float phaseMie = PhaseMie(cosTheta, _G);
                
                for (uint i = 0; i < _PrimarySteps; i++)
                {
                    float height = length(samplePos - planetCenter) - _PlanetRadius;
                    
                    float densityRayleigh = DensityAtHeight(height, _RayleighScaleHeight);
                    float densityMie = DensityAtHeight(height, _MieScaleHeight);
                    float densityOzone = OzoneDensity(height);
                    
                    opticalDepthPA += float3(densityRayleigh, densityMie, densityOzone) * stepSize;
                    
                    float2 lightIntersect = RaySphereIntersection(samplePos, lightDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                    
                    if (lightIntersect.y > 0)
                    {
                        float3 opticalDepthAB = OpticalDepth(samplePos, samplePos + lightDir * lightIntersect.y, planetCenter, _LightSteps);
                        
                        float3 totalOpticalDepth = opticalDepthPA + opticalDepthAB;
                        
                        float3 extinction = betaRayleigh * totalOpticalDepth.x + 
                        betaMie * totalOpticalDepth.y + 
                        betaOzone * totalOpticalDepth.z;
                        
                        float3 transmittance = exp(-extinction);
                        
                        totalRayleigh += transmittance * densityRayleigh;
                        totalMie += transmittance * densityMie;
                    }
                    
                    samplePos += rayDir * stepSize;
                }
                
                totalRayleigh *= stepSize;
                totalMie *= stepSize;
                
                float3 scattering = totalRayleigh * betaRayleigh * phaseRayleigh + 
                totalMie * betaMie * phaseMie;
                
                return scattering * _LightIntensity;
            }

            // ============================================
            // FRAGMENT SHADER
            // ============================================

            float4 frag(Varyings IN) : SV_Target
            {
                float3 planetCenter = unity_ObjectToWorld._m03_m13_m23;
                float3 cameraPos = _WorldSpaceCameraPos.xyz;
                float3 viewDir = normalize(IN.positionWS - cameraPos);
                
                #if defined(_SUN_MODE_USE_SUN_POSITION)
                    float3 sunDir = normalize(_SunPosition - planetCenter);
                #else
                    float3 sunDir = GetMainLight().direction;
                #endif

                float2 atmosphereIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                
                if (atmosphereIntersect.x > atmosphereIntersect.y)
                discard;
                
                float2 planetIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius);
                
                float tMin = max(0, atmosphereIntersect.x);
                float tMax = planetIntersect.x > 0 ? min(atmosphereIntersect.y, planetIntersect.x) : atmosphereIntersect.y;
                
                if (tMin >= tMax)
                discard;
                
                // Calculate atmosphere scattering
                float3 scatter = CalculateScattering(cameraPos, viewDir, tMin, tMax, sunDir, planetCenter);
                
                // Calculate cloud layer intersection
                float2 cloudBottomIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius + _CloudLayerHeight);
                float2 cloudTopIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius + _CloudLayerHeight + _CloudLayerThickness);
                
                float cloudTMin = max(tMin, min(cloudBottomIntersect.x, cloudTopIntersect.x));
                float cloudTMax = min(tMax, max(cloudBottomIntersect.y, cloudTopIntersect.y));
                
                // Raymarch clouds
                float4 clouds = float4(0, 0, 0, 0);
                if (cloudTMin < cloudTMax && _CloudsEnabled > 0.5)
                {
                    clouds = RaymarchClouds(cameraPos, viewDir, cloudTMin, cloudTMax, sunDir, planetCenter);
                }
                
                // Combine atmosphere and clouds
                // Clouds are in front of atmosphere, so blend accordingly
                float3 combined = scatter * (1.0 - clouds.a) + clouds.rgb;
                
                combined *= _Exposure;
                
                // Filmic tonemapping
                combined = saturate(combined / (combined + 1.0));
                
                // Gamma correction
                combined = pow(max(combined, 0.0), 1.0 / 2.2);
                
                float distance = tMax - tMin;
                float alpha = saturate(1.0 - exp(-distance / (_AtmosphereHeight * 0.3)));
                alpha = max(alpha, clouds.a);
                
                return float4(combined, alpha);
            }
            ENDHLSL
        }
    }
}