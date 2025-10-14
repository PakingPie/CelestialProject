Shader "Custom/Atmosphere"
{
    Properties
    {
        _SunPosition("Sun Position", Vector) = (0, 0, 0, 0)
        _LightIntensity("Light Intensity", Float) = 10
        _PlanetRadius("Planet Radius", Float) = 1    // Earth radius in km
        _AtmosphereHeight("Atmosphere Height", Float) = 0.5 // Atmosphere height in km
        _RayleighBeta("Rayleigh Scattering Coefficients", Vector) = (0.0055, 0.013, 0.0224)
        _MieBeta("Mie Scattering Coefficients", Vector) = (0.021, 0.021, 0.021)
        _AmbientBeta("Ambient Coefficients", Color) = (0, 0, 0, 1)
        _AbsorptionBeta("Absorption Coefficients", Vector) = (0.0204, 0.0497, 0.00195)
        _G("G", Range(0, 1)) = 0.76
        _HeightRayleigh("Height Rayleigh", Float) = 8
        _HeightMie("Height Mie", Float) = 1.2
        _HeightAbsorption("Height Absorption", Float) = 30
        _AbsorptionFalloff("Absorption Falloff", Float) = 4

        _PrimarySteps("Primary Steps", Int) = 40
        _LightSteps("Light Steps", Int) = 4
    }

    SubShader
    {
        Pass
        {
            // Name "Universal Forward"
            Tags { "RenderPipeline"="UniversalPipeline" 
                "Queue"="Transparent" 
            "RenderType"="Transparent"}
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _LightIntensity;
                float3 _SunPosition;
                float _PlanetRadius;
                float _AtmosphereHeight;
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

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            
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

            float PhaseMie( float g, float c, float cc ) {
                float gg = g * g;
                
                float a = ( 1.0 - gg ) * ( 1.0 + cc );

                float b = 1.0 + gg - 2.0 * g * c;
                b *= sqrt( b );
                b *= 2.0 + gg;	
                
                return ( 3.0 / 8.0 / PI ) * a / b;
            }

            float PhaseRayleigh( float cc ) 
            {
                return ( 3.0 / 16.0 / PI ) * ( 1.0 + cc );
            }
            

            float2 RayIntersectSphere( float3 p, float3 dir, float r ) 
            {
                float b = dot( p, dir );
                float c = dot( p, p ) - r * r;
                
                float d = b * b - c;
                if ( d < 0.0 ) {
                    return float2( maxFloat, -maxFloat );
                }
                d = sqrt( d );

                return float2( -b - d, -b + d );
            }

            float SampleDensity( float3 p, float ph ) 
            {
                return exp( -max( length( p ) - _PlanetRadius, 0.0 ) / ph );
            }

            float optic( float3 p, float3 q, float ph ) 
            {
                float3 s = ( q - p ) / float( _LightSteps );
                float3 v = p + s * 0.5;

                float sum = 0.0;
                for ( int i = 0; i < _LightSteps; i++ ) 
                {
                    sum += SampleDensity( v, ph );
                    v += s;
                }
                sum *= length( s );
                
                return sum;
            }

            float3 InScattering( float3 o, float3 dir, float2 e, float3 l ) 
            {
                const float ph_ray = _HeightRayleigh * 1e-3;
                const float ph_mie = _HeightMie * 1e-3;

                const float3 k_ray = _RayleighBeta * 1e3;
                const float3 k_mie = _MieBeta * 1e3;
                const float k_mie_ex = 1.1;

                float3 sum_ray = 0; // total rayleigh
                float3 sum_mie = 0; // total mie

                float n_ray0 = 0.0; // 
                float n_mie0 = 0.0;

                // float opticalDepthI = 0;
                
                float len = ( e.y - e.x ) / float( _PrimarySteps );
                float3 s = dir * len;
                float3 v = o + dir * ( e.x + len * 0.5 );

                for ( int i = 0; i < _PrimarySteps; i++, v += s ) 
                {
                    float d_ray = SampleDensity( v, ph_ray ) * len;
                    float d_mie = SampleDensity( v, ph_mie ) * len;
                    
                    n_ray0 += d_ray;
                    n_mie0 += d_mie;

                    // #if 0
                        // float2 e = RayIntersectSphere( v, l, _PlanetRadius );
                        // e.x = max( e.x, 0.0 );
                        // if ( e.x < e.y ) 
                        // {
                        //     continue;
                        // }
                    // #endif

                    float2 f = RayIntersectSphere( v, l, _PlanetRadius + _AtmosphereHeight );
                    float3 u = v + l * f.y;

                    float n_ray1 = optic( v, u, ph_ray );
                    float n_mie1 = optic( v, u, ph_mie );

                    float3 att = exp( - ( n_ray0 + n_ray1 ) * k_ray - ( n_mie0 + n_mie1 ) * k_mie * k_mie_ex );

                    sum_ray += d_ray * att;
                    sum_mie += d_mie * att;
                }
                
                float c  = dot( dir, -l );
                float cc = c * c;
                float3 scatter =
                sum_ray * k_ray * PhaseRayleigh( cc ) +
                sum_mie * k_mie * PhaseMie( -_G, c, cc );// + n_ray0 * _AmbientBeta.rgb;
                
                
                return _LightIntensity * scatter;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 planetPos = unity_ObjectToWorld._m03_m13_m23;
                float3 cameraPosWS = _WorldSpaceCameraPos.xyz;
                float3 positionWS = IN.positionWS;
                float3 normalWS = normalize(IN.normalWS);
                // Camera to Pixel Direction
                float3 viewDir = -normalize(cameraPosWS - positionWS);

                float3 sunDir = GetMainLight(0).direction; // normalize(_SunPosition - planetPos);

                // // Get object scale
                // float3 scale = 0;
                // scale.x = length(unity_ObjectToWorld._m00_m10_m20);
                // scale.y = length(unity_ObjectToWorld._m01_m11_m21);
                // scale.z = length(unity_ObjectToWorld._m02_m12_m22);

                float2 inter1 = RayIntersectSphere(cameraPosWS - planetPos, viewDir, _PlanetRadius + _AtmosphereHeight);
                if(inter1.x > inter1.y)
                {
                    discard;
                }

                float2 inter2 = RayIntersectSphere(cameraPosWS - planetPos, viewDir, _PlanetRadius);
                inter1.y = min(inter1.y, inter2.x);
                float3 scatter = InScattering(cameraPosWS - planetPos, viewDir, inter1, sunDir);
                // scatter = 1 - exp(-scatter);
                scatter = pow(scatter, 1/2.2);
                // float4 color = float4(scatter + _AmbientBeta.rgb, 1);

                float fresnel = saturate(pow(dot(normalWS, viewDir), 1.0));
                float4 color = float4(scatter, (1 - fresnel));
                float NoL = saturate(pow(saturate(dot(normalWS, sunDir) + 0.3), 0.5));
                color *= clamp(fresnel + 0.1, 0, 1);
                color *= NoL;

                clip(0.5 - color.a);
                color.xyz *= _AmbientBeta.rgb;
                return color;
            }
            

            // float4 frag(Varyings IN) : SV_Target
            // {
                //     float3 planetPos = unity_ObjectToWorld._m03_m13_m23;
                //     float3 cameraPosWS = _WorldSpaceCameraPos.xyz;
                //     float3 positionWS = IN.positionWS;
                //     float3 normalWS = normalize(IN.normalWS);
                //     // Camera to Pixel Direction
                //     float3 viewDir = normalize(cameraPosWS - positionWS);

                //     float sceneDepth = SampleSceneDepth(IN.uv);
                //     float depth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                
                //     // viewDir = normalize(viewDir);
                //     // Main Light Direction
                //     float3 sunDir = GetMainLight(0).direction; // normalize(_SunPosition - planetPos);

                //     float4 color = float4(0, 0, 0, 1);
                //     float3 opacity = 0;

                //     color.rgb += CalculateScattering(
                //     cameraPosWS,
                //     viewDir,
                //     1e12,
                //     float3(0,0,0),
                //     sunDir,
                //     planetPos,
                //     _PlanetRadius,
                //     _PlanetRadius + _AtmosphereHeight,
                //     _PrimarySteps,
                //     _LightSteps,
                //     opacity
                //     );

                //     color.rgb = 1 - exp(-color.rgb);
                //     color.a = opacity.r + opacity.g + opacity.b;
                //     // // float3 mainLightDir = GetMainLight(0).direction;
                //     // float NoL = saturate(pow(saturate(dot(normalWS, sunDir)), 1.1));
                //     float fresnel = saturate(pow(dot(normalWS, viewDir), 1));
                //     color.rgb *= fresnel;

                
                //     // color.rgb *= NoL;

                //     return color;
            // }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            void frag(Varyings input, out float DEPTH : SV_DEPTH)
            {
                DEPTH =  LinearEyeDepth(SampleSceneDepth(input.uv), _ZBufferParams);
                // input.positionHCS.z / input.positionHCS.w; //
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode" = "DepthNormals"}

            ZWrite On

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                return float4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    // FallBack "Diffuse"
}
