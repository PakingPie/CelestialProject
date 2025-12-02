#ifndef CUSTOM_FRACTAL_BROWNIAN_MOTION_HLSL
#define CUSTOM_FRACTAL_BROWNIAN_MOTION_HLSL

#define N float2(0, 1)
#define E float2(1, 0)
#define S float2(0, -1)
#define W float2(-1, 0)

#define HASHSCALE1 0.1031
#define HASHSCALE3 float3(0.1031, 0.1030, 0.0973)

#define rexp(p) (-log(1e-4 + (1.0 - 2e-4) * hash12(p)))

float3 fromlatlon(float lat, float lon)
{
    return float3(sin(lon * PI / 180.0) * cos(lat * PI / 180.0),
                  sin(lat * PI / 180.0),
                  cos(lon * PI / 180.0) * cos(lat * PI / 180.0));
}

float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * HASHSCALE1);
    p3 += dot(p3, p3.yzx + 19.19);
    return frac((p3.x + p3.y) * p3.z);
}

float hash13(float3 p3)
{
    p3 = frac(p3 * HASHSCALE1);
    p3 += dot(p3, p3.yzx + 19.19);
    return frac((p3.x + p3.y) * p3.z);
}

float2 hash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * HASHSCALE3);
    p3 += dot(p3, p3.yzx + 19.19);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float Noise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f *= f * (3.0 - 2.0 * f);

    return lerp(
        lerp(lerp(hash13(i + float3(0, 0, 0)), hash13(i + float3(1, 0, 0)), f.x),
             lerp(hash13(i + float3(0, 1, 0)), hash13(i + float3(1, 1, 0)), f.x),
             f.y),
        lerp(lerp(hash13(i + float3(0, 0, 1)), hash13(i + float3(1, 0, 1)), f.x),
             lerp(hash13(i + float3(0, 1, 1)), hash13(i + float3(1, 1, 1)), f.x),
             f.y),
        f.z);
}

static const float3x3 fbmMatrix = float3x3(
    0.00 * 1.7, 0.80 * 1.7, 0.60 * 1.7,
    -0.80 * 1.7, 0.36 * 1.7, -0.48 * 1.7,
    -0.60 * 1.7, -0.48 * 1.7, 0.64 * 1.7);

float FBM(float3 p)
{
    float f;
    f = 0.5000 * Noise(p);
    p = mul(fbmMatrix, p);
    f += 0.2500 * Noise(p);
    p = mul(fbmMatrix, p);
    f += 0.1250 * Noise(p);
    p = mul(fbmMatrix, p);
    f += 0.0625 * Noise(p);
    p = mul(fbmMatrix, p);
    f += 0.03125 * Noise(p);
    p = mul(fbmMatrix, p);
    f += 0.015625 * Noise(p);
    return f;
}

#endif