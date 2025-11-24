Shader "Custom/Atmosphere"
{
    Properties
    {
        [KeywordEnum(USE_SUN_POSITION, USE_DIRECTIONAL)] _SUN_MODE("Sun Mode", Float) = 0
        _SunPosition("Sun Position", Vector) = (0, 0, 0, 0)
        _LightIntensity("Light Intensity", Float) = 22
        _PlanetRadius("Planet Radius (km)", Float) = 6371    // Earth radius in km
        _AtmosphereHeight("Atmosphere Height (km)", Float) = 100 // Atmosphere height in km
        
        _RayleighScaleHeight("Rayleigh Scale Height (km)", Float) = 8
        _MieScaleHeight("Mie Scale Height (km)", Float) = 1.2
    
        _RayleighBeta("Rayleigh Beta", Vector) = (0.0000055, 0.000013, 0.0000224, 0)
        _MieBeta("Mie Beta", Float) = 0.000021
        
        _G("Mie Anisotropy", Range(-0.99, 0.99)) = 0.76
        
        _PrimarySteps("Primary Steps", Int) = 16
        _LightSteps("Light Steps", Int) = 8
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
            Blend One OneMinusSrcAlpha

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _LightIntensity;
                float3 _SunPosition;
                float _PlanetRadius;
                float _AtmosphereHeight;
                float3 _RayleighBeta;
                float _MieBeta;
                float _G;
                float _RayleighScaleHeight;
                float _MieScaleHeight;
                uint _PrimarySteps;
                uint _LightSteps;
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

            // Rayleigh phase function
            float PhaseRayleigh(float cosTheta)
            {
                return (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
            }

            // Cornette-Shanks phase function (better than Henyey-Greenstein)
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
                return exp(-height / scaleHeight);
            }

            // Calculate optical depth between two points
            float3 OpticalDepth(float3 startPos, float3 endPos, float3 planetCenter, uint steps)
            {
                float3 step = (endPos - startPos) / float(steps);
                float stepLength = length(step);
                float3 samplePos = startPos + step * 0.5;
                
                float opticalDepthRayleigh = 0.0;
                float opticalDepthMie = 0.0;
                
                for (uint i = 0; i < steps; i++)
                {
                    float height = length(samplePos - planetCenter) - _PlanetRadius;
                    
                    opticalDepthRayleigh += DensityAtHeight(height, _RayleighScaleHeight);
                    opticalDepthMie += DensityAtHeight(height, _MieScaleHeight);
                    
                    samplePos += step;
                }
                
                return float3(opticalDepthRayleigh, opticalDepthMie, 0) * stepLength;
            }

            float3 CalculateScattering(float3 rayOrigin, float3 rayDir, float tMin, float tMax, float3 lightDir, float3 planetCenter)
            {
                float stepSize = (tMax - tMin) / float(_PrimarySteps);
                float3 samplePos = rayOrigin + rayDir * (tMin + stepSize * 0.5);
                
                float3 totalRayleigh = 0;
                float3 totalMie = 0;
                
                float2 opticalDepthPA = 0; // From camera to sample point (P->A)
                
                float cosTheta = dot(rayDir, lightDir);
                float phaseRayleigh = PhaseRayleigh(cosTheta);
                float phaseMie = PhaseMie(cosTheta, _G);
                
                for (uint i = 0; i < _PrimarySteps; i++)
                {
                    float height = length(samplePos - planetCenter) - _PlanetRadius;
                    
                    // Density at current sample point
                    float densityRayleigh = DensityAtHeight(height, _RayleighScaleHeight);
                    float densityMie = DensityAtHeight(height, _MieScaleHeight);
                    
                    // Accumulate optical depth from camera to sample
                    opticalDepthPA += float2(densityRayleigh, densityMie) * stepSize;
                    
                    // Calculate optical depth from sample to sun
                    float2 lightIntersect = RaySphereIntersection(samplePos, lightDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                    
                    if (lightIntersect.y > 0) // Ray reaches atmosphere boundary
                    {
                        float3 opticalDepthAB = OpticalDepth(samplePos, samplePos + lightDir * lightIntersect.y, planetCenter, _LightSteps);
                        
                        // Total optical depth = camera to sample + sample to sun
                        float2 totalOpticalDepth = opticalDepthPA + opticalDepthAB.xy;
                        
                        // Calculate transmittance using Beer's law
                        float3 transmittanceRayleigh = exp(-_RayleighBeta * totalOpticalDepth.x);
                        float3 transmittanceMie = exp(-_MieBeta * totalOpticalDepth.y);
                        float3 transmittance = transmittanceRayleigh * transmittanceMie;
                        
                        // Accumulate scattered light
                        totalRayleigh += transmittance * densityRayleigh * stepSize;
                        totalMie += transmittance * densityMie * stepSize;
                    }
                    
                    samplePos += rayDir * stepSize;
                }
                
                // Combine Rayleigh and Mie scattering
                float3 scattering = totalRayleigh * _RayleighBeta * phaseRayleigh + 
                                   totalMie * _MieBeta * phaseMie;
                
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
                    float3 sunDir = -GetMainLight().direction;
                #endif

                // Intersect with atmosphere
                float2 atmosphereIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                
                if (atmosphereIntersect.x > atmosphereIntersect.y)
                    discard;
                
                // Intersect with planet surface
                float2 planetIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius);
                
                // Ray enters atmosphere at atmosphereIntersect.x
                float tMin = max(0, atmosphereIntersect.x);
                // Ray exits atmosphere or hits planet, whichever comes first
                float tMax = planetIntersect.x > 0 ? min(atmosphereIntersect.y, planetIntersect.x) : atmosphereIntersect.y;
                
                if (tMin >= tMax)
                    discard;
                
                // Calculate scattering
                float3 scatter = CalculateScattering(cameraPos, viewDir, tMin, tMax, sunDir, planetCenter);
                
                // Tone mapping
                scatter = 1.0 - exp(-scatter);
                
                // Gamma correction
                scatter = pow(scatter, 1.0 / 2.2);
                
                // Calculate alpha based on optical depth
                float distance = tMax - tMin;
                float alpha = 1.0 - exp(-distance / (_AtmosphereHeight * 0.5));
                
                return float4(scatter, alpha);
            }
            ENDHLSL
        }
    }
}