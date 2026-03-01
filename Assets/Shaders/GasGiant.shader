Shader "Custom/GasGiant"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}

        [Header(Storm)]
        _StormLatitude("Storm Latitude", Range(0.05, 2.0)) = 0.5
        _StormScales("Storm Scales (XYZ)", Vector) = (1.52, 1.75, 0.75, 0)
        _StormIntensity("Storm Intensity", Float) = 10.0

        [Header(Animation)]
        _NoiseSpeed("Noise Speed", Float) = 1.0

        [Header(Bands)]
        _BandSmoothing("Band Smoothing", Float) = 0.003
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // ========================
        // Forward Lit
        // ========================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float4 _BaseMap_ST;
                float  _StormLatitude;
                float4 _StormScales;
                float  _StormIntensity;
                float  _NoiseSpeed;
                float  _BandSmoothing;
            CBUFFER_END

            // ----- Simplex Noise -----

            float3 mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float2 mod289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
            float3 permute(float3 x) { return mod289(((x * 34.0) + 1.0) * x); }

            float snoise(float2 v)
            {
                const float4 C = float4( 0.211324865405187,
                                          0.366025403784439,
                                         -0.577350269189626,
                                          0.024390243902439);

                float2 i  = floor(v + dot(v, C.yy));
                float2 x0 = v - i + dot(i, C.xx);

                float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);

                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;

                i = mod289(i);
                float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0))
                                         + i.x + float3(0.0, i1.x, 1.0));

                float3 m = max(0.5 - float3(dot(x0, x0),
                                             dot(x12.xy, x12.xy),
                                             dot(x12.zw, x12.zw)), 0.0);
                m = m * m;
                m = m * m;

                float3 xn = 2.0 * frac(p * C.www) - 1.0;
                float3 h  = abs(xn) - 0.5;
                float3 ox = floor(xn + 0.5);
                float3 a0 = xn - ox;

                m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

                float3 g;
                g.x  = a0.x  * x0.x   + h.x  * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;

                return 130.0 * dot(m, g);
            }

            // ----- Structs -----

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS  : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            // ----- Vertex -----

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // ----- Fragment -----

            half4 frag(Varyings IN) : SV_Target
            {
                // Project interpolated position back onto unit sphere
                float3 nor = normalize(IN.positionOS);

                float q = _Time.y * 0.001 * _NoiseSpeed;

                // Band noise — multi-octave distortion of latitude bands
                float srnd  = snoise((q + nor.yx) * 50.0)  / 5.0;
                      srnd += snoise((q + nor.yx) * 10.0)  / 2.0;
                      srnd += snoise((q + nor.yx) * 100.0) / 10.0;
                float rnd   = snoise((q + nor.xy) * 50.0)  / 50.0;

                // Storm swirl
                float lat      = _StormLatitude;
                float stormity = sqrt(max(0.0, 1.0 - abs(lat - nor.y) / lat) / 1.2);

                float s1 = snoise(nor.xy * _StormScales.x) * stormity;
                float s2 = snoise(nor.xy * _StormScales.y) * stormity;
                float s3 = snoise(nor.xy * _StormScales.z) * stormity;

                float storm = s1 * s2 * s3;
                float2 sv = 0.0;
                if (storm > 0.0)
                    sv = (nor * storm * storm * _StormIntensity).xy;

                nor.xy *= (1.0 - sv);

                // Sample band texture (latitude-based)
                float2 bandUV = (nor.yy + 1.0) * 0.5;

                float4 texColor = (
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                        bandUV + srnd / 100.0 + rnd / 10.0) +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                        bandUV + _BandSmoothing + srnd / 100.0) +
                    SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                        bandUV - _BandSmoothing + srnd / 100.0)
                ) / 3.0;

                half4 color = texColor * _BaseColor;

                // URP Main Light
                float3 normalWS    = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                float NdotL   = dot(normalWS, mainLight.direction);
                float diffuse = max(0.05, NdotL);

                color.rgb *= diffuse * mainLight.color * NdotL;
                color.a = 1.0;

                return color;
            }
            ENDHLSL
        }

        // ========================
        // Shadow Caster
        // ========================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionHCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 norWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(
                    ApplyShadowBias(posWS, norWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    OUT.positionHCS.z = min(OUT.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionHCS.z = max(OUT.positionHCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // ========================
        // Depth Only
        // ========================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionHCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}