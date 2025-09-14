#ifndef __GRASS_SHADING_LIB_HLSL__
#define __GRASS_SHADING_LIB_HLSL__

         
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// #define _SHADER_TIP_SPECULAR 1
// #define _SHADER_PBR 1

// blend the grass normal according to the positionModel.x
// suppose the model is flatten on xy plane ( z == 0 !)
// and y is height , x is width
float3 grass_ModelNormal(
    float3 positionModel,
    float normalBlend
)
{
    float3 forward = float3( 0,0,1.0f);
    float3 blendOffset = float3(1.0,0,0) * normalBlend * positionModel.x * 50.0 ;
    return normalize(forward + blendOffset);
}

float3 grass_GetNormal(
    float3 positionModel,
    float3 up , 
    float3 forward ,
    float3 modelNormal,
    float droop) {

    float3 right = -cross(up, forward);
    
    // 标准变换：将模型法线变换到世界/局部空间
    float3 N = modelNormal.x * right +
               modelNormal.y * up +
               modelNormal.z * forward;
    N = normalize(N);

    // clamp the stretch to avoid negative( ignore the grass drop too down)
    // float verticalStretch = max(1e-10, 1.0 - 2.0 * droop * positionModel.y);
    float verticalStretch =  1.0 - 2.0 * droop * positionModel.y;

    if ( abs(verticalStretch) > 1e-10)
    {
        N.y /= verticalStretch;
        N = normalize(N);
    }else{
        N = float3(0,1,0);
    }

    return N;
}


float3 grass_Albedo( float3 colorA , float3 colorB , float rand , float3 colorAO , float3 positionOS, float AOFactor)
{
    float3 baseColor = lerp(colorA, colorB, rand);
    float3 albedo = lerp(colorAO, baseColor, pow( positionOS.y * positionOS.y , AOFactor) );
    return albedo;
}


float3 grass_AmbientDiffuse(
    float3 albedo
)
{
    return (SampleSH(0) + 0.1) * albedo;
}


half3 ShadeGrassBlade_TipSpecular(Light light, half3 N, half3 V, half3 albedo, half specularMask, half positionY)
{
    half3 H = normalize(light.direction + V);

    half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;

    float directSpecular = saturate(dot(N,H));
    directSpecular *= directSpecular;
    directSpecular *= directSpecular;
    directSpecular *= directSpecular;
    directSpecular *= directSpecular;

    directSpecular *= positionY * 0.12;

    half3 lighting = light.color * light.shadowAttenuation * light.distanceAttenuation;
    half3 result = (albedo * directDiffuse + directSpecular * (1-specularMask)) * lighting;


    return result; 
}


// Geometry function: Smith's method with GGX
half grass_GeometrySchlickGGX(half k, half dotNV) {
    return dotNV / (dotNV * (1.0h - k) + k);
}


/// <summary>
/// Physically Based Rendering (PBR) shading function for grass blades or similar vegetation.
/// Uses Cook-Torrance BRDF with GGX normal distribution and Schlick approximations.
/// Includes artistic enhancement for tip specular (e.g., dew or backlit effect).
/// </summary>
half3 ShadeGrassBlade_PBR(
    Light light,
    half3 N,
    half3 V,
    half3 albedo,
    half metallic,
    half smoothness,
    half positionY
)
{
    half3 L = light.direction;
    half3 H = normalize(L + V);

    // Dot products
    half NdotL = saturate(dot(N, L));
    half NdotV = saturate(dot(N, V));
    half NdotH = saturate(dot(N, H));
    half VdotH = saturate(dot(V, H));

    // Avoid division by zero in denominator 
    NdotL = max(NdotL, 1e-10);
    NdotV = max(NdotV, 1e-10);

    // =====================
    // 1. Material Properties
    // =====================

    // Base reflectivity (F0): 4% for dielectrics, albedo for metals
    half3 F0 = lerp(half3(0.04h, 0.04h, 0.04h), albedo, metallic);

    // Convert smoothness to roughness
    half roughness = 1.0h - smoothness;
    roughness = max(roughness, 0.001h); // Prevent numerical instability
    half alpha = roughness * roughness; // α² for GGX distribution

    // =====================
    // 2. BRDF Components
    // =====================

    // Combined geometric shadowing
    half G = grass_GeometrySchlickGGX(roughness, NdotV) * grass_GeometrySchlickGGX(roughness, NdotL);

    // Normal distribution function: GGX (Trowbridge-Reitz)
    half D = alpha * alpha / (3.1415926 * pow((NdotH * NdotH * (alpha*alpha - 1.0h) + 1.0h), 2.0h));

    // Fresnel: Schlick approximation
    half3 F = F0 + (1.0h - F0) * pow(1.0h - VdotH, 5.0h);

    // =====================
    // 3. Cook-Torrance Specular BRDF
    // =====================

    half3 numerator = D * G * F;
    half denominator = 4.0h * NdotL * NdotV;
    half3 specular = numerator / denominator;

    // =====================
    // 4. Diffuse Reflection (energy conservation)
    // =====================

    // Diffuse contribution: only non-metallic parts reflect diffuse
    half3 kD = (1.0h - F) * (1.0h - metallic);
    half3 diffuse = kD * albedo / 3.1415926;

    // =====================
    // 5. Light Attenuation
    // =====================

    half3 lighting = light.color * light.shadowAttenuation * light.distanceAttenuation;

    // =====================
    // 6. Final Color
    // =====================

    half3 color = (diffuse + specular) * NdotL * lighting;

    // =====================
    // 7. [Optional] Artistic Enhancement: Tip Gloss
    // =====================

    // Enhance specular on grass tips (e.g., dew, backlit edge)
    half tipGloss = saturate(positionY * 2.0h); // Adjust multiplier as needed
    half3 enhancedSpecular = F * tipGloss * smoothness * lighting;
    color += enhancedSpecular * 0.5h; // Add subtle enhancement


    return color;
}

float3 grass_Shading(
    float3 albedo,
    float smoothness,
    float metallic,
    float3 positionWS,
    float3 normalWS,
    float3 viewWS,
    float specularMask,
    float3 positionModel)
{
    float3 result = float3(0,0,0);
    float3 ambient = grass_AmbientDiffuse(albedo);
    result += ambient;

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
#ifdef _SHADER_TIP_SPECULAR
    result += ShadeGrassBlade_TipSpecular(mainLight, normalWS, viewWS, albedo, specularMask, positionModel.y);
#endif
#ifdef _SHADER_PBR
    result += ShadeGrassBlade_PBR(mainLight, normalWS, viewWS, albedo, metallic, smoothness, positionModel.y);
#endif
    
    int additionalLightsCount = 4;
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        Light light = GetAdditionalLight(i, positionWS);

#ifdef _SHADER_TIP_SPECULAR
        result += ShadeGrassBlade_TipSpecular(light, normalWS, viewWS, albedo, specularMask, positionModel.y);
#endif
#ifdef _SHADER_PBR
        result += ShadeGrassBlade_PBR(light, normalWS, viewWS, albedo, metallic, smoothness, positionModel.y);
#endif
    }
    
    return result;
}

#endif