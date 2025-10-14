#ifndef __TERRAIN_SHADING_LIB__
#define __TERRAIN_SHADING_LIB__

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/ATGI.hlsl"

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) 
// BRDF reference :
// https://schuttejoe.github.io/post/disneybsdf/

// Geometry function: Smith's method with GGX
half GeometrySchlickGGX(half roughness, half NdotV) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    return nom / denom;
}

float GeometrySmith( float NdotV , float NdotL , float roughness)
{
    float ggx2 = GeometrySchlickGGX(roughness, NdotV);
    float ggx1 = GeometrySchlickGGX(roughness, NdotL);

    return ggx1 * ggx2 ;
}

    // version in https://learnopengl.com/PBR/Theory
float DistributionGGX( float NdotH , float roughness )
{
    float alpha = max( roughness * roughness, 0.0001f); 
    float alpha2 = max( alpha * alpha, 0.0001f);
    float NdotH_abs = max(NdotH,0.00001f);
    float NdotH2 = NdotH_abs * NdotH_abs;
    
    float nom = alpha2;
    float denom = (NdotH2 * (alpha2 - 1.0f) + 1.000001f);
    denom = 3.14159265359 * denom * denom;

    return nom / denom ;
}


// version in Unity URP and disney
// https://schuttejoe.github.io/post/disneybsdf/
float SpecularUnity( float NdotH , float HdotL, float roughness )
{
    float alpha = max( roughness * roughness, 0.0001f); 
    float alpha2 = max( alpha * alpha, 0.0001f);
    float NdotH_abs = max(NdotH,0.00001f);
    float NdotH2 = NdotH_abs * NdotH_abs;
    float HdotL_abs = max(HdotL,0.00001f);
    float HdotL2 = HdotL_abs * HdotL_abs;

    float nom = alpha2;
    float d = (NdotH2 * (alpha2 - 1.0f) + 1.000001f);
    float normalizeTerm = roughness * 4.0f - 2.0f ;
    float denom = d * d * max (0.1f , HdotL2) * normalizeTerm;

    return HdotL2;

    return nom / denom ;
}



float3 FresnelSchlick(float VdotH, float3 F0)
{
    return F0 + (1.0f - F0) * pow( 1.0f - max( VdotH , 0.001f) , 5.0f);
}


/// <summary>
/// Physically Based Rendering (PBR) shading function for grass blades or similar vegetation.
/// Uses Cook-Torrance BRDF with GGX normal distribution and Schlick approximations.
/// Includes artistic enhancement for tip specular (e.g., dew or backlit effect).
/// </summary>
float3 Shader_DirectLightPBR(
    float3 lightDir,
    float3 lightColor,
    float3 N,
    float3 V,
    float3 albedo,
    float metallic,
    float smoothness
)
{
    float3 L = lightDir;
    float3 H = normalize(L + V);

    // Dot products
    float NdotL = saturate(dot(N, L));
    float NdotV = saturate(dot(N, V));
    float NdotH = saturate(dot(N, H));
    float VdotH = saturate(dot(V, H));
    float LdotH = saturate(dot(L, H));

    // Avoid division by zero in denominator 
    NdotL = max(NdotL, 1e-10);
    NdotV = max(NdotV, 1e-10);

    // =====================
    // 1. Material Properties
    // =====================

    // Base reflectivity (F0): 4% for dielectrics, albedo for metals
    float3 F0 = lerp(kDielectricSpec.rgb, albedo, metallic);

    // Convert smoothness to roughness
    float roughness = 1.0f - smoothness;
    roughness = max(roughness, 0.001f); // Prevent numerical instability
    float alpha = max( roughness * roughness, 0.0001f); // α² for GGX distribution

    // =====================
    // 2. BRDF Components
    // =====================

    // Combined geometric shadowing
    float G = GeometrySmith(NdotV, NdotL, roughness);
    // Normal distribution function: GGX (Trowbridge-Reitz)
    float d = NdotH * NdotH * ( alpha * alpha - 1.0f ) + 1.000001f;
    // float D = alpha * alpha / (3.1415926 * pow((NdotH * NdotH * (alpha*alpha - 1.0f) + 1.000001f), 2.0f));
    // float D = alpha * alpha / (d * d * max(0.1h, LdotH ));
    // float D = DistributionUnity(NdotH, LdotH, roughness);
    float D = DistributionGGX(NdotH, roughness);


    // Fresnel: Schlick approximation
    float3 F = FresnelSchlick(VdotH, F0);

    // =====================
    // 3. Cook-Torrance Specular BRDF
    // =====================

    float3 numerator = D * G * F;
    float denominator = 4.0f * NdotL * NdotV;
    float3 specular = numerator / denominator;

    // =====================
    // 4. Diffuse Reflection (energy conservation)
    // =====================
 
    // Diffuse contribution: only non-metallic parts reflect diffuse
    float3 kD = (1.0f - F) * (1.0f - metallic);
    float3 diffuse = kD * albedo / 3.1415926;

    // =====================
    // 5. Light Attenuation
    // =====================

    float3 lighting = lightColor;

    // =====================
    // 6. Final Color
    // =====================

    float3 color = (diffuse + specular ) * NdotL * lighting;

    return color;
}


float3 terrain_AmbientDiffuse(
    float3 albedo,
    float3 N
)
{
    return (ATGI_SampleSH0(N) + 0.01) * albedo;
}


float3 terrain_Shading(
    float3 albedo,
    float smoothness,
    float metallic,
    float3 positionWS,
    float3 normalWS,
    float3 viewWS)
{
    float3 result = float3(0,0,0);
    float3 ambient = terrain_AmbientDiffuse(albedo, normalWS);
    result += ambient;

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    float3 lightDir = mainLight.direction;
    float3 lightColor = mainLight.color * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    result += Shader_DirectLightPBR(lightDir, lightColor, normalWS, viewWS, albedo, metallic, smoothness);
    
    int additionalLightsCount = GetAdditionalLightsCount();
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        Light light = GetAdditionalLight(i, positionWS);
        lightDir = light.direction;
        lightColor = light.color * light.shadowAttenuation * light.distanceAttenuation;
        result += Shader_DirectLightPBR(lightDir, lightColor, normalWS, viewWS, albedo, metallic, smoothness);
    }
    
    return result;
}
 
#endif
