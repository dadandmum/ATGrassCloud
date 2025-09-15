#ifndef __GRASS_LIB_HLSL__
#define __GRASS_LIB_HLSL__
 
struct GrassData
{
    float3 position;
    float windOffset;
    uint rand0;
};

uint grass_murmurHash3F(float input) {
    uint h = abs(input);
    h ^= h >> 16;
    h *= 0x85ebca6b;
    h ^= h >> 13;
    h *= 0xc2b2ae3d;
    h ^= h >> 16;
    return h;
}


float grass_random(float input) {
    return grass_murmurHash3F(input) / 4294967295.0;
}

float grass_srandom(float input) {
    return (grass_murmurHash3F(input) / 4294967295.0) * 2 - 1;
}

float grass_Remap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

#include "GrassGeometryLib.hlsl"
#include "GrassShadingLib.hlsl"
#include "GrassMeshGeometryLib.hlsl"



#endif
