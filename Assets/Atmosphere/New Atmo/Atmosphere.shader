Shader "Custom/Atmosphere"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        _SunPosition("Sun Position", Vector) = (0, 0, 0, 0)
        _LightIntensity("Light Intensity", Float) = 10
        _PlanetRadius("Planet Radius", Float) = 1
        _AtmosphereRadius("Atmosphere Radius", Float) = 0
        _RayleighBeta("Rayleigh Scattering Coefficients", Vector) = (0.0000055, 0.000013, 0.0000224)
        _MieBeta("Mie Scattering Coefficients", Vector) = (0.000021, 0.000021, 0.000021)
        _AmbientBeta("Ambient Coefficients", Color) = (0, 0, 0, 1)
        _AbsorptionBeta("Absorption Coefficients", Vector) = (0.0000204, 0.0000497, 0.00000195)
        _G("G", Range(0, 1)) = 0.76
        _HeightRayleigh("Height Rayleigh", Float) = 8000
        _HeightMie("Height Mie", Float) = 1200
        _HeightAbsorption("Height Absorption", Float) = 30000
        _AbsorptionFalloff("Absorption Falloff", Float) = 4000

        _LightSteps("Light Steps", Int) = 4
    }

    SubShader
    {
        Cull Off 
        ZWrite On 
        ZTest LEqual

        Pass
        {
            Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
            HLSLPROGRAM

            // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma target 4.5

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float _LightIntensity;
                float3 _SunPosition;


                float _PlanetRadius;
                float _AtmosphereRadius;

                float3 _RayleighBeta;
                float3 _MieBeta;
                float3 _AmbientBeta;
                float3 _AbsorptionBeta;
                float _G;
                float _HeightRayleigh;
                float _HeightMie;
                float _HeightAbsorption;
                float _AbsorptionFalloff;

                uint _LightSteps;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float2 RaySphereIntersect
            (
            float3 start, // starting position of the ray
            float3 dir,   // the direction of the ray
            float radius  // and the sphere radius
            )
            {
                // ray-sphere intersection that assumes
                // the sphere is centered at the origin.
                // No intersection when result.x > result.y
                float a = dot(dir, dir);
                float b = 2.0 * dot(dir, start);
                float c = dot(start, start) - (radius * radius);
                float d = (b * b) - 4.0 * a * c;
                if (d < 0.0)
                return float2(1e5, -1e5);
                return float2(
                (-b - sqrt(d)) / (2.0 * a),
                (-b + sqrt(d)) / (2.0 * a));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 cameraPosWS = _WorldSpaceCameraPos;
                float3 positionWS = IN.positionWS;
                float3 normalWS = normalize(IN.normalWS);
                // Camera to Pixel Direction
                float3 viewDir = normalize(cameraPosWS - positionWS);


                float sceneDepth = SampleSceneDepth(IN.uv);
                float depth = LinearEyeDepth(sceneDepth, _ZBufferParams) * length(viewDir);

                // Main Light Direction
                float3 sunDir = normalize(_SunPosition - positionWS);
                float NdotL = saturate(dot(normalWS, sunDir));
                float NdotV = saturate(dot(normalWS, viewDir));

                float atmosphereRadius = _PlanetRadius + _AtmosphereRadius;

                float4 color = 1;

                cameraPosWS -= unity_ObjectToWorld._m03_m13_m23;

                // float2 viewRayIntersectPlanet = RaySphereIntersect(cameraPosWS, viewDir, atmosphereRadius);
                float a = dot(viewDir, viewDir);
                float b = 2.0 * dot(viewDir, cameraPosWS);
                float c = dot(cameraPosWS, cameraPosWS) - (atmosphereRadius * atmosphereRadius);
                float d = (b * b) - 4.0 * a * c;
                // If not hit planet, return scene color
                if(d < 0)
                {
                    color = 0;
                    return color;
                }

                float maxDistance = depth;

                float2 rayLength = float2(
                max((-b - sqrt(d)) / (2.0 * a), 0), 
                min((-b + sqrt(d)) / (2.0 * a), maxDistance)
                );
                color.a = 0.5;

                if(rayLength.x > rayLength.y)
                {
                    color = float4(0.5, 0.5, 0.5, 1);
                    return color;
                }

                bool allowMie = maxDistance > rayLength.y;
                rayLength.y = min(rayLength.y, maxDistance);
                rayLength.x = max(rayLength.x, 0);

                float stepSizeI = (rayLength.y - rayLength.x) / float(_LightSteps);

                float rayPosI = rayLength.x + stepSizeI * 0.5;

                float3 totalRayleigh = 0;
                float3 totalMie = 0;

                float3 opticalDepthI = 0;

                float2 scaleHeight = float2(_HeightRayleigh, _HeightMie);

                float mu = dot(viewDir, sunDir);
                float mumu = mu * mu;
                float gg = _G * _G;
                float phaseRayleigh = 3 / 50.2654824574 * (1 + mumu);
                float phaseMie = allowMie ? 3 / 25.1327412287 * ((1 - gg) * (1 + mumu)) / (pow(1 + gg - 2 * _G * mu, 1.5) * (2.0 + gg)) : 0;
                
                uint stepI = _LightSteps;
                for(int i = 0; i < stepI; ++i)
                {
                    float3 posI = cameraPosWS + viewDir * rayPosI;
                    // get object scaled radius

                    float heightI = length(posI) - _PlanetRadius;

                    float3 density = float3(exp(-heightI / scaleHeight), 0);
                    
                    float denom = (_HeightAbsorption - heightI) / _AbsorptionFalloff;
                    density.z = (1.0 / (denom * denom + 1.0)) * density.x;  

                    density *= stepSizeI;

                    opticalDepthI += density;

                    a = dot(sunDir, sunDir);
                    b = 2.0 * dot(sunDir, posI);
                    c = dot(posI, posI) - (atmosphereRadius * atmosphereRadius);
                    d = (b * b) - 4.0 * a * c;
                    

                    float stepsL = _LightSteps;
                    float stepSizeL = (-b + sqrt(d)) / (2 * a * float(stepsL));

                    float rayPosL = stepSizeL * 0.5;

                    float3 opticalDepthL = 0;

                    for(int l = 0; l < stepsL; ++l)
                    {
                        float3 posL = posI + sunDir * rayPosL;
                        
                        float heightL = length(posL) - _PlanetRadius;

                        float3 densityL = float3(exp(-heightL / scaleHeight), 0);

                        float denomL = (_HeightAbsorption - heightL) / _AbsorptionFalloff;
                        densityL.z = (1.0 / (denomL * denomL + 1.0)) * densityL.x;  

                        densityL *= stepSizeL;

                        opticalDepthL += densityL;

                        rayPosL += stepSizeL;
                    }

                    float3 attenuation = exp(-(_RayleighBeta * (opticalDepthI.x + opticalDepthL.x) - _MieBeta * (opticalDepthI.y + opticalDepthL.y) - _AbsorptionBeta * (opticalDepthI.z + opticalDepthL.z)));
                    totalRayleigh += density.x * attenuation;
                    totalMie += density.y * attenuation;

                    rayPosI += stepSizeI;
                }

                float3 opacity = exp(-(_MieBeta * opticalDepthI.y + _RayleighBeta * opticalDepthI.x + _AbsorptionBeta * opticalDepthI.z));

                float3 finalColor = (phaseRayleigh * _RayleighBeta * totalRayleigh + 
                phaseMie * _MieBeta * totalMie + 
                opticalDepthI.x * _AmbientBeta) * _LightIntensity;


                color.rgb = finalColor;

                return color;
            }
            ENDHLSL
        }
    }
}
