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

        _TerminatorStart("Terminator Start", Range(-0.3, 0.3)) = 0.0
        _TerminatorEnd("Terminator End", Range(0.1, 0.5)) = 0.2

        _WaveLength("Wavelength Red (nm)", Vector) = (700, 530, 440, 0)
        
        _PrimarySteps("Primary Steps", Int) = 16
        _LightSteps("Light Steps", Int) = 8
        
        _Exposure("Exposure", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "AtmospherePass"

            Cull Front
            ZTest Always
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _LightIntensity;
                float3 _SunPosition;
                float _PlanetRadius;
                float _AtmosphereHeight;
                float _RayleighScatteringCoeff;
                float _MieScatteringCoeff;
                float _OzoneAbsorptionCoeff;
                float _G;
                float _TerminatorStart;
                float _TerminatorEnd;
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile _SUN_MODE_USE_SUN_POSITION _SUN_MODE_USE_DIRECTIONAL
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // ============================================
            // DEPTH SAMPLING HELPER
            // ============================================
            
            float GetSceneDistance(float2 screenUV, float3 cameraPos, float3 viewDir)
            {
                float rawDepth = SampleSceneDepth(screenUV);
                
                // Sky detection using Linear01Depth — distance-independent
                // (far plane → 1.0, near plane → 0.0)
                float linear01 = Linear01Depth(rawDepth, _ZBufferParams);
                if (linear01 > 0.999)
                return 1e20;
                
                float linearDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float3 viewDirVS = TransformWorldToViewDir(viewDir);
                float rayDistance = linearDepth / max(-viewDirVS.z, 0.00001);
                
                return rayDistance;
            }

            // ============================================
            // ATMOSPHERE FUNCTIONS
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
                return float2(1e20, -1e20);
                
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
                    height = max(0.0, height);
                    
                    float densityRayleigh = DensityAtHeight(height, _RayleighScaleHeight);
                    float densityMie = DensityAtHeight(height, _MieScaleHeight);
                    float densityOzone = OzoneDensity(height);
                    
                    opticalDepthPA += float3(densityRayleigh, densityMie, densityOzone) * stepSize;
                    
                    float2 lightIntersect = RaySphereIntersection(samplePos, lightDir, planetCenter, _PlanetRadius + _AtmosphereHeight);
                    
                    if (lightIntersect.y > 0)
                    {
                        float3 sampleNormal = normalize(samplePos - planetCenter);
                        float dayFactor = smoothstep(_TerminatorStart, _TerminatorEnd, dot(sampleNormal, lightDir));
                        
                        float3 opticalDepthAB = OpticalDepth(samplePos, samplePos + lightDir * lightIntersect.y, planetCenter, _LightSteps);
                        
                        float3 totalOpticalDepth = opticalDepthPA + opticalDepthAB;
                        
                        float3 extinction = betaRayleigh * totalOpticalDepth.x + 
                        betaMie * totalOpticalDepth.y + 
                        betaOzone * totalOpticalDepth.z;
                        
                        float3 transmittance = exp(-extinction);
                        
                        totalRayleigh += transmittance * densityRayleigh * dayFactor;
                        totalMie += transmittance * densityMie * dayFactor;
                    }
                    
                    samplePos += rayDir * stepSize;
                }
                
                totalRayleigh *= stepSize;
                totalMie *= stepSize;
                
                float3 scattering = totalRayleigh * betaRayleigh * phaseRayleigh + 
                totalMie * betaMie * phaseMie;
                
                return scattering;
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

                float2 screenUV = IN.positionHCS.xy / _ScaledScreenParams.xy;
                float sceneDistance = GetSceneDistance(screenUV, cameraPos, viewDir);

                // Analytical atmosphere outer boundary
                float atmosphereOuterRadius = _PlanetRadius + _AtmosphereHeight;
                float2 atmosphereIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, atmosphereOuterRadius);
                
                // No atmosphere intersection at all
                if (atmosphereIntersect.x > atmosphereIntersect.y)
                discard;
                
                // Analytical planet surface intersection (inner solid sphere)
                float2 planetIntersect = RaySphereIntersection(cameraPos, viewDir, planetCenter, _PlanetRadius);
                
                // Determine ray segment through atmosphere
                float tMin = max(0.0, atmosphereIntersect.x);
                float tMax = atmosphereIntersect.y;
                
                // Clamp at planet surface when the hit is ahead of the camera
                if (planetIntersect.x > 0.0 && planetIntersect.x < tMax)
                {
                    tMax = planetIntersect.x;
                }
                
                // Clamp at scene depth buffer (rendered geometry such as terrain)
                if (sceneDistance > 0.0 && sceneDistance < tMax)
                {
                    tMax = sceneDistance;
                }
                
                // Atmosphere fully behind geometry, or degenerate segment
                if (tMin >= tMax)
                discard;

                // Calculate atmosphere scattering
                float3 scatter = CalculateScattering(cameraPos, viewDir, tMin, tMax, sunDir, planetCenter);
                
                #if defined(_SUN_MODE_USE_SUN_POSITION)
                    scatter *= _LightIntensity;
                #else
                    scatter *= GetMainLight().color.rgb * GetMainLight().shadowAttenuation * _LightIntensity;
                #endif
                
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

        // DepthOnly and DepthNormals passes removed —
        // the atmosphere must NOT write to the depth buffer.
    }
}