Shader "Custom/Atmosphere"
{
    Properties
    {
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

        _PrimarySteps("Primary Steps", Int) = 32
        _LightSteps("Light Steps", Int) = 4
    }

    SubShader
    {
        Pass
        {
            Name "Universal Forward"
            Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Opaque" "RenderType"="Opaque"}
            Cull Off
            ZTest LEqual
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            

            CBUFFER_START(UnityPerMaterial)
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
                uint _PrimarySteps;
                uint _LightSteps;
            CBUFFER_END

            // #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            
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
            

            #include "AtmoUtilities.hlsl"

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            

            float4 frag(Varyings IN, out float DEPTH : SV_Depth) : SV_Target
            {
                float3 planetPos = unity_ObjectToWorld._m03_m13_m23;
                float3 cameraPosWS = _WorldSpaceCameraPos.xyz;
                float3 positionWS = IN.positionWS;
                float3 normalWS = normalize(IN.normalWS);
                // Camera to Pixel Direction
                float3 viewDir = normalize(cameraPosWS - positionWS);

                float sceneDepth = SampleSceneDepth(IN.uv);
                float depth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                
                // viewDir = normalize(viewDir);
                // Main Light Direction
                float3 sunDir = normalize(_SunPosition - planetPos);
                float atmosphereRadius = _PlanetRadius + _AtmosphereRadius;

                float4 color = float4(0, 0, 0, 1);

                color.rgb += CalculateScattering(
                cameraPosWS,
                viewDir,
                depth,
                float3(0,0,0),
                sunDir,
                planetPos,
                _PlanetRadius,
                atmosphereRadius,
                _PrimarySteps,
                _LightSteps
                );

                color.rgb = 1 - exp(-color.rgb);
                // color.a = 0.5;

                float NoL = saturate(pow(saturate(dot(normalWS, sunDir) + 0.5), 1.1));
                float fresnel = saturate(pow(dot(normalWS, viewDir), 1.5));
                color.rgb *= fresnel;

                DEPTH = IN.positionHCS.z / IN.positionHCS.w;
                return color;
            }
            ENDHLSL
        }
    }
}
