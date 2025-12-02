Shader "Custom/PlanetNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Frame ("Frame", Int) = 0
        _Resolution ("Resolution", Vector) = (512, 512, 0, 0)
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
            #include "Assets/Shaders/Includes/FBM.hlsl"

            sampler2D _MainTex;
            int _Frame;
            float4 _Resolution;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float line_segment(float2 a, float2 b, float2 p, float width) {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
                float d = length(pa - ba * h);
                float x = distance(p, a) / (distance(p, a) + distance(p, b));
                return 1.5 * lerp(rexp(a), rexp(b), x) * smoothstep(width / 2.0, 0.0, d) * smoothstep(1.75, 0.5, distance(a, b));
            }

            float network(float2 p, float width) {
                float2 fp = floor(p);
                float2 c = fp + hash22(fp);
                float2 n = fp + N + hash22(fp + N);
                float2 e = fp + E + hash22(fp + E);
                float2 s = fp + S + hash22(fp + S);
                float2 w = fp + W + hash22(fp + W);
                
                float result = 0.0;
                result += line_segment(n, e, p, width);
                result += line_segment(e, s, p, width);
                result += line_segment(s, w, p, width);
                result += line_segment(w, n, p, width);
                
                for (float y = -1.0; y <= 1.0; y++) {
                    for (float x = -1.0; x <= 1.0; x++) {
                        float2 offset = float2(x, y);
                        float2 q = fp + offset + hash22(fp + offset);
                        float intensity = distance(p, q) / clamp(rexp(fp + offset), 0.0, 1.0);
                        result += line_segment(c, q, p, width);
                        result += 10.0 * exp(-40.0 * intensity);
                    }
                }
                
                return result;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 fragCoord = i.uv * _Resolution.xy;
                float2 iResolution = _Resolution.xy;
                
                if (_Frame > 10) {
                    float4 existing = tex2Dlod(_MainTex, float4(i.uv, 0, 0));
                    if (existing.z == iResolution.x && existing.w == iResolution.y) {
                        return existing;
                    }
                }
                
                float lat = 180.0 * fragCoord.y / iResolution.y - 90.0;
                float lon = 360.0 * fragCoord.x / iResolution.x;
                float3 p = fromlatlon(lat, lon);
                
                float2 uv = fragCoord / iResolution.y + 1.0;
                float2 wiggle = float2(FBM(float3(50.0 * uv, 1.0)), FBM(float3(50.0 * uv, 2.0))) - 0.5;
                
                float height = FBM(3.0 * p) - 0.6;
                
                float4 fragColor;
                fragColor.x = height;
                
                if (height < 0.0) {
                    fragColor.y = 0.0;
                    } else {
                    float d = 0.75;
                    float width = 3e-3;
                    d += 0.5 * network(100.0 * uv + 1.0 * wiggle, 100.0 * width);
                    d += 1.0 * network(30.0 * uv + 0.3 * wiggle, 30.0 * width);
                    d += 2.0 * network(10.0 * uv + 0.1 * wiggle, 10.0 * width);
                    d += smoothstep(0.1, 0.0, height);
                    d *= 0.1 + clamp(2.0 * FBM(12.0 * p) - 0.5, 0.0, 1.0);
                    d *= 0.2 + 1.3 * clamp(2.0 * FBM(1.5 * p) - 0.67, 0.0, 1.0);
                    fragColor.y = d;
                }
                
                fragColor.zw = iResolution.xy;
                
                return fragColor;
            }
            ENDHLSL
        }
    }
}
