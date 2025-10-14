#ifndef __TERRAIN_MATERIAL_LIB__
#define __TERRAIN_MATERIAL_LIB__


// Texture & Sampler declarations
TEXTURE2D(_SplatMap1);
TEXTURE2D(_SplatMap2);
SAMPLER(sampler_SplatMap1);

TEXTURE2D_ARRAY(_LayerAlbedoMap);
TEXTURE2D_ARRAY(_LayerMetallicSmoothnessMap);
TEXTURE2D_ARRAY(_LayerNormalMap);
TEXTURE2D(_HeightMap);

SAMPLER(sampler_LayerAlbedoMap);
SAMPLER(sampler_LayerMetallicSmoothnessMap);
SAMPLER(sampler_LayerNormalMap);
SAMPLER(sampler_HeightMap);

// Terrain material layer definition (8 layers)
struct TerrainMaterial
{
    float3 albedo;
    float  metallic;
    float  smoothness;
    float3 normalTS; // Optional normal map
    float  height;  // Height map sample value
};

// Input: UV and weight index
// Output: Properties of a single material layer
TerrainMaterial SAMPLE_TERRAIN_LAYER(
    TEXTURE2D_ARRAY(_LayerAlbedoMap),
    TEXTURE2D_ARRAY(_LayerMetallicSmoothnessMap),
    TEXTURE2D_ARRAY(_LayerNormalMap),
    TEXTURE2D(_HeightMap),
    SamplerState sampler_LayerAlbedoMap,
    SamplerState sampler_LayerMetallicSmoothnessMap,
    SamplerState sampler_LayerNormalMap,
    SamplerState sampler_HeightMap,
    float2 uv,
    uint layerIndex
)
{
    TerrainMaterial mat;
    
    // Albedo (RGB)
    mat.albedo = SAMPLE_TEXTURE2D_ARRAY(_LayerAlbedoMap, sampler_LayerAlbedoMap, uv, layerIndex).rgb;

    // Metallic & Smoothness: Assume R=Metallic, A=Smoothness
    float4 ms = SAMPLE_TEXTURE2D_ARRAY(_LayerMetallicSmoothnessMap, sampler_LayerMetallicSmoothnessMap, uv, layerIndex);
    mat.metallic = ms.r;
    mat.smoothness = ms.a;

    // Normal (Optional)
    mat.normalTS = SAMPLE_TEXTURE2D_ARRAY(_LayerNormalMap, sampler_LayerNormalMap, uv, layerIndex).rgb * 2.0 - 1.0;

    // Height
    mat.height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uv).r;

    return mat;
}





#endif // __TERRAIN_MATERIAL_LIB__