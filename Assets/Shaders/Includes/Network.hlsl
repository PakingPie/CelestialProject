#ifndef CUSTOM_NETWORK_HLSL
#define CUSTOM_NETWORK_HLSL

#include "FBM.hlsl"

float CustomLine(float2 a, float2 b, float2 p, float width)
{
    // https://iquilezles.org/articles/distfunctions
    float2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0., 1.);
    float d = length(pa - ba * h);
    float x = distance(p, a) / (distance(p, a) + distance(p, b));
    return 1.5 * lerp(rexp(a), rexp(b), x) * smoothstep(width / 2., 0., d) * smoothstep(1.75, 0.5, distance(a, b));
}

float network(float2 p, float width)
{
    // based on https://www.shadertoy.com/view/lscczl
    float2 c = floor(p) + hash22(floor(p));
    float2 n = floor(p) + N + hash22(floor(p) + N);
    float2 e = floor(p) + E + hash22(floor(p) + E);
    float2 s = floor(p) + S + hash22(floor(p) + S);
    float2 w = floor(p) + W + hash22(floor(p) + W);

    float m = 0.0;
    m += CustomLine(n, e, p, width);
    m += CustomLine(e, s, p, width);
    m += CustomLine(s, w, p, width);
    m += CustomLine(w, n, p, width);

    for (float y = -1.; y <= 1.; y++)
    {
        for (float x = -1.; x <= 1.; x++)
        {
            float2 q = floor(p) + float2(x, y) + hash22(floor(p) + float2(x, y));
            float intensity = distance(p, q) / clamp(rexp(floor(p) + float2(x, y)), 0., 1.);
            m += CustomLine(c, q, p, width);
            m += 10. * exp(-40. * intensity);
        }
    }

    return m;
}

float2 SampleNetwork(float2 texcoord)
{
    float screenWidth = _ScreenParams.x;
    float screenHeight = _ScreenParams.y;
    float lat = 180. * texcoord.y / screenHeight - 90.;
    float lon = 360. * texcoord.x / screenWidth;
    float3 p = fromlatlon(lat, lon);

    float2 uv = texcoord / screenHeight + 1.;
    float2 wiggle = float2(FBM(float3(50. * uv, 1)), FBM(float3(50. * uv, 2))) - 0.5;

    float height = FBM(3. * p) - 0.5;
    float2 color = 0;
    color.x = height;
    if (height < 0.)
    {
        color.y = 0.;
    }
    else
    {
        float d = 0.75;
        float width = 3e-3;
        d += 0.5 * network(100. * uv + 1.0 * wiggle, 100. * width);
        d += 1.0 * network(30. * uv + 0.3 * wiggle, 30. * width);
        d += 2.0 * network(10. * uv + 0.1 * wiggle, 10. * width);
        d += smoothstep(0.1, 0., height); // coast
        d *= 0.1 + clamp(2. * FBM(12. * p) - 0.5, 0., 1.);
        d *= 0.2 + 1.3 * clamp(2. * FBM(1.5 * p) - 0.67, 0., 1.);
        color.y = d;
    }
    return color;
}

// float4 textureSeamless(sampler2D s, float2 uv)
// {
//     // avoid mipmap artifacts due to uv discontinuities
//     float2 dx = min(abs(ddx(uv)), abs(dFdx(frac(uv + 0.5))));
//     float2 dy = min(abs(ddy(uv)), abs(dFdy(frac(uv + 0.5))));
//     return textureGrad(s, uv, dx, dy);
// }

float speckle(float2 p, float density)
{
    float m = 0.;
    for (float y = -1.; y <= 1.; y++)
    {
        for (float x = -1.; x <= 1.; x++)
        {
            float2 q = floor(p) + float2(x, y) + hash22(floor(p) + float2(x, y));
            // m += 1.5 * rexp(q) * exp(-2. * distance(p,q) / clamp(density, 0., 1.));
            float a = 1.5 * rexp(q) * pow(1.5 * clamp(density, 0., 0.67), 1.5);
            m += a * exp(-2.0 * distance(p, q) / clamp(density, 0.67, 1.));
        }
    }
    return m;
}

void map_float(float3 p, out float3 c)
{
    float lat = 90. - acos(p.y / length(p)) * 180. / PI;
    float lon = atan2(p.x, p.z) * 180. / PI;
    float2 uv = float2(lon / 360., lat / 180.) + 0.5;
    c.xy = SampleNetwork(uv).xy;
    c.x = max(c.x, 0.);
    c.z = speckle(1000. * uv, c.y);
    // c.z *= 0.5 * FBM(float3(50.0 * uv, _Time.y));
}

#endif