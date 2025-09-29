Shader "Custom/GargantuaFirstBloom"
{
    Properties
    {
        _GargantuaTex("Gargantua Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

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
                // float2 uv = IN.texcoord;
                
                // float3 color = 0.0;

                // /*
                // Create a mipmap tree thingy with padding to prevent leaking bloom
                
                // Since there's no mipmaps for the previous buffer and the reduction process has to be done in one pass,
                // oversampling is required for a proper result
                // */
                // color += Grab1(uv, 1.0, 0.0);
                // color += Grab4(uv, 2.0, CalcOffset(1.0));
                // color += Grab8(uv, 3.0, CalcOffset(2.0));
                // color += Grab16(uv, 4.0, CalcOffset(3.0));
                // color += Grab16(uv, 5.0, CalcOffset(4.0));
                // color += Grab16(uv, 6.0, CalcOffset(5.0));
                // color += Grab16(uv, 7.0, CalcOffset(6.0));
                // color += Grab16(uv, 8.0, CalcOffset(7.0));

                // return float4(saturate(color), 1);
                return SAMPLE_TEXTURE2D_X(_GargantuaTex, sampler_GargantuaTex, IN.texcoord);
            }
            ENDHLSL
        }
    }
}
