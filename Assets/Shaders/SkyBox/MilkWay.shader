Shader "Custom/MilyWay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ZoomScale("Zoom Scale", Float) = 1.0
        _BandWidth("Band Half Width", Range(0.0, 1.0)) = 0.22
        _BandSoftness("Band Softness", Range(0.01, 0.5)) = 0.15
        _BandLongitudeFade("Band Longitude Fade", Range(0.0, 1.0)) = 0.25
        _BandBreakup("Band Breakup", Range(0.0, 1.0)) = 0.35
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _ZoomScale;
            float _BandWidth;
            float _BandSoftness;
            float _BandLongitudeFade;
            float _BandBreakup;

            #define MILKY_WAY_ITERATIONS 5
            #define MILKY_WAY_SEED 107.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv = IN.texcoord;
                OUT.viewDirWS = TransformObjectToWorldDir(IN.positionOS.xyz);
                return OUT;
            }

            float3 hash33(float3 p, float seed)
            {
                p += float3(seed * float3(-395.23,705.23,966.37));
                uint3 q = asuint(p);
                q = ((q>>16u)^q.yzx)*1111111111u;
                q = ((q>>16u)^q.yzx)*1111111111u;
                q = ((q>>16u)^q.yzx)*1111111111u;
                return q/float(-1U);
            }

            float sphere( float3 p, float seed, float entropyPos, float rad, float entropyR, float sm )
            {
                float3 uvLoc = frac(p)-0.5, offset, bias;
                float c = 0.0, r = 0.0, l = 0.0, id;
                for(float dz=-1.0; dz<=1.0; dz++){
                    for(float dy=-1.0; dy<=1.0; dy++){
                        for(float dx=-1.0; dx<=1.0; dx++){
                            offset = hash33(floor(p)+float3(dx,dy,dz), seed*2.0+0.15)-0.5;
                            id = hash33(offset, 13.17+seed).x;
                            r = (hash33(floor(p)+float3(dx,dy,dz), seed+13.17)-0.5).y*entropyR + rad;
                            l = length(float3(dx,dy,dz)+offset*entropyPos-uvLoc);
                            c = max(c,smoothstep(r+sm, r-sm, l)*id);
                        }
                    }
                }
                return c;
            }

            float sphereNoise( float3 direction, float seed, float softness )
            {
                return sphere(direction, seed, 1.0, 0.5, 0.5, softness);
            }

            float stars( float3 dir, float seed, float curve )
            {
                float f, ff = 0.0;
                float s = 5e2 / _ZoomScale / _ScreenParams.y;
                float a = 2e-3*_ScreenParams.x * _ZoomScale * _ZoomScale;
                f = sphere(dir*350.0, seed+254.564, 1.0, 0.45*s, 0.025, 0.1);
                ff += pow(f,1000.0*curve)*1.5;
                f = sphere(dir*350.0, seed+26.274, 1.0, 0.25*s, 0.25, 0.05);
                ff += pow(f,4.0*curve)*0.5;
                f = sphere(dir*450.0, seed+656.344, 1.0, 0.25*s, 0.25, 0.3);
                ff += pow(f,1.0*curve)*0.25;    
                return ff*a;
            }

            float nebula( float3 dir, float seed, float steps, float softness, float ratioA, float ratioF )
            {
                float a=1.0, f=1.0, n=0.0, d=0.0, l=length(dir);
                for(float i=1.0; i<=steps; i++)
                {
                    n += pow(sphereNoise(dir*f,seed+i*7.343, softness*0.9+0.1),softness*0.9+0.1)*a;
                    d += a;
                    a *= ratioA;
                    f *= ratioF;
                }
                return n;
                
            }

            float3 projectionTo ( float3 axis, float3 orient, float3 direction )
            {
                float3 x = normalize(cross(orient,axis));
                float3 y = normalize(cross(x,orient));
                float3 z = normalize(cross(y,x));
                float3 d = normalize(direction);
                return float3(dot(x,d), dot(y,d), dot(z,d));
            }

            float3 projection ( float3 axis, float3 vec )
            {
                return normalize(cross(axis,cross(vec,axis)));
            }

            float3 getFar(float3 uvToSpace)
            {
                // Milky Way Core, Periphery, and Nebulae
                float core = 0.0, periphery = 0.0, neb = 0.0;
                float3 galaxyAxis = normalize(float3(0.4, 1.0, -0.2));
                float3 galaxyDirection = float3(0.0, 0.0, 1.0);
                float3 dir = projectionTo(galaxyAxis, galaxyDirection, uvToSpace);
                float3 nebulaAbs = float3(0.7, 0.85, 0.95);
                float3 proj = projection(galaxyAxis, uvToSpace);
                float3 col = 0;

                // Band Masking (currently disabled)
                float latitude = abs(dir.y);
                float band = smoothstep(_BandWidth + _BandSoftness, _BandWidth, latitude);
                float longitude = atan2(dir.x, dir.z) * (1.0 / PI);
                float longMask = smoothstep(_BandLongitudeFade, 1.0, 1.0 - abs(longitude));
                float breakup = lerp(1.0, sphereNoise(dir * 2.0, 991.123 + MILKY_WAY_SEED, 0.35), _BandBreakup);
                float bandMask = band * longMask * breakup;

                // Core
                float3 coreColor = float3(0.99, 0.95, 0.9);
                float coreWidth = 0.22;
                float coreHeight = 0.7;
                float coreSmooth = 0.7;
                float coreMask = 
                smoothstep(0, coreSmooth, dot(uvToSpace, -galaxyAxis + proj * coreHeight) + coreWidth) * 
                smoothstep(0, coreSmooth, dot(uvToSpace, galaxyAxis + proj * coreHeight) + coreWidth);
                if(coreMask > 0)
                {
                    core = max(0, pow(coreMask, 3.0) * 5.0 - 0.5 * nebula(dir * 5.0, 291.432 + MILKY_WAY_SEED, float(MILKY_WAY_ITERATIONS), 0.15, 0.7, 1.8));
                }

                // Glow
                float3 glowColor = float3(0.99, 0.95, 0.9);
                float glowWidth=0.47;
                float glowHeight=0.3;
                float glowSmooth=0.7;
                float glowMask = 
                smoothstep(0, glowSmooth,dot(uvToSpace, -galaxyAxis + proj *glowHeight) + glowWidth) * 
                smoothstep(0, glowSmooth,dot(uvToSpace, galaxyAxis + proj*glowHeight) + glowWidth);

                // Periphery
                float3 peripheryColor = float3(0.1, 0.3, 0.99);
                float peripheryWidth = 0.22;
                float peripheryHeight = 0.19;
                float peripherySmooth = 0.9;
                float peripheryMask =
                smoothstep(0, peripherySmooth, dot(uvToSpace, -galaxyAxis+proj * peripheryHeight) + peripheryWidth) * 
                smoothstep(0, peripherySmooth, dot(uvToSpace, galaxyAxis + proj * peripheryHeight) + peripheryWidth);
                peripheryMask = pow(peripheryMask, 0.4);
                if(peripheryMask > 0)
                {
                    periphery = max(0, peripheryMask * 4.0 - 1.0 * nebula(dir * 5.0, 583.457 + MILKY_WAY_SEED, float(MILKY_WAY_ITERATIONS), 0.1, 0.6, 2.1));
                }

                // Nebulae
                float nebulaWidth=0.75;
                float nebulaHeight=0.13;
                float nebulaSmooth=0.8;
                float nebulaMask =
                smoothstep(0, nebulaSmooth, dot(uvToSpace, -galaxyAxis + proj * nebulaHeight) + nebulaWidth) * 
                smoothstep(0, nebulaSmooth, dot(uvToSpace, galaxyAxis + proj * nebulaHeight) + nebulaWidth);
                if(nebulaMask > 0) neb = max(0, nebulaMask * 9.0 - 1.5*nebula((dir) * 4.0, 1276.12 + MILKY_WAY_SEED, float(MILKY_WAY_ITERATIONS), 0.2, 0.6, 2.1));
                
                // Combine
                col += pow(core, 3.0) * coreColor * 0.002;
                col += pow(glowMask, 2.0) * glowColor * 0.07;
                col *= exp(-periphery * nebulaAbs * 0.2);
                col += periphery * peripheryColor * 0.05;
                col *= exp(-pow(neb * 0.7, 5.1) * nebulaAbs * 0.0015);
                col *= bandMask;
                col += stars(uvToSpace * 0.5, 968.148, 2.0) * 0.07;

                return col;
            }

            float3 frameSubProcessing(float3 viewDirWS)
            {
                float3 c = 0;
                float3 rayDir = normalize(viewDirWS);
                c += getFar(rayDir);
                c *= 1.0;
                return 1.0 - exp(-c * 1.5);
            }

            float3 frameProcessing(float3 viewDirWS, int subs)
            {
                return frameSubProcessing(viewDirWS);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 color = frameProcessing(IN.viewDirWS, 1);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}