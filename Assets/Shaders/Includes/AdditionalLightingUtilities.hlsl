#ifndef CUSTOM_ADDITIONAL_LIGHTING_UTILITIES_HLSL
#define CUSTOM_ADDITIONAL_LIGHTING_UTILITIES_HLSL

// For CustomLit Default
void AdditionalLightsRadianceDefault_float(float MainDiffuse, float3 MainSpecular, float3 MainColor, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float SpecularPower, float2 ScreenPosition,
    out float Diffuse, out float3 Specular, out float3 Color) {
    Diffuse = MainDiffuse;
    Specular = MainSpecular;
    Color = MainColor * (MainDiffuse + MainSpecular);

#ifndef SHADERGRAPH_PREVIEW

    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition;
    inputData.positionWS = WorldPosition;
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
    // get light color and direction
#if !USE_CLUSTER_LIGHT_LOOP
    lightIndex = GetPerObjectLightIndex(lightIndex);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, WorldPosition);

    // calculate shadows
    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, WorldPosition, light.direction);
    float atten = light.distanceAttenuation * light.shadowAttenuation;

    // calculate diffuse and specular
    float NdotL = saturate(dot(WorldNormal, light.direction));
    float thisDiffuse = atten * NdotL;
    float3 thisSpecular = LightingSpecular(thisDiffuse, light.direction, WorldNormal, WorldView, 1, SpecularPower);

    // accumulate light
    Diffuse += thisDiffuse;
    Specular += thisSpecular;

#if defined(LIGHT_COOKIES)
    float3 cookieColor = SampleAdditionalLightCookie(lightIndex, WorldPosition);
    light.color *= cookieColor;
#endif

    Color += light.color * (thisDiffuse + thisSpecular);
    LIGHT_LOOP_END

    float total = Diffuse + dot(Specular, float3(0.333, 0.333, 0.333));
    Color = total <= 0 ? MainColor : Color / total;
#endif
}

// For CutomLitToonLit
void AdditionalLightsRadianceHalfLambert_float(float MainDiffuse, float3 MainSpecular, float3 MainColor, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float SpecularPower, float2 ScreenPosition,
    out float Diffuse, out float3 Specular, out float3 Color) {
    Diffuse = MainDiffuse;
    Specular = MainSpecular;
    Color = MainColor * (MainDiffuse + MainSpecular);

#ifndef SHADERGRAPH_PREVIEW

    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition;
    inputData.positionWS = WorldPosition;
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
    // get light color and direction
#if !USE_CLUSTER_LIGHT_LOOP
    lightIndex = GetPerObjectLightIndex(lightIndex);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, WorldPosition);

    // calculate shadows
    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, WorldPosition, light.direction);
    float atten = light.distanceAttenuation * light.shadowAttenuation;

    // calculate diffuse and specular
    float NdotL = saturate(dot(WorldNormal, light.direction) * 0.5 + 0.5);
    float thisDiffuse = atten * NdotL;
    float3 thisSpecular = LightingSpecular(thisDiffuse, light.direction, WorldNormal, WorldView, 1, SpecularPower);

    // accumulate light
    Diffuse += thisDiffuse;
    Specular += thisSpecular;

#if defined(LIGHT_COOKIES)
    float3 cookieColor = SampleAdditionalLightCookie(lightIndex, WorldPosition);
    light.color *= cookieColor;
#endif

    Color += light.color * (thisDiffuse + thisSpecular);
    LIGHT_LOOP_END

    float total = Diffuse + dot(Specular, float3(0.333, 0.333, 0.333));
    Color = total <= 0 ? MainColor : Color / total;
#endif
}

// For CustomLitSimple
void AdditionalLightsRadianceSimple_float(float MainDiffuse, float3 MainColor, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float2 ScreenPosition,
    out float Diffuse, out float3 Color) {
    Diffuse = MainDiffuse;
    Color = MainColor * MainDiffuse;

#ifndef SHADERGRAPH_PREVIEW
    uint pixelLightCount = GetAdditionalLightsCount();

#if USE_CLUSTER_LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = ScreenPosition;
    inputData.positionWS = WorldPosition;
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
    // get light color and direction
#if !USE_CLUSTER_LIGHT_LOOP //! USE_CLUSTER_LIGHT_LOOP
    lightIndex = GetPerObjectLightIndex(lightIndex);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, WorldPosition);
    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, WorldPosition, light.direction);
    float atten = light.distanceAttenuation * light.shadowAttenuation;
    // calculate diffuse
    float NdotL = saturate(dot(WorldNormal, light.direction));
    float thisDiffuse = atten * NdotL;
    // accumulate light
    Diffuse += thisDiffuse;

    Color += light.color * thisDiffuse;
    LIGHT_LOOP_END

    float total = Diffuse;
    Color = total <= 0 ? MainColor : Color / total;
#endif
}

// For CustomLitURP
void AdditionalLightsURP_float(float MainDiffuse, float3 MainSpecular, float3 MainColor, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Smoothness, float Reflectance, float2 ScreenPosition, 
out float Diffuse, out float3 Specular, out float3 Color)
{
    Diffuse = MainDiffuse;
    Specular = MainSpecular;
    Color = MainColor * (MainDiffuse + MainSpecular);

    #ifndef SHADERGRAPH_PREVIEW
        
        uint pixelLightCount = GetAdditionalLightsCount();
        half Roughness = pow(1 - Smoothness, 2);
        half Roughness2 = Roughness * Roughness;
        half Roughness2Minus1 = Roughness2 - 1;
        half normalizationTerm = (Roughness * half(4.0) + half(2.0));

        #if USE_CLUSTER_LIGHT_LOOP
            // for Foward+ LIGHT_LOOP_BEGIN macro uses inputData.normalizedScreenSpaceUV and inputData.positionWS
            InputData inputData = (InputData)0;

            inputData.normalizedScreenSpaceUV = ScreenPosition;
            inputData.positionWS = WorldPosition;
        #endif

        LIGHT_LOOP_BEGIN(pixelLightCount)
            // Convert the pixel light index to the light data index
            #if !USE_CLUSTER_LIGHT_LOOP
            lightIndex = GetPerObjectLightIndex(lightIndex);
            #endif
            // Call the URP additional light algorithm. This will not calculate shadows, since we don't pass a shadow mask value
            Light light = GetAdditionalPerObjectLight(lightIndex, WorldPosition);
            // Manually set the shadow attenuation by calculating realtime shadows
            light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, WorldPosition, light.direction);
            #if defined(_LIGHT_COOKIES)
                float3 cookieColor = SampleAdditionalLightCookie(lightIndex, WorldPosition);
                light.color *= cookieColor;
            #endif
            float NdotL = saturate(dot(WorldNormal, light.direction));
            float atten = light.distanceAttenuation * light.shadowAttenuation;
            float thisDiffuse = NdotL * atten;
            //DirectBRDFSpecular

            float3 lightDirectionWSFloat3 = float3(light.direction);
            float3 halfDir = SafeNormalize(lightDirectionWSFloat3 + float3(WorldView));
            float NoH = saturate(dot(float3(WorldNormal), halfDir));
            half LoH = half(saturate(dot(lightDirectionWSFloat3, halfDir)));
            float d = NoH * NoH * Roughness2Minus1 + 1.00001f;
            half LoH2 = LoH * LoH;
            half spec = Roughness2 / ((d * d) * max(0.1h, LoH2) * normalizationTerm);
            #if REAL_IS_HALF
                spec = spec - HALF_MIN;
                spec = clamp(spec, 0.0, 1000.0);
            #endif  
            float3 thisSpecular = spec * Reflectance * NdotL * atten;

            Diffuse += thisDiffuse;
            Specular += thisSpecular;

            Color += light.color * (thisDiffuse + thisSpecular);
        LIGHT_LOOP_END
        float total = Diffuse + dot(Specular, float3(0.333, 0.333, 0.333));
        Color = total <= 0 ? MainColor : Color / total;
    #endif
}
#endif