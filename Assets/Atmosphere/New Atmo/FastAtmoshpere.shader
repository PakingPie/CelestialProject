Shader "Custom/FastAtmosphere"
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

            // Wavelength-dependent scattering coefficients
            // These are scaled by wavelength^-4 for Rayleigh
            float3 GetRayleighCoefficients()
            {
                // Wavelengths in nm: Red=700, Green=530, Blue=440
                // Relative scattering: proportional to 1/wavelength^4
                float3 wavelengths = _WaveLength;
                float3 scattering = pow(440.0 / wavelengths, 4.0);
                
                // Base coefficient scaled to realistic values (per km)
                float baseCoeff = 0.0058 * _RayleighScatteringCoeff;
                return scattering * baseCoeff;
            }

            float3 GetMieCoefficients()
            {
                // Mie scattering is wavelength independent (or nearly so)
                float baseCoeff = 0.0021 * _MieScatteringCoeff;
                return float3(baseCoeff, baseCoeff, baseCoeff);
            }

            float3 GetOzoneAbsorption()
            {
                // Ozone strongly absorbs in green/yellow
                // Peak absorption around 600nm (orange-red boundary)
                // Less absorption in red and blue
                float3 absorption = float3(0.4, 2.0, 0.05) * 0.00006 * _OzoneAbsorptionCoeff;
                return absorption;
            }

            // Rayleigh phase function
            float PhaseRayleigh(float cosTheta)
            {
                return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
            }

            // Cornette-Shanks phase function for Mie
            float PhaseMie(float cosTheta, float g)
            {
                float g2 = g * g;
                float num = (1.0 - g2) * (1.0 + cosTheta * cosTheta);
                float denom = (2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * g * cosTheta), 1.5);
                return (3.0 / (8.0 * PI)) * num / denom;
            }

            // Ray-sphere intersection
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

            // Density at height using exponential falloff
            float DensityAtHeight(float height, float scaleHeight)
            {
                return exp(-max(0.0, height) / scaleHeight);
            }

            // Ozone density distribution
            float OzoneDensity(float height)
            {
                float x = (height - _OzoneLayerCenter) / _OzoneLayerWidth;
                return max(0.0, exp(-x * x));
            }

            // Calculate optical depth between two points
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
                    
                    // Calculate sun ray optical depth
                    float2 lightIntersect = RaySphereIntersection(samplePos, lightDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                    
                    if (lightIntersect.y > 0)
                    {
                        float3 opticalDepthAB = OpticalDepth(samplePos, samplePos + lightDir * lightIntersect.y, planetCenter, _LightSteps);
                        
                        float3 totalOpticalDepth = opticalDepthPA + opticalDepthAB;
                        
                        // Calculate total extinction (scattering + absorption)
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
                
                // Combine scattering
                float3 scattering = totalRayleigh * betaRayleigh * phaseRayleigh + 
                                   totalMie * betaMie * phaseMie;
                
                return scattering * _LightIntensity;
            }

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
                
                float3 scatter = CalculateScattering(cameraPos, viewDir, tMin, tMax, sunDir, planetCenter);
                
                scatter *= _Exposure;
                
                // Filmic tonemapping
                scatter = saturate(scatter / (scatter + 1.0));
                
                // Gamma correction
                scatter = pow(max(scatter, 0.0), 1.0 / 2.2);
                
                float distance = tMax - tMin;
                float alpha = saturate(1.0 - exp(-distance / (_AtmosphereHeight * 0.3)));
                
                return float4(scatter, alpha);
            }
            ENDHLSL
        }
    }
}