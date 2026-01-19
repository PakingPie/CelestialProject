#ifndef CUSTOM_ADDITIONAL_LIGHTING_UTILITIES_HLSL
#define CUSTOM_ADDITIONAL_LIGHTING_UTILITIES_HLSL
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
    //get light color and direction
#if !USE_CLUSTER_LIGHT_LOOP //!USE_CLUSTER_LIGHT_LOOP
    lightIndex = GetPerObjectLightIndex(lightIndex);
#endif
    Light light = GetAdditionalPerObjectLight(lightIndex, WorldPosition);
    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, WorldPosition, light.direction);
    float atten = light.distanceAttenuation * light.shadowAttenuation;
    //calculate diffuse
    float NdotL = saturate(dot(WorldNormal, light.direction));
    float thisDiffuse = atten * NdotL;
    //accumulate light
    Diffuse += thisDiffuse;

    Color += light.color * thisDiffuse;
    LIGHT_LOOP_END

    float total = Diffuse;
    Color = total <= 0 ? MainColor : Color / total;
#endif
}
#endif