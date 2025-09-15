
#ifndef GRASS_MESH_GEOMETRY_LIB_HLSL
#define GRASS_MESH_GEOMETRY_LIB_HLSL

int GetTextureLOD( float3 worldPos , float3 viewPos, float texMipDistance )
{
    float distance = sqrt(length(viewPos - worldPos));
    float lod = floor( distance / texMipDistance );

    return lod;

}





#endif 
