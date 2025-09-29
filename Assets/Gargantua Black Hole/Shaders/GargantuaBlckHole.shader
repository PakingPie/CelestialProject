Shader "Custom/GargantuaBlckHole"
{
    Properties
    {
        _MainColor("Main Color", Color) = (1,1,1,1)
        _GargantuaPrevTex("Previous Gargantua Texture", 2D) = "black" {}
        _GargantuaTex("Gargantua Texture", 2D) = "black" {}
        _NoiseTex("Noise Texture", 2D) = "white" {}
        _HaloTex("Halo Texture", 2D) = "white" {}

        _BlackHoleScale("Black Hole Scale", Vector) = (1,1,1,1)
        _BlackHoleCenter("Black Hole Center", Vector) = (0,0,0,0)

        [Toggle(_)] TEMPORTAL_AA("Temporal AA", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass // Pass 0
        {
            HLSLPROGRAM

            #pragma multi_compile _ TEMPORTAL_AA

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define ITERATIONS 100          //Increase for less grainy result

            TEXTURE2D(_NoiseTex);
            TEXTURE2D(_HaloTex);    
            TEXTURE2D(_GargantuaPrevTex);

            SAMPLER(sampler_NoiseTex);
            SAMPLER(sampler_HaloTex);
            SAMPLER(sampler_GargantuaPrevTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float3 _BlackHoleScale;
                float3 _BlackHoleCenter;
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

            float2 intersectSphere(float3 rayOrigin, float3 rayDir, float3 centre, float radius) 
            {

                float3 offset = rayOrigin - centre;
                const float a = 1;
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - radius * radius;

                float discriminant = b * b - 4 * a*c;
                // No intersections: discriminant < 0
                // 1 intersection: discriminant == 0
                // 2 intersections: discriminant > 0
                if (discriminant > 0) {
                    float s = sqrt(discriminant);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar = (-b + s) / (2 * a);

                    if (dstToSphereFar >= 0) {
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }
                // Ray did not intersect sphere
                return float2(3.402823466e+38, 0);
            }

            float remap(float v, float minOld, float maxOld, float minNew, float maxNew) 
            {
                return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
            }


            float4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.texcoord;

                float aspect = _ScreenParams.x / _ScreenParams.y;

                float depth = SampleSceneDepth(screenUV);

                float3 positionWS = ComputeWorldSpacePosition(screenUV, depth, UNITY_MATRIX_I_VP);

                float3 viewPos = _WorldSpaceCameraPos;
                float3 viewDir = normalize(positionWS - viewPos);

                const float far = 10.0;//length(_BlackHoleScale) * 2.0;
                
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
                    float3 previous = pow(SAMPLE_TEXTURE2D_X(_GargantuaPrevTex, sampler_GargantuaPrevTex, screenUV).rgb, 1.0 / p);
                    
                    color = pow(color, 1.0 / p);

                    float blendWeight = 0.9;

                    color = lerp(color, previous, blendWeight);
                    
                    color = pow(color, p);
                #endif

                
                color *= 0.1;

                float4 blackHoleColor = float4(saturate(color), 1);


                float sphereRadius = 0.5 * min(min(_BlackHoleScale.x, _BlackHoleScale.y), _BlackHoleScale.z);
                float2 outerSphereIntersection = intersectSphere(viewPos, viewDir, _BlackHoleCenter, sphereRadius);


                float3 distortedRayDir = normalize(raypos - viewPos);
                float4 rayCameraSpace = mul(unity_WorldToCamera, float4(distortedRayDir,0));
                float4 rayUVProjection = mul(unity_CameraProjection, float4(rayCameraSpace));
                float2 distortedScreenUV = rayUVProjection.xy + 1 * 0.5;

                // Screen and object edge transitions
                float edgeFadex = smoothstep(0, 0.25, 1 - abs(remap(screenUV.x, 0, 1, -1, 1)));
                float edgeFadey = smoothstep(0, 0.25, 1 - abs(remap(screenUV.y, 0, 1, -1, 1)));
                float t = saturate(remap(outerSphereIntersection.y, sphereRadius, 2 * sphereRadius, 0, 1)) * edgeFadex * edgeFadey;
                distortedScreenUV = lerp(screenUV, distortedScreenUV, t);
                
                float4 oriColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, distortedScreenUV);

                return blackHoleColor;
            }
            ENDHLSL
        }

        Pass // Pass 1
        {
            Name "Gaussian Blur Horizontal"
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GargantuaTex);
            SAMPLER(sampler_GargantuaTex);

            
            float3 ColorFetch(float2 coord)
            {
                return SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord).rgb;
            }

            float weights[5] = {0.19638062, 0.29675293, 0.09442139, 0.01037598, 0.00025940};
            float offsets[5] = {0.00000000, 1.41176471, 3.29411765, 5.17647059, 7.05882353};

            float4 frag(Varyings IN) : SV_Target
            {   
                float2 uv = IN.texcoord;
                float3 color = 0;
                float weightSum = 0.0;
                
                if (uv.x < 0.52)
                {
                    color += ColorFetch(uv) * weights[0];
                    weightSum += weights[0];

                    for(int i = 1; i < 5; i++)
                    {
                        float2 offset = offsets[i] / _ScreenParams.xy;
                        color += ColorFetch(uv + offset * float2(0.5, 0.0)) * weights[i];
                        color += ColorFetch(uv - offset * float2(0.5, 0.0)) * weights[i];
                        weightSum += weights[i] * 2.0;
                    }

                    color /= weightSum;
                }

                return float4(color, 1.0);
            }

            ENDHLSL
        }

        Pass // Pass 2
        {
            Name "Gaussian Blur Vertical"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GargantuaTex);
            SAMPLER(sampler_GargantuaTex);

            
            float3 ColorFetch(float2 coord)
            {
                return SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord).rgb;
            }

            float weights[5] = {0.19638062, 0.29675293, 0.09442139, 0.01037598, 0.00025940};
            float offsets[5] = {0.00000000, 1.41176471, 3.29411765, 5.17647059, 7.05882353};

            float4 frag(Varyings IN) : SV_Target
            {   
                float2 uv = IN.texcoord;
                float3 color = 0;
                float weightSum = 0.0;
                
                if (uv.x < 0.52)
                {
                    color += ColorFetch(uv) * weights[0];
                    weightSum += weights[0];

                    for(int i = 1; i < 5; i++)
                    {
                        float2 offset = offsets[i] / _ScreenParams.xy;
                        color += ColorFetch(uv + offset * float2(0.0, 0.5)) * weights[i];
                        color += ColorFetch(uv - offset * float2(0.0, 0.5)) * weights[i];
                        weightSum += weights[i] * 2.0;
                    }

                    color /= weightSum;
                }

                return float4(color,1.0);
            }

            ENDHLSL
        }

        Pass // Pass 3
        {
            Name "Combine"
            HLSLPROGRAM

            #pragma multi_compile _ TEMPORTAL_AA

            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GargantuaTex);
            TEXTURE2D(_GarganturaBlurred);

            SAMPLER(sampler_GargantuaTex);

            float4 cubic(float x)
            {
                float x2 = x * x;
                float x3 = x2 * x;
                float4 w;
                w.x = -x3 + 3.0 * x2 - 3.0 * x + 1.0;
                w.y =  3.0 * x3 - 6.0 * x2 + 4.0;
                w.z = -3.0 * x3 + 3.0 * x2 + 3.0 * x + 1.0;
                w.w =  x3;
                return w / 6.0;
            }

            float4 BicubicTexture(in Texture2D tex, in float2 coord)
            {
                float2 resolution = _ScreenParams.xy;

                coord *= resolution;

                float fx = frac(coord.x);
                float fy = frac(coord.y);
                coord.x -= fx;
                coord.y -= fy;

                fx -= 0.5;
                fy -= 0.5;

                float4 xcubic = cubic(fx);
                float4 ycubic = cubic(fy);

                float4 c = float4(coord.x - 0.5, coord.x + 1.5, coord.y - 0.5, coord.y + 1.5);
                float4 s = float4(xcubic.x + xcubic.y, xcubic.z + xcubic.w, ycubic.x + ycubic.y, ycubic.z + ycubic.w);
                float4 offset = c + float4(xcubic.y, xcubic.w, ycubic.y, ycubic.w) / s;

                float4 sample0 = SAMPLE_TEXTURE2D_X(tex, sampler_GargantuaTex, float2(offset.x, offset.z) / resolution);
                float4 sample1 = SAMPLE_TEXTURE2D_X(tex, sampler_GargantuaTex, float2(offset.y, offset.z) / resolution);
                float4 sample2 = SAMPLE_TEXTURE2D_X(tex, sampler_GargantuaTex, float2(offset.x, offset.w) / resolution);
                float4 sample3 = SAMPLE_TEXTURE2D_X(tex, sampler_GargantuaTex, float2(offset.y, offset.w) / resolution);

                float sx = s.x / (s.x + s.y);
                float sy = s.z / (s.z + s.w);

                return lerp( lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);
            }

            float3 ColorFetch(float2 coord)
            {
                return SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, coord).rgb;   
            }

            float3 BloomFetch(float2 coord)
            {
                return BicubicTexture(_GarganturaBlurred, coord).rgb;
            }

            float3 Grab(float2 coord, const float octave, const float2 offset)
            {
                float scale = exp2(octave);
                
                coord /= scale;
                coord -= offset;

                return BloomFetch(coord);
            }

            float2 CalcOffset(float octave)
            {
                float2 offset = 0.0;
                
                float2 padding = 10.0 / _ScreenParams.xy;
                
                offset.x = -min(1.0, floor(octave / 3.0)) * (0.25 + padding.x);
                
                offset.y = -(1.0 - (1.0 / exp2(octave))) - padding.y * octave;

                offset.y += min(1.0, floor(octave / 3.0)) * 0.35;
                
                return offset;   
            }

            float3 GetBloom(float2 coord)
            {
                float3 bloom = 0.0;
                
                //Reconstruct bloom from multiple blurred images
                bloom += Grab(coord, 1.0, CalcOffset(0.0)) * 1.0;
                bloom += Grab(coord, 2.0, CalcOffset(1.0)) * 1.5;
                bloom += Grab(coord, 3.0, CalcOffset(2.0)) * 1.0;
                bloom += Grab(coord, 4.0, CalcOffset(3.0)) * 1.5;
                bloom += Grab(coord, 5.0, CalcOffset(4.0)) * 1.8;
                bloom += Grab(coord, 6.0, CalcOffset(5.0)) * 1.0;
                bloom += Grab(coord, 7.0, CalcOffset(6.0)) * 1.0;
                bloom += Grab(coord, 8.0, CalcOffset(7.0)) * 1.0;

                return bloom;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float3 color = ColorFetch(uv);
                
                
                color += GetBloom(uv) * 0.08;
                
                color *= 200.0;
                

                //Tonemapping and color grading
                // color = pow(color, 1.5);
                // color = color / (1.0 + color);
                // color = pow(color, 1.0 / 1.5);

                
                // color = lerp(color, color * color * (3.0 - 2.0 * color), 1.0);
                // color = pow(color, float3(1.3, 1.20, 1.0));    

                // color = saturate(color * 1.01);
                
                // color = pow(color, 0.7 / 2.2);

                return float4(color, 1.0);
                // return 1;
            }
            ENDHLSL
        }
    }
}
