#ifndef CUSTOM_STARFIELD_TUTORIAL_INCLUDE_HLSL
#define CUSTOM_STARFIELD_TUTORIAL_INCLUDE_HLSL

float2x2 Rot(float a) {
    float s=sin(a), c=cos(a);
    return float2x2(c, -s, s, c);
}

float Star(float2 uv, float flare) {
    float d = length(uv);
    float m = .05/d;

    float rays = max(0., 1.-abs(uv.x*uv.y*1000.));
    m += rays*flare;
    uv = mul(Rot(3.1415/4.0), uv);
    rays = max(0., 1.-abs(uv.x*uv.y*1000.));
    m += rays*.3*flare;

    m *= smoothstep(1., .2, d);
    return m;
}

float Hash21(float2 p) {
    p = frac(p*float2(123.34, 456.21));
    p += dot(p, p+45.32);
    return frac(p.x*p.y);
}

float3 StarLayer(float2 uv) {
    float3 col = 0;

    float2 gv = frac(uv)-.5;
    float2 id = floor(uv);

    for(int y=-1;y<=1;y++) {
        for(int x=-1;x<=1;x++) {
            float2 offs = float2(x, y);

            float n = Hash21(id+offs); // random between 0 and 1
            float size = frac(n*345.32);

            float star = Star(gv-offs-float2(n, frac(n*34.))+.5, smoothstep(.9, 1., size)*.6);

            float3 color = sin(float3(.2, .3, .9)*frac(n*2345.2)*123.2)*.5+.5;
            color = color*float3(1,.25,1.+size)+float3(.2, .2, .1)*2.;

            star *= sin(_Time.y * 0.003 + n * 6.2831) * 0.5+1.;
            col += star*size*color;
        }
    }
    return col;
}

void StarField_float(float2 uv, float numOfLayers, out float3 col) {
    float t = _Time.y * 0.0002;
    uv = mul(Rot(t), uv);
    col = 0;

    for(float i=0.0; i < 1.0; i += 1.0/numOfLayers) {
        float depth = frac(i+t);

        float scale = lerp(20., .5, depth);
        float fade = depth*smoothstep(1., .9, depth);
        col += StarLayer(uv * scale + i * 453.2) * fade;
    }

    col = pow(col, float3(.4545, .4545, .4545)); // gamma correction
}

#endif