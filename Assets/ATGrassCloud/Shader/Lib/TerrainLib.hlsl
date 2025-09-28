#ifndef TERRAINLIB_HLSL
#define TERRAINLIB_HLSL


#define PATCH_MESH_GRID_SIZE 0.5
 
#define PATCH_COUNT_PER_TILE_IN_ROW 8

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
const int MAX_TERRAIN_LOD_LEVEL = 7;
// x for tile size of this LOD in world scale, 
// y for patch extent, 
// z for tile count in row in world scale in this LOD, 
// w for the total number of sectors(tile in LOD0, which has min size) 
// per tile in row in this LOD ( e.g. 1 for LOD 0 , 2 for LOD 1, 4 for LOD 2 , etc. )
float4 WorldLodParams[MAX_TERRAIN_LOD];
// The Tile ID Level Offset of Each LOD
// Offset of LOD TOP is 0, LOD TOP - 1  is TileCountPerRow * TileCountPerRow of LOD TOP
// LOD TOP - 2 is TileCountPerRow * TileCountPerRow of LOD TOP - 1 + Tile Offset of LOD TOP - 1, etc.
// Note: It seems there is a bug in Unity, So I have to use float[] to pass the offset( which should be uint[] )
// float TileIDOffsetByLOD[MAX_TERRAIN_LOD];
// int TileIDOffsetByLOD[MAX_TERRAIN_LOD];
int TileIDOffsetByLOD0;
int TileIDOffsetByLOD1;
int TileIDOffsetByLOD2;
int TileIDOffsetByLOD3;
int TileIDOffsetByLOD4;
int TileIDOffsetByLOD5;
int TileIDOffsetByLOD6;


uniform float3 _TerrainCameraPositionWS;
uniform float3 _TerrainOffsetWS;
uniform float4 _TerrainCameraFrustumPlanes[6];
uniform float3 _TerrainWorldSize;
uniform int _TerrainLODLevel;


texture2D<float4> _HeightMapTexture;
texture2D<float4> _MinMaxHeightMapTexture;

float GetTileSize(uint lod){
    return WorldLodParams[lod].x;
}

uint GetTileCount(uint lod){
    return (uint)WorldLodParams[lod].z;
}

float GetPatchExtent(uint lod){
    return WorldLodParams[lod].y;
}

uint GetSectorCountPerTilePerRow(uint lod){
    return (uint)WorldLodParams[lod].w;
}

int GetTileOffsetByLOD(uint lod){
    switch(lod){ 
        case 0: return TileIDOffsetByLOD0;
        case 1: return TileIDOffsetByLOD1;
        case 2: return TileIDOffsetByLOD2;
        case 3: return TileIDOffsetByLOD3;
        case 4: return TileIDOffsetByLOD4;
        case 5: return TileIDOffsetByLOD5;
        case 6: return TileIDOffsetByLOD6;
    }
    return 0;
}

uint GetTileId( uint3 tileLoc )
{
    return (uint)GetTileOffsetByLOD(tileLoc.z) + tileLoc.y * GetTileCount(tileLoc.z) + tileLoc.x;
}

uint GetTileId(uint2 tileLoc,uint lod){
    return GetTileId(uint3(tileLoc,lod));
}

float2 GetTilePositionWS2(uint2 tileLoc,uint mip){
    float tileMeterSize = GetTileSize(mip);
    float tileCount = GetTileCount(mip);
    float2 tilePositionWS = ((float2)tileLoc - (tileCount-1)*0.5) * tileMeterSize;
    return tilePositionWS;
}

float3 GetTilePositionWS(uint2 tileLoc,uint lod){
    float2 tilePositionWS = GetTilePositionWS2(tileLoc,lod);
    tilePositionWS += _TerrainOffsetWS.xz;

    // float2 minMaxHeight = _MinMaxHeightMapTexture.mips[lod + 3][tileLoc].xy;
    // float y = (minMaxHeight.x + minMaxHeight.y) * 0.5 * _WorldSize.y;
    float y = 0;
    return float3(tilePositionWS.x,y,tilePositionWS.y);
}



#endif