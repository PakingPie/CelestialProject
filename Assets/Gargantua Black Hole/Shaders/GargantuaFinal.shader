Shader "Custom/GargantuaFinal"
{
    Properties
    {
        _GargantuaTex("Gargantua Texture", 2D) = "black" {}
        _GarganturaBlurred("Blurred Gargantua Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass // Pass 3
        {
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
                

                // Tonemapping and color grading
                color = pow(color, 1.5);
                color = color / (1.0 + color);
                color = pow(color, 1.0 / 1.5);

                
                color = lerp(color, color * color * (3.0 - 2.0 * color), 1.0);
                color = pow(color, float3(1.3, 1.20, 1.0));    

                color = saturate(color * 1.01);
                
                color = pow(color, 0.7 / 2.2);

                return float4(color, 1.0);
                // return 1;
            }
            ENDHLSL
        }
    }
}
