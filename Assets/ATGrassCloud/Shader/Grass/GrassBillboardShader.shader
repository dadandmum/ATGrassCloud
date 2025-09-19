Shader "ATGrassCloud/GrassBillboardShader"

{

    Properties
    {
        [Header(Grass Shading)]
        _ColorA("ColorA(Wet Grass)", Color) = (0.2,1,0.8,1)
        _ColorB("ColorB(Dry Grass)", Color) = (0.5,0.8,0.8,1)
        _ColorNoiseFactor("Color Noise Factor", Range(0, 10.0)) = 0.5
        _AOColor("AO Color", Color) = (0.5,0.5,0.5)
        _AOFactor("AO Factor", Range(0, 2.0)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0.5
        _OpacityTex("Opacity Text", 2D) = "white" {}

        // [Header(Subsurface Scattering)]
        // _SSSTint("SSS Tint", Color) = (1.0,1.0,1.0, 1) // SSS Color 
        // _SSSTranslucencyTex("SSS Translucency Map", 2D) = "white" {} // SSS Color Map  
        // _SSSPower("SSS Power", Range(0.0, 10.0)) = 1.0 
        // _SSSDistortion("SSS Distortion", Range(0.0, 2.0)) = 0.5
        // _SSSScale("SSS Scale", Range(0.0, 1.0)) = 0.5


        [Header(Grass Geometry)][Space]
        _GrassWidth("Grass Billboard Width", Float) = 1
        _GrassHeight("Grass Billboard Height", Float) = 1
        _GrassScale("Grass Scale", Float) = 1
        _ObjectOffset("Object Offset", Vector) = (0, 0, 0, 0)

        // _GrassCurveKeys("Grass Curve Key Values" , Vector ) = (1.0,0.5,0.5,0.5)

        [Header(Grass Random)][Space]
        _GrassWidthRandomness("Grass Width Randomness", Range(0, 1)) = 0
        _GrassHeightRandomness("Grass Height Randomness", Range(0, 1)) = 0
        _GrassUpDirectionRandom("Grass Up Direction Randomness", Range(0, 1)) = 0.25
        _GrassFaceDirectionRandom("Grass Face Direction Randomness", Range(0, 1)) = 0.25
        _GrassScaleRandom("Grass Scale Randomness", Range(0, 1)) = 0.25
        _GrassDroopIntensity("Grass Droop Intensity", Range(0, 1)) = 0.5
        // _GrassCurving("Grass Curving", Float) = 0.1

        [Header(Grass Expand)][Space]
        _ExpandDistantGrassWidth("Expand Distant Grass Width", Float) = 1
        // _ExpandDistantGrassRange("Expand Distant Grass Range", Vector) = (50, 200, 0, 0)
        
        // [Header(Normal)][Space]
        // _GrassNormalBlend("Grass Normal Blend", Range(-1, 1)) = 0.25

        [Header(Wind)][Space]
        // _WindTexture("Wind Texture", 2D) = "white" {}
        // _WindScroll("Wind Scroll", Vector) = (1, 1, 0, 0)
        _WindRandomness("Wind Randomness", Range(0, 1)) = 0.25
        _WindStrength("Wind Strength", Float) = 1


        // [Header(Mipmap)]
        // _TexMipDistance("Tex Mip Distance", Float) = 40.0

        [Header(AlphaCut)][Space]
        _Cutoff("Cutoff", Range(0, 1)) = 0.5
        // [Header(Lighting)][Space]
        // _RandomNormal("Random Normal", Range(0, 1)) = 0.1

        // [Toggle(_SINGLE_MESH)]_ShaderPBR("Single Mesh", Float) = 1
        // [Toggle(_FACE_TO_CAMERA)]_FaceToCamera("Face To Camera", Float) = 0

        // [Toggle(_INSTANCE_MESH)]_ShaderTipSpecular("Instance Mesh", Float) = 0
        [Toggle(_USE_WIND)]_UseWind("Use Wind", Float) = 0
        [Toggle(_SHADER_PBR)]_ShaderPBR("Shader PBR", Float) = 1
        [Toggle(_SHADER_TIP_SPECULAR)]_ShaderTipSpecular("Shader Tip Specular", Float) = 0
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
            #pragma multi_compile _ _SHADER_TIP_SPECULAR _SHADER_PBR
            #pragma multi_compile _ _USE_WIND


            //#pragma multi_compile _ _SINGLE_MESH _INSTANCE_MESH
            
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/GrassLib.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/Simplex.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/WindLib.hlsl"
            
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
                float3 color       : TEXCOORD1;
                // float3 positionWS  : TEXCOORD1;
                // float3 normalWS    : TEXCOORD2;
                // float3 tangentWS   : TEXCOORD3;
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
                float _GrassWidth;
                float _GrassHeight;
                // float4 _GrassCurveKeys;
                float _GrassWidthRandomness;
                float _GrassHeightRandomness;
                float4 _ObjectOffset;


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

                StructuredBuffer<GrassData> _GrassData;

            CBUFFER_END

            TEXTURE2D(_OpacityTex);
            SAMPLER(sampler_OpacityTex);

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
                float3 pivot = _GrassData[instanceID].position;
                uint rand = _GrassData[instanceID].rand0;
                float3 faceDirection = GetDefaultFaceDirection(pivot, rand, _GrassFaceDirectionRandom, _WorldSpaceCameraPos);
                float3 upDirection = GetDefaultUpDirection(pivot, rand, _GrassUpDirectionRandom, _WorldSpaceCameraPos);
                
                float scale = max( 0.001f , _GrassScale) *  ( 1.0 - decodeRandByBit(rand, 3) * _GrassScaleRandom); 

                float3 positionOS = (IN.positionOS + _ObjectOffset.xyz) * ( 1.0 + _ObjectOffset.w );

                float3 positionModel = positionOS * scale; 
                positionModel = grass_AdjustWithRandomness(pivot, positionModel, _GrassWidth, _GrassWidthRandomness, _GrassHeight, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
               
#ifdef _USE_WIND

              // deal with wind
                float2 wind = GetWind(pivot.xz, _WindPositionParams);
                upDirection = grass_ApplyWindToUp(upDirection, wind, _WindStrength, rand , _WindRandomness, positionModel);  
#endif 
                
                float3 positionModelWithRot = grass_RecalculateByDirection(
                    upDirection, 
                    faceDirection, 
                    positionModel,  
                    0.0 );

                float3 positionWS = pivot + TransformObjectToWorld(positionModelWithRot);
                float3 normalModel = normalize(IN.normalOS);

                float3 normalWS = grass_GetNormal(
                    positionModel,
                    upDirection,
                    faceDirection,
                    normalModel,
                    0.0
                );

                float albedoRand = snoise(positionWS / _CascadeRange.y * 3.41 * _ColorNoiseFactor);
                float3 albedo = grass_Albedo(_ColorA, _ColorB, albedoRand, _AOColor, positionOS, _AOFactor);
                float3 viewWS = normalize(_WorldSpaceCameraPos - positionWS);

                OUT.positionCS = TransformWorldToHClip(positionWS);

                float specularMask = 0.0; // TODO : pass by map
                OUT.color = grass_Shading(albedo, _Smoothness, _Metallic, positionWS, normalWS, viewWS, specularMask, positionModel);
                
                OUT.color += _DebugColor;
                OUT.uv = IN.uv;


                return OUT;

                // return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            { 
                // Get Data fron Input 
                float2 uv = IN.uv;

                float opacity = SAMPLE_TEXTURE2D_LOD(_OpacityTex, sampler_OpacityTex, uv, 0 ).r;
                clip(opacity - _Cutoff);
                float3 color = IN.color * opacity ;


                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}