Shader "Custom/GasGiant"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white"
        _Rotation("Rotation", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float3 _Rotation;
            CBUFFER_END

            float3 mod289(float3 x) 
            {
                return x - floor(x * (1.0 / 289.0)) * 289.0;
            }

            float2 mod289(float2 x) 
            {
                return x - floor(x * (1.0 / 289.0)) * 289.0;
            }

            float3 permute(float3 x) 
            {
                return mod289(((x * 34.0) + 1.0) * x);
            }

            float snoise(float2 v)
            {
                const float4 C = float4(0.211324865405187,  // (3.0-sqrt(3.0))/6.0
                0.366025403784439,                          // 0.5*(sqrt(3.0)-1.0)
                -0.577350269189626,                         // -1.0 + 2.0 * C.x
                0.024390243902439);                         // 1.0 / 41.0
                float2 i  = floor(v + dot(v, C.yy) );
                float2 x0 = v -   i + dot(i, C.xx);

                // Other corners
                float2 i1;
                //i1.x = step( x0.y, x0.x ); // x0.x > x0.y ? 1.0 : 0.0
                //i1.y = 1.0 - i1.x;
                i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
                // x0 = x0 - 0.0 + 0.0 * C.xx ;
                // x1 = x0 - i1 + 1.0 * C.xx ;
                // x2 = x0 - 1.0 + 2.0 * C.xx ;
                float4 x12 = x0.xyxy + C.xxzz;
                x12.xy -= i1;

                // Permutations
                i = mod289(i); // Avoid truncation effects in permutation
                float3 p = permute( permute( i.y + float3(0.0, i1.y, 1.0 ))
                + i.x + float3(0.0, i1.x, 1.0 ));

                float3 m = max(0.5 - float3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
                m = m*m ;
                m = m*m ;

                // Gradients: 41 points uniformly over a line, mapped onto a diamond.
                // The ring size 17*17 = 289 is close to a multiple of 41 (41*7 = 287)

                float3 x = 2.0 * frac(p * C.www) - 1.0;
                float3 h = abs(x) - 0.5;
                float3 ox = floor(x + 0.5);
                float3 a0 = x - ox;

                // Normalise gradients implicitly by scaling m
                // Approximation of: m *= inversesqrt( a0*a0 + h*h );
                m *= 1.79284291400159 - 0.85373472095314 * ( a0*a0 + h*h );

                // Compute final noise value at P
                float3 g;
                g.x  = a0.x  * x0.x  + h.x  * x0.y;
                g.yz = a0.yz * x12.xz + h.yz * x12.yw;
                return 130.0 * dot(m, g);
            }

            float snoise(float v)
            {
                return snoise(float2(v, v));
            }

            float hash( float n ) { return frac(sin(n) * 123.456789); }

            float noise( in float3 p )
            {
                float3 fl = floor( p );
                float3 fr = frac( p );
                fr = fr * fr * ( 3.0 - 2.0 * fr );

                float n = fl.x + fl.y * 157.0 + 113.0 * fl.z;
                return lerp( lerp( lerp( hash( n +   0.0), hash( n +   1.0 ), fr.x ),
                lerp( hash( n + 157.0), hash( n + 158.0 ), fr.x ), fr.y ),
                lerp( lerp( hash( n + 113.0), hash( n + 114.0 ), fr.x ),
                lerp( hash( n + 270.0), hash( n + 271.0 ), fr.x ), fr.y ), fr.z );
            }

            
            float noise( float a, float b )
            {
                return snoise(float2(a, b));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _BaseMap);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 color = 0;

                // float2 screenSize = _ScreenParams.xy;
                // float3 ro = _WorldSpaceCameraPos.xyz;
                // float3 rd = normalize(_WorldSpaceCameraPos.xyz - IN.positionOS);

                // float b = dot(ro, rd);
                // float c = dot(ro, ro) - 1.0;
                // float h = b * b - c;
                // float t = -b - sqrt(abs(h));

                float3 nor = IN.positionOS;


                float rot = _Rotation.z;
                nor = (mul(float3x3(1.0, 0.0, 0.0, 
                0.0, cos(rot), - sin(rot), 
                0.0, sin(rot), cos(rot)), nor)).xyz;

                rot = _Rotation.x;

                nor = (mul(float3x3(cos(rot), 0.0, sin(rot), 
                0.0, 1.0, 0.0,
                -sin(rot), 0.0, -cos(rot)), nor)).xyz;
                
                rot = _Time.y * 0.001;

                float2 pos = (mul(float3x3(cos(rot), 0.0, sin(rot), 
                0.0, 1.0, 0.0, 
                -sin(rot), 0.0, cos(rot)), float3(nor.xy, -10.0))).xy;

                float time = _Time.y;
                float q = time * 0.001;

                
                float srnd = snoise((q + nor.yx) * 50.0) / 5.0;
                srnd += snoise((q + nor.yx) * 10.0) / 2.0;
                srnd += snoise((q + nor.yx) * 100.0) / 10.0;
                float rnd = snoise((q + nor.xy) * 50.0) / 50.0;

                float lat = 0.5;
                float stormity = sqrt(max(0.0, 1.0 - abs(lat - nor.y) / lat) / 1.2);
                float s1 = snoise(nor.xy * 1.520) * stormity;
                float s2 = snoise(nor.xy * 1.75) * stormity;
                float s3 = snoise(nor.xy * 0.75) * stormity;
                
                float storm = s1 * s2 *s3;
                float2 sv;
                if (storm > 0.0) {
                    sv = (nor * storm * storm * 10.0).xy;
                    } else {
                    sv = 0.0;
                }
                
                nor.xy *= (1.0 - sv);

                float4 texColor = (SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ((nor.yy + 1.0) * 0.5).yy + srnd / 100.0 + srnd / 10.0) + 
                SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ((nor.yy + 1.0) * 0.5).yy + 0.003 + srnd / 100.0) +
                SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, ((nor.yy + 1.0) * 0.5).yy - 0.003 + srnd / 100.0)) / 3.0;
                color = texColor; 
                

                // float lightColor = dot(nor.xyz, float3(15.25, 10.5, 3.0));
                // float3 col = float3(0.15, 0.09, 0.05);
                // float lum = lightColor / 20.0;

                // if (h <= 0.0) 
                // {
                    //     lum *= abs(log(abs(h)));
                // }

                // color.rgb = max(color.rgb, 0.0);
                // color.a = 1.0;

                // if (h <= 0.0) 
                // {
                    //     color.rgb = lightColor * lum;
                // }
                // else 
                // {        	
                    //     color.rgb = color.rgb * (max(0.1, lightColor / 10.0)) + col * lum;
                // }

                return color;
            }
            ENDHLSL
        }
    }
}
