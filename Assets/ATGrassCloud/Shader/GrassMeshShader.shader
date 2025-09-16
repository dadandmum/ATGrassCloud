Shader "ATGrassCloud/GrassMeshShader"
{

    Properties
    {
        [Header(Grass Shading)]
        // _ColorA("ColorA(Wet Grass)", Color) = (0.2,1,0.8,1)
        // _ColorB("ColorB(Dry Grass)", Color) = (0.5,0.8,0.8,1)
        // _ColorNoiseFactor("Color Noise Factor", Range(0, 10.0)) = 0.5
        _AlbedoTex("Albedo Texture", 2D) = "white" {}
        _NormalTex("Normal Texture", 2D) = "bump" {}
        _RoughnessTex("Roughness Texture", 2D) = "white" {}
        _OpacityTex("Opacity Texture", 2D) = "white" {}
        _NormalIntensity("Normal Intensity", Range(0, 1)) = 0.5
        // _AOColor("AO Color", Color) = (0.5,0.5,0.5)
        // _AOFactor("AO Factor", Range(0, 2.0)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0.5

        [Header(Subsurface Scattering)]
        _SSSTint("SSS Tint", Color) = (1.0,1.0,1.0, 1) // SSS Color 
        _SSSTranslucencyTex("SSS Translucency Map", 2D) = "white" {} // SSS Color Map  
        _SSSPower("SSS Power", Range(0.0, 10.0)) = 1.0 
        _SSSDistortion("SSS Distortion", Range(0.0, 2.0)) = 0.5
        _SSSScale("SSS Scale", Range(0.0, 1.0)) = 0.5


        // [Header(Grass Geometry)][Space]
        // _GrassWidth("Grass Width", Float) = 1
        // _GrassHeight("Grass Height", Float) = 1
        // _GrassCurveKeys("Grass Curve Key Values" , Vector ) = (1.0,0.5,0.5,0.5)
        // _GrassWidthRandomness("Grass Width Randomness", Range(0, 1)) = 0.25
        // _GrassHeightRandomness("Grass Height Randomness", Range(0, 1)) = 0.5

        [Header(Grass Random)][Space]
        _GrassUpDirectionRandom("Grass Up Direction Randomness", Range(0, 1)) = 0.25
        _GrassFaceDirectionRandom("Grass Face Direction Randomness", Range(0, 1)) = 0.25
        _GrassScaleRandom("Grass Scale Randomness", Range(0, 1)) = 0.25
        _GrassDroopIntensity("Grass Droop Intensity", Range(0, 1)) = 0.5
        // _GrassCurving("Grass Curving", Float) = 0.1

        [Header(Grass Expand)][Space]
        _GrassScale("Grass Scale", Float) = 1
        _ExpandDistantGrassWidth("Expand Distant Grass Width", Float) = 1
        // _ExpandDistantGrassRange("Expand Distant Grass Range", Vector) = (50, 200, 0, 0)
        
        // [Header(Normal)][Space]
        // _GrassNormalBlend("Grass Normal Blend", Range(-1, 1)) = 0.25

        [Header(Wind)][Space]
        // _WindTexture("Wind Texture", 2D) = "white" {}
        // _WindScroll("Wind Scroll", Vector) = (1, 1, 0, 0)
        _WindRandomness("Wind Randomness", Range(0, 1)) = 0.25
        _WindStrength("Wind Strength", Float) = 1


        [Header(Mipmap)]
        _TexMipDistance("Tex Mip Distance", Float) = 40.0

        [Header(AlphaCut)][Space]
        _Cutoff("Cutoff", Range(0, 1)) = 0.5


        // [Header(Lighting)][Space]
        // _RandomNormal("Random Normal", Range(0, 1)) = 0.1

        [Toggle(_SINGLE_MESH)]_ShaderPBR("Single Mesh", Float) = 1
        [Toggle(_FACE_TO_CAMERA)]_FaceToCamera("Face To Camera", Float) = 0

        // [Toggle(_INSTANCE_MESH)]_ShaderTipSpecular("Instance Mesh", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}

        Pass
        {
            Cull Back
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _FACE_TO_CAMERA

            //#pragma multi_compile _ _SINGLE_MESH _INSTANCE_MESH
            
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Lib/GrassLib.hlsl"
            #include "Lib/WindLib.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float3 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                // Shading 
                half3 _ColorA;
                half3 _ColorB;
                float4 _BaseColorTexture_ST;
                half3 _AOColor;
                float _AOFactor;
                float _ColorNoiseFactor;
                float _Smoothness;
                float _Metallic;
                float _NormalIntensity;

                // SSS
                half3 _SSSTint;
                float _SSSPower;
                float _SSSDistortion;
                float _SSSScale;



                // Geometry 
                // float _GrassWidth;
                // float _GrassHeight;
                // float4 _GrassCurveKeys;
                // float _GrassWidthRandomness;
                // float _GrassHeightRandomness;

                float _GrassUpDirectionRandom;
                float _GrassFaceDirectionRandom;
                float _GrassScaleRandom;


                float _GrassDroopIntensity;
                float _GrassCurving;

                float _GrassScale;
                float _ExpandDistantGrassWidth;
                float2 _ExpandDistantGrassRange;
                float4 _CascadeRange; // ( innerRange , outterRange , 1.0f / innerFade , 1.0f / outterFade )

                // Normal 
                // float _GrassNormalBlend;
 
                float4 _WindTexture_ST;
                float _WindStrength;
                float2 _WindScroll;
                float4 _WindPositionParams;
                float _WindRandomness;

                // half _RandomNormal;

                float4  _MapData;
                // Debug 
                float3 _DebugColor;
                float _Cutoff;
                float _TexMipDistance;



// #ifdef _INSTANCE_MESH
                StructuredBuffer<GrassData> _GrassData;
// #endif
            CBUFFER_END

            TEXTURE2D(_AlbedoTex);
            SAMPLER(sampler_AlbedoTex);
            TEXTURE2D(_NormalTex);
            SAMPLER(sampler_NormalTex);
            TEXTURE2D(_OpacityTex);
            SAMPLER(sampler_OpacityTex);
            TEXTURE2D(_RoughnessTex);
            SAMPLER(sampler_RoughnessTex);
            TEXTURE2D(_SSSTranslucencyTex);
            SAMPLER(sampler_SSSTranslucencyTex);


            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
// #ifdef _INSTANCE_MESH
                float3 pivot = _GrassData[instanceID].position;
                uint rand = _GrassData[instanceID].rand0;
#ifdef _FACE_TO_CAMERA
                float3 faceDirection = GetDefaultFaceDirection(pivot, rand, _GrassFaceDirectionRandom, _WorldSpaceCameraPos);
#else
                float3 faceDirection = GetRandomFaceDirection(rand);
#endif
                float3 upDirection = GetDefaultUpDirection(pivot, rand, _GrassUpDirectionRandom, _WorldSpaceCameraPos);

// #else  //  _SINGLE_MESH

//                 float3 pivot = float3(0, 0, 0);
//                 float3 faceDirection = TransformObjectToWorld(float3(0,0,1.0));
//                 float3 upDirection = TransformObjectToWorld(float3(0,1.0,0));
//                 uint rand = 0;
// #endif 
                
                float scale = max( 0.001f , _GrassScale) *  ( 1.0 - decodeRandByBit(rand, 3) * _GrassScaleRandom); 

                float3 positionModel = IN.positionOS * scale;
                // deal with wind
                float2 wind = GetWind(pivot.xz, _WindPositionParams);
                upDirection = grass_ApplyWindToUp(upDirection, wind, _WindStrength, rand , _WindRandomness, positionModel);

                float3 positionModelWithRot = grass_RecalculateByDirection(
                    upDirection, 
                    faceDirection, 
                    positionModel,  
                    _GrassDroopIntensity);
                 

// #ifdef _INSTANCE_MESH
                float3 positionWS = pivot + TransformObjectToWorld(positionModelWithRot);
                float3 tangentWS = IN.tangentOS.xyz;
// #else // _SINGLE_MESH
//                 float3 positionWS = TransformObjectToWorld( IN.positionOS);
//                 float3 tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
// #endif 
 
                float3 normalModel = normalize(IN.normalOS);


                float3 normalWS = grass_GetNormal(
                    positionModel,
                    upDirection,
                    faceDirection,
                    normalModel,
                    _GrassDroopIntensity
                );

                tangentWS = grass_GetTangent(upDirection, faceDirection, IN.tangentOS.xyz);

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);

                OUT.uv = IN.uv;
                OUT.normalWS = normalWS;

                OUT.tangentWS = tangentWS.xyz;

                return OUT;

                // float3 pivot = _GrassData[instanceID].position;
                // uint rand = _GrassData[instanceID].rand0;

                // // float2 uv = (pivot.xz - _CenterPos) / (_DrawDistance + _SnapDistance);
                // float2 uv = (pivot.xz - _MapData.xy) * _MapData.w;
                // uv = uv * 0.5 + 0.5;


                // float3 upDirection = GetDefaultUpDirection(pivot, rand, _GrassUpDirectionRandom, _WorldSpaceCameraPos);
                // float3 faceDirection = GetDefaultFaceDirection(pivot, rand, _GrassFaceDirectionRandom, _WorldSpaceCameraPos);

                // float3 positionOS = IN.positionOS;
                // #ifdef _PROCEDURAL_MESH
                // float3 positionModel = grass_CreateProceduralPosition(positionOS, _GrassWidth, _GrassHeight, _GrassCurveKeys, _GrassWidthRandomness * grass_random(pivot.x * 842 + pivot.z * 12) );
                // positionModel = grass_AdjustWithRandomness(pivot, positionModel, 1.0, _GrassWidthRandomness, 1.0, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                // #else

                // float3 positionModel = grass_AdjustWithRandomness(pivot, positionOS, _GrassWidth, _GrassWidthRandomness, _GrassHeight, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                // #endif


                // // TODO : add wind 
                // float2 wind = GetWind(pivot.xz, _WindPositionParams);
                // upDirection = grass_ApplyWindToUp(upDirection, wind, _WindStrength, rand , _WindRandomness, positionModel);
                
                // float3 positionModelWithRot = grass_RecalculateByDirection(
                //     upDirection, 
                //     faceDirection, 
                //     positionModel, 
                //     _GrassDroopIntensity);
                 
                // //posOS -> posWS
                // float3 positionWS = positionModelWithRot + pivot;

                // float3 modelNormal = grass_ModelNormal(positionModel, _GrassNormalBlend);
                // float3 normalWS = grass_GetNormal(
                //     positionModel,
                //     upDirection,
                //     faceDirection,
                //     modelNormal,
                //     _GrassDroopIntensity);

                // float albedoRand = snoise(positionWS / _CascadeRange.y * 3.41 * _ColorNoiseFactor);
                // float3 albedo = grass_Albedo(_ColorA, _ColorB, albedoRand, _AOColor, positionOS, _AOFactor);
                // float3 viewWS = normalize(_WorldSpaceCameraPos - positionWS);

                // //posWS -> posCS
                // OUT.positionCS = TransformWorldToHClip(positionWS);

                // // shading 
                // float specularMask = 0.0; // TODO : pass by map
                // OUT.color = grass_Shading(albedo, _Smoothness, _Metallic, positionWS, normalWS, viewWS, specularMask, positionModel);
                // OUT.color += _DebugColor;


                // return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            { 
                // Get Data fron Input 
                float2 uv = IN.uv;

                float opacity = SAMPLE_TEXTURE2D_LOD(_OpacityTex, sampler_OpacityTex, uv, 0 ).r;
                clip(opacity - _Cutoff);
                float3 positionWS = IN.positionWS;
                float3 normalWS = normalize(IN.normalWS);
                float3 tangentWS = IN.tangentWS;

                int lod = GetTextureLOD(IN.positionWS, _WorldSpaceCameraPos, _TexMipDistance);
                
                float3 albedo = SAMPLE_TEXTURE2D_LOD(_AlbedoTex, sampler_AlbedoTex, uv, lod).rgb;
                float roughness = SAMPLE_TEXTURE2D_LOD(_RoughnessTex , sampler_RoughnessTex, uv, lod).r;
                float smoothness = (1.0 - roughness) * _Smoothness;
                float metallic = _Metallic;
                float3 normalTex = UnpackNormal(SAMPLE_TEXTURE2D_LOD(_NormalTex, sampler_NormalTex, uv, lod));
                normalTex = normalize(normalTex);

                // apply normal texture 
                float3 bitangentWS = cross(normalWS, tangentWS);
                float3 normalBump = normalize(tangentWS * normalTex.x + bitangentWS * normalTex.y + normalWS * normalTex.z);
                normalWS = normalize( normalWS + normalBump * _NormalIntensity * 10 );
                float3 viewWS = normalize(_WorldSpaceCameraPos - positionWS);

                // PBR
                float3 color = grass_ShadingPBR(albedo, smoothness, metallic, positionWS, normalWS, viewWS);

                // SSS
                float3 sssColor = SAMPLE_TEXTURE2D_LOD(_SSSTranslucencyTex, sampler_SSSTranslucencyTex, uv, lod).rgb;
                sssColor *= _SSSTint;
                float sssThickness = 1.0;
                color += grass_SubsurfaceScattering(albedo, positionWS, viewWS, normalWS, sssThickness, sssColor, _SSSPower, _SSSDistortion, _SSSScale);

                return half4(color, opacity);
            }
            ENDHLSL
        }
    }
}