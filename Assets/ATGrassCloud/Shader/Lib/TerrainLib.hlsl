#ifndef TERRAINLIB_HLSL
#define TERRAINLIB_HLSL

#define PATCH_MESH_SIZE 8
#define PATCH_MESH_GRID_COUNT 16

#define PATCH_COUNT_PER_NODE 8

#define PATCH_MESH_GRID_SIZE 0.5
 
#define SECTOR_COUNT_WORLD 160

struct TileDescriptor{
    uint branch;
};

struct RenderPatch{
    float2 position;
    float2 minMaxHeight;
    uint lod;
    uint4 lodTrans;
};


struct Bounds{
    float3 minPosition;
    float3 maxPosition;
};

struct BoundsDebug{
    Bounds bounds;
    float4 color;
};


// World LOD Params
#define MAX_TERRAIN_LOD 7
// x for tile size of this LOD in world scale, 
// y for patch extent, 
// z for tile count per row in this LOD, 
// w for sector count per tile in this LOD
float4 WorldLodParams[MAX_TERRAIN_LOD];
// The Tile ID Level Offset of Each LOD
// Offset of LOD TOP is 0, LOD TOP - 1  is TileCountPerRow * TileCountPerRow of LOD TOP
// LOD TOP - 2 is TileCountPerRow * TileCountPerRow of LOD TOP - 1 + Tile Offset of LOD TOP - 1, etc.
// Note: It seems there is a bug in Unity, So I have to use float[] to pass the offset( which should be uint[] )
float TileIDOffsetByLOD[MAX_TERRAIN_LOD];

float GetTileSize(uint lod){
    return WorldLodParams[lod].x;
}

uint GetTileCount(uint lod){
    return (uint)WorldLodParams[lod].z;
}

float GetPatchExtent(uint lod){
    return WorldLodParams[lod].y;
}

uint GetSectorCountPerTile(uint lod){
    return (uint)WorldLodParams[lod].w;
}


uint GetTileId( uint3 tileLoc )
{
    return (uint)TileIDOffsetByLOD[tileLoc.z] + tileLoc.y * GetTileCount(tileLoc.z) + tileLoc.x;
}

uint GetTileId(uint2 tileLoc,uint lod){
    return GetTileId(uint3(tileLoc,lod));
}

uniform float3 _TerrainCameraPositionWS;
uniform float3 _TerrainOffsetWS;
uniform float4 _TerrainCameraFrustumPlanes[6];
uniform float3 _TerrainWorldSize;
uniform int _TerrainLODLevel;



#endif