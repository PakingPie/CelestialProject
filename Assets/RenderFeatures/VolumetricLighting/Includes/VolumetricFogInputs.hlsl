#ifndef VOLUMETRIC_FOG_INPUT_INCLUDED
#define VOLUMETRIC_FOG_INPUT_INCLUDED

#define KERNEL_RADIUS 4
#define BLUR_DEPTH_FALLOFF 0.5

TEXTURE2D_X(_VolumetricFogTexture);
SAMPLER(sampler_BlitTexture);

TEXTURE2D_X_FLOAT(_DownsampledCameraDepthTexture);
float4 _DownsampledCameraDepthTexture_TexelSize;

#endif