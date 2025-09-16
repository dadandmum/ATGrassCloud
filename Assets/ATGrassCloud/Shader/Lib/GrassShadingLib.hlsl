#ifndef __GRASS_SHADING_LIB_HLSL__
#define __GRASS_SHADING_LIB_HLSL__

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) 
         
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
    float3 forward = float3( 0,0,-1.0f);
    float3 blendOffset = float3(1.0,0,0) * normalBlend * positionModel.x * 50.0 ;
    return normalize(forward + blendOffset);
}


float3 grass_GetTangent(
    float3 up , 
    float3 forward ,
    float3 modelTangent
)
{
    float3 newForward = normalize(forward);
    float3 right = normalize( cross(up, newForward));
    float3 newUp = normalize( - cross(right, newForward));

    
    // 标准变换：将模型法线变换到世界/局部空间
    float3 T = modelTangent.x * right +
               modelTangent.y * newUp +
               modelTangent.z * newForward;
    T = normalize(T);
    return T;


}

float3 grass_GetNormal(
    float3 positionModel,
    float3 up , 
    float3 forward ,
    float3 modelNormal,
    float droop) {

    // float3 right = -cross(up, forward);
    // float3 newUp = cross(right, forward);

    float3 newForward = normalize(forward);
    float3 right = normalize( cross(up, newForward));
    float3 newUp = normalize( - cross(right, newForward));

    
    // 标准变换：将模型法线变换到世界/局部空间
    float3 N = modelNormal.x * right +
               modelNormal.y * newUp +
               modelNormal.z * newForward;
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
float3 ShadeGrassBlade_PBR(
    Light light,
    float3 N,
    float3 V,
    float3 albedo,
    float metallic,
    float smoothness,
    float positionY
)
{
    float3 L = light.direction;
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

    float3 lighting = light.color * light.shadowAttenuation * light.distanceAttenuation;

    // =====================
    // 6. Final Color
    // =====================

    float3 color = (diffuse + specular ) * NdotL * lighting;    

    // =====================
    // 7. [Optional] Artistic Enhancement: Tip Gloss
    // =====================

    // Enhance specular on grass tips (e.g., dew, backlit edge)
    float tipGloss = saturate(positionY * 2.0f); // Adjust multiplier as needed
    float3 enhancedSpecular = F * tipGloss * smoothness * lighting;
    color += enhancedSpecular * 0.5f; // Add subtle enhancement

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
    
    int additionalLightsCount = GetAdditionalLightsCount();
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


float3 grass_ShadingPBR(
    float3 albedo,
    float smoothness,
    float metallic,
    float3 positionWS,
    float3 normalWS,
    float3 viewWS)
{
    float3 result = float3(0,0,0);
    float3 ambient = grass_AmbientDiffuse(albedo);
    result += ambient;

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    result += ShadeGrassBlade_PBR(mainLight, normalWS, viewWS, albedo, metallic, smoothness, 0);
    
    int additionalLightsCount = 4;
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        Light light = GetAdditionalLight(i, positionWS);
        result += ShadeGrassBlade_PBR(light, normalWS, viewWS, albedo, metallic, smoothness, 0);
    }
    
    return result;
}

half3 WrapLighting(half3 lightDir, half3 normal, half wrapLight)
{
    return saturate((dot(lightDir, normal) + wrapLight) / ((1 + wrapLight) * (1 + wrapLight)));
}

// SSS模拟函数 (基于厚度和光线方向)
half3 SubsurfaceScattering(half3 albedo, Light light, half3 viewDir, half3 normal, half thickness, half3 sssTint, half sssPower, half sssDistortion, half sssScale)
{
    half3 lightDir = light.direction;
    half3 lighting = light.color * light.shadowAttenuation * light.distanceAttenuation;

    // 计算光线入射方向的反向
    half3 lightTransmission = -lightDir;
    
    // 根据厚度图和光线方向计算SSS贡献
    // 这里使用了一个简化的模型，基于光线穿透和散射
    half3 sssNormal = normal + lightTransmission * sssDistortion;
    sssNormal = normalize(sssNormal);
    
    // 使用Wrap Lighting模拟透射光
    half wrapLight = 0.5; // 控制Wrap效果
    half3 sssLighting = WrapLighting(lightTransmission, sssNormal, wrapLight);
    
    // 应用厚度和SSS参数
    half sssIntensity = pow(saturate(dot(viewDir, -sssNormal)), sssPower) * thickness * sssScale * 20.0;
    
    // 最终SSS颜色
    half3 sssColor = sssLighting * sssTint * sssIntensity * albedo;

    
    return sssColor * lighting;
}


half3 grass_SubsurfaceScattering(half3 albedo, float3 positionWS, half3 viewDir, half3 normal, half thickness, half3 sssTint, half sssPower, half sssDistortion, half sssScale)
{
    float3 result = float3(0,0,0);

    Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
    result += SubsurfaceScattering(albedo, mainLight, viewDir, normal, thickness, sssTint, sssPower, sssDistortion, sssScale);
    
    int additionalLightsCount = 4;
    for (int i = 0; i < additionalLightsCount; ++i)
    {
        Light light = GetAdditionalLight(i, positionWS);
        result += SubsurfaceScattering(albedo, light, viewDir, normal, thickness, sssTint, sssPower, sssDistortion, sssScale);
    }
    
    return result;


}


#endif