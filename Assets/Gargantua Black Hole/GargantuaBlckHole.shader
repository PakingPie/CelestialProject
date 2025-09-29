Shader "Custom/GargantuaBlckHole"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _MainColor("Main Color", Color) = (1,1,1,1)
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _HaloTex("Halo Texture", 2D) = "white" {}

        [Toggle(_)] TEMPORTAL_AA("Temporal AA", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma multi_compile _ TEMPORTAL_AA

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define ITERATIONS 200          //Increase for less grainy result

            TEXTURE2D(_NoiseTex);
            TEXTURE2D(_HaloTex);    
            TEXTURE2D(_MainTex);

            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_NoiseTex);
            SAMPLER(sampler_HaloTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
            CBUFFER_END

            


            // From Inigo Quilez
            float noise( in float3 x )
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3 - 2 * f);
                float2 uv = (p.xy + float2(37, 17)*p.z) + f.xy;
                float2 rg = SAMPLE_TEXTURE2D_X_LOD( _NoiseTex, sampler_NoiseTex, (uv + 0.5) / 256, 0).yx;
                return -1 + 2 * lerp( rg.x, rg.y, f.z );
            }


            float rand(float2 coord)
            {
                return saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
            }

            float pcurve( float x, float a, float b )
            {
                float k = pow(a + b, a + b) / (pow(a, a) * pow(b, b));
                return k * pow(x, a) * pow(1.0 - x, b);
            }

            float sdTorus(float3 p, float2 t)
            {
                float2 q = float2(length(p.xz) - t.x, p.y);
                return length(q)-t.y;
            }

            float sdSphere(float3 p, float r)
            {
                return length(p)-r;
            }

            void Haze(inout float3 color, float3 pos, float alpha)
            {
                float2 t = float2(1.0, 0.01);

                float torusDist = length(sdTorus(pos + float3(0.0, -0.05, 0.0), t));

                float bloomDisc = 1.0 / (pow(torusDist, 2.0) + 0.001);
                float3 col = _MainColor;
                bloomDisc *= length(pos) < 0.5 ? 0.0 : 1.0;

                color += col * bloomDisc * (2.9 / float(ITERATIONS)) * (1.0 - alpha * 1.0);
            }

            
            void GasDisc(inout float3 color, inout float alpha, float3 pos)
            {
                float discRadius = 3.2;
                float discWidth = 5.3;
                float discInner = discRadius - discWidth * 0.5;
                float discOuter = discRadius + discWidth * 0.5;
                
                float3 origin = float3(0.0, 0.0, 0.0);
                float3 discNormal = normalize(float3(0.0, 1.0, 0.0));
                float discThickness = 0.1;

                float distFromCenter = distance(pos, origin);
                float distFromDisc = dot(discNormal, pos - origin);
                
                float radialGradient = 1.0 - saturate((distFromCenter - discInner) / discWidth * 0.5);

                float coverage = pcurve(radialGradient, 4.0, 0.9);

                discThickness *= radialGradient;
                coverage *= saturate(1.0 - abs(distFromDisc) / discThickness);

                float3 dustColorLit = _MainColor;
                float3 dustColorDark = float3(0.0, 0.0, 0.0);

                float dustGlow = 1.0 / (pow(1.0 - radialGradient, 2.0) * 290.0 + 0.002);
                float3 dustColor = dustColorLit * dustGlow * 8.2;

                coverage = saturate(coverage * 0.7);


                float fade = pow((abs(distFromCenter - discInner) + 0.4), 4.0) * 0.04;
                float bloomFactor = 1.0 / (pow(distFromDisc, 2.0) * 40.0 + fade + 0.00002);
                float3 b = dustColorLit * pow(bloomFactor, 1.5);
                
                b *= lerp(float3(1.7, 1.1, 1.0), float3(0.5, 0.6, 1.0), pow(radialGradient, 2.0));
                b *= lerp(float3(1.7, 0.5, 0.1), 1.0, pow(radialGradient, 0.5));

                dustColor = lerp(dustColor, b * 150.0, saturate(1.0 - coverage * 1.0));
                coverage = saturate(coverage + bloomFactor * bloomFactor * 0.1);
                
                if (coverage < 0.01)
                {
                    return;   
                }
                
                
                float3 radialCoords;
                radialCoords.x = distFromCenter * 1.5 + 0.55;
                radialCoords.y = atan2(-pos.x, -pos.z) * 1.5;
                radialCoords.z = distFromDisc * 1.5;

                radialCoords *= 0.95;
                
                float speed = 0.06;
                
                float noise1 = 1.0;
                float3 rc = radialCoords + 0.0;               rc.y += _Time.y * speed;
                noise1 *= noise(rc * 3.0) * 0.5 + 0.5;      rc.y -= _Time.y * speed;
                noise1 *= noise(rc * 6.0) * 0.5 + 0.5;      rc.y += _Time.y * speed;
                noise1 *= noise(rc * 12.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;
                noise1 *= noise(rc * 24.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;

                float noise2 = 2.0;
                rc = radialCoords + 30.0;
                noise2 *= noise(rc * 3.0) * 0.5 + 0.5;      rc.y += _Time.y * speed;
                noise2 *= noise(rc * 6.0) * 0.5 + 0.5;      rc.y -= _Time.y * speed;
                noise2 *= noise(rc * 12.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;
                noise2 *= noise(rc * 24.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;
                noise2 *= noise(rc * 48.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;
                noise2 *= noise(rc * 92.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;

                dustColor *= noise1 * 0.998 + 0.002;
                coverage *= noise2;
                
                radialCoords.y += _Time.y * speed * 0.5;
                
                dustColor *= pow(SAMPLE_TEXTURE2D_X(_HaloTex, sampler_HaloTex, radialCoords.yx * float2(0.15, 0.27)).rgb, 2.0) * 4.0;

                coverage = saturate(coverage * 1200.0 / float(ITERATIONS));
                dustColor = max(0, dustColor);

                coverage *= pcurve(radialGradient, 4.0, 0.9);

                color = (1.0 - alpha) * dustColor * coverage + color;

                alpha = (1.0 - alpha) * coverage + alpha;
            }

            float3 rotate(float3 p, float x, float y, float z)
            {
                float3x3 matx = float3x3(1.0, 0.0, 0.0,
                0.0, cos(x), sin(x),
                0.0, -sin(x), cos(x));

                float3x3 maty = float3x3(cos(y), 0.0, -sin(y),
                0.0, 1.0, 0.0,
                sin(y), 0.0, cos(y));

                float3x3 matz = float3x3(cos(z), sin(z), 0.0,
                -sin(z), cos(z), 0.0,
                0.0, 0.0, 1.0);

                p = mul(matx, p);
                p = mul(maty, p);
                p = mul(matz, p);

                return p;
            }

            void WarpSpace(inout float3 eyevec, inout float3 raypos)
            {
                float3 origin = float3(0.0, 0.0, 0.0);

                float singularityDist = distance(raypos, origin);
                float warpFactor = 1.0 / (pow(singularityDist, 2.0) + 0.000001);

                float3 singularityVector = normalize(origin - raypos);
                
                float warpAmount = 5.0;

                eyevec = normalize(eyevec + singularityVector * warpFactor * warpAmount / float(ITERATIONS));
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.texcoord;

                float aspect = _ScreenParams.x / _ScreenParams.y;

                float depth = SampleSceneDepth(screenUV);

                float3 positionWS = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                float3 viewPos = _WorldSpaceCameraPos;
                float3 viewDir = normalize(positionWS - viewPos);

                const float far = 15.0;
                
                float dither = rand(screenUV) * 2.0;

                float alpha = 0.0;
                float3 raypos = viewPos + viewDir * dither * far / float(ITERATIONS);

                float3 color = float3(0.0, 0.0, 0.0);
                for (int i = 0; i < ITERATIONS; i++)
                {
                    WarpSpace(viewDir, raypos);
                    raypos += viewDir * far / float(ITERATIONS);
                    GasDisc(color, alpha, raypos);
                    Haze(color, raypos, alpha);
                }
                #if TEMPORTAL_AA
                    const float p = 1.0;
                    float3 previous = pow(SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, screenUV).rgb, 1.0 / p);
                    
                    color = pow(color, 1.0 / p);
                    
                    float blendWeight = 0.9 ;
                    
                    color = lerp(color, previous, blendWeight);
                    
                    color = pow(color, p);
                #endif

                
                color *= 0.1;
                
                return float4(saturate(color), 1);
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GargantuaTex);
            SAMPLER(sampler_GargantuaTex);

            CBUFFER_START(UnityPerMaterial)
            CBUFFER_END

            
            float3 Grab1(float2 coord, const float octave, const float2 offset)
            {
                float scale = exp2(octave);
                
                coord += offset;
                coord *= scale;

                if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                {
                    return 0;   
                }
                
                float3 color = SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord).rgb;

                return color;
            }

            
            float3 Grab4(float2 coord, const float octave, const float2 offset)
            {
                float scale = exp2(octave);
                
                coord += offset;
                coord *= scale;

                if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                {
                    return 0.0;   
                }
                
                float3 color = 0.0;
                float weights = 0.0;
                
                const int oversampling = 4;
                
                for (int i = 0; i < oversampling; i++)
                {    	    
                    for (int j = 0; j < oversampling; j++)
                    {
                        float2 off = (float2(i, j) / _ScreenParams.xy + float2(0.0, 0.0) / _ScreenParams.xy) * scale / float(oversampling);
                        color += SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord + off).rgb;

                        weights += 1.0;
                    }
                }
                
                color /= weights;
                
                return color;
            }

            float3 Grab8(float2 coord, const float octave, const float2 offset)
            {
                float scale = exp2(octave);
                
                coord += offset;
                coord *= scale;

                if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                {
                    return 0.0;   
                }
                
                float3 color = 0.0;
                float weights = 0.0;
                
                const int oversampling = 8;
                
                for (int i = 0; i < oversampling; i++)
                {    	    
                    for (int j = 0; j < oversampling; j++)
                    {
                        float2 off = (float2(i, j) / _ScreenParams.xy + float2(0.0, 0.0) / _ScreenParams.xy) * scale / float(oversampling);
                        color += SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord + off).rgb;
                        

                        weights += 1.0;
                    }
                }
                
                color /= weights;
                
                return color;
            }

            float3 Grab16(float2 coord, const float octave, const float2 offset)
            {
                float scale = exp2(octave);
                
                coord += offset;
                coord *= scale;

                if (coord.x < 0.0 || coord.x > 1.0 || coord.y < 0.0 || coord.y > 1.0)
                {
                    return 0.0;   
                }
                
                float3 color = 0.0;
                float weights = 0.0;
                
                const int oversampling = 16;
                
                for (int i = 0; i < oversampling; i++)
                {    	    
                    for (int j = 0; j < oversampling; j++)
                    {
                        float2 off = (float2(i, j) / _ScreenParams.xy + float2(0.0, 0.0) / _ScreenParams.xy) * scale / float(oversampling);
                        color += SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord + off).rgb;
                        

                        weights += 1.0;
                    }
                }
                
                color /= weights;
                
                return color;
            }

            float2 CalcOffset(float octave)
            {
                float2 offset = 0.0;
                
                float2 padding = float2(10.0, 10.0) / _ScreenParams.xy;
                
                offset.x = -min(1.0, floor(octave / 3.0)) * (0.25 + padding.x);
                
                offset.y = -(1.0 - (1.0 / exp2(octave))) - padding.y * octave;

                offset.y += min(1.0, floor(octave / 3.0)) * 0.35;
                
                return offset;   
            }


            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                float3 color = 0.0;

                /*
                Create a mipmap tree thingy with padding to prevent leaking bloom
                
                Since there's no mipmaps for the previous buffer and the reduction process has to be done in one pass,
                oversampling is required for a proper result
                */
                color += Grab1(uv, 1.0, 0.0);
                color += Grab4(uv, 2.0, CalcOffset(1.0));
                color += Grab8(uv, 3.0, CalcOffset(2.0));
                color += Grab16(uv, 4.0, CalcOffset(3.0));
                color += Grab16(uv, 5.0, CalcOffset(4.0));
                color += Grab16(uv, 6.0, CalcOffset(5.0));
                color += Grab16(uv, 7.0, CalcOffset(6.0));
                color += Grab16(uv, 8.0, CalcOffset(7.0));

                return float4(saturate(color), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Gaussian Blur"
        }
    }
}
