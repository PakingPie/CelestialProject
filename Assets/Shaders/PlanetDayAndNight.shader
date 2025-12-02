Shader "Custom/PlanetDayAndNight"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SunPosition ("Sun Position", Vector) = (0, 1, 0, 0)
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
            #include "Assets/Shaders/Includes/FBM.hlsl"

            sampler2D _MainTex;
            float3 _SunPosition;

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
                float3 positionOS : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 textureSeamless(sampler2D s, float2 uv) {
                float2 dx_normal = ddx(uv);
                float2 dx_wrapped = ddx(frac(uv + 0.5));
                float2 dy_normal = ddy(uv);
                float2 dy_wrapped = ddy(frac(uv + 0.5));
                
                float2 dx_use = float2(
                abs(dx_normal.x) < abs(dx_wrapped.x) ? dx_normal.x : dx_wrapped.x,
                abs(dx_normal.y) < abs(dx_wrapped.y) ? dx_normal.y : dx_wrapped.y
                );
                float2 dy_use = float2(
                abs(dy_normal.x) < abs(dy_wrapped.x) ? dy_normal.x : dy_wrapped.x,
                abs(dy_normal.y) < abs(dy_wrapped.y) ? dy_normal.y : dy_wrapped.y
                );
                
                return tex2Dgrad(s, uv * 2.0, dx_use, dy_use);
            }

            float speckle(float2 p, float densityVal) {
                float result = 0.0;
                for (float y = -1.0; y <= 1.0; y++) {
                    for (float x = -1.0; x <= 1.0; x++) {
                        float2 fp = floor(p);
                        float2 offset = float2(x, y);
                        float2 q = fp + offset + hash22(fp + offset);

                        // Add a threshold to skip most lights
                        float threshold = hash12(fp + offset + 0.5);
                        // if (threshold > 0.1) continue;  // Only 10% of cells have lights

                        float a = 1.5 * rexp(q) * pow(1.5 * clamp(densityVal, 0.0, 0.67), 1.5);
                        result += a * exp(-2.0 * distance(p, q) / clamp(densityVal, 0.67, 1.0));
                    }
                }
                return result;
            }

            float3 map(float3 p) {
                float lat = 90.0 - acos(p.y / length(p)) * 180.0 / PI;
                float lon = atan2(p.x, p.z) * 180.0 / PI;
                float2 uv = float2(lon / 360.0, lat / 180.0) + 0.5;
                
                float3 c;
                c.xy = textureSeamless(_MainTex, uv).xy;
                c.x = max(c.x, 0.0);
                c.z = speckle(1000.0 * uv, c.y);
                c.z *= 0.5 * FBM(float3(150.0 * uv, 1.0)); // _Time.y));

                // Add density threshold - only show lights in high density areas
                // c.z *= smoothstep(0.5, 1.0, c.y);  // Lights only where density > 0.3
                return c;
            }

            float3 calcNormal(float3 p) {
                float2 e = float2(1, 0) / 1e3;
                float3 offset = 0.04 * float3(
                map(p + e.xyy).x - map(p - e.xyy).x,
                map(p + e.yxy).x - map(p - e.yxy).x,
                map(p + e.yyx).x - map(p - e.yyx).x
                ) / (2.0 * length(e));
                return normalize(p + offset);
            }

            float4 frag (Varyings i) : SV_Target
            {
                float3 p = normalize(i.positionOS);
                float3 sunPos = _SunPosition.xyz;
                // Use Unity's main directional light
                float3 lightDir = normalize(sunPos);
                // Transform light direction to local space
                float3 localLightDir = normalize(mul(unity_WorldToObject, float4(lightDir, 0.0)).xyz);
                
                // Camera direction in local space
                float3 camWorldPos = _WorldSpaceCameraPos;
                float3 camDir = normalize(camWorldPos - i.positionWS);
                float3 localCamDir = normalize(mul(unity_WorldToObject, float4(camDir, 0.0)).xyz);
                
                float3 c = map(p);
                float height = c.x;
                float densityVal = c.y;
                float cities = c.z;
                
                float3 n = calcNormal(p);
                float light = dot(n, localLightDir);
                
                float specular = 0;//pow(clamp(dot(normalize(localLightDir - localCamDir), p), 0.0, 1.0), 64.0);
                
                float3 day = float3(0.06, 0.05, 0.2);
                float3 night = float3(0.00, 0.00, 0.00); // float3(0.00, 0.00, 0.01);
                
                if (height > 0.0) {
                    day = lerp(float3(0.16, 0.23, 0.09), float3(0.56, 0.49, 0.28), smoothstep(0.0, 0.5, height));
                    night = float3(0.00, 0.00, 0.00); // float3(0.00, 0.00, 0.02);
                    night *= 1.0 + 1.5 * clamp(FBM(50.0 * p) - 0.15, 0.0, 1.0);
                    night = lerp(night, 0.75 * sqrt(densityVal) * float3(0.95, 0.76, 0.47),
                    smoothstep(0.5, 1.0, cities) * dot(normalize(localCamDir), p));
                    night = clamp(night, 0.0, 1.0);
                    } else {
                    day += 0.25 * specular * float3(0.87, 0.75, 0.0);
                }
                
                float3 col = lerp(night, day * max(light, 0.0), smoothstep(-0.1, 0.1, light));
                
                col = pow(col, 1.0 / 2.2);
                
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
