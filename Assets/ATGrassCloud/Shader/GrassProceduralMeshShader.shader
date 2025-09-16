Shader "ATGrassCloud/GrassProceduralMeshShader"
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

        [Header(Grass Geometry)][Space]
        _GrassWidth("Grass Width", Float) = 1
        _GrassHeight("Grass Height", Float) = 1
        _GrassCurveKeys("Grass Curve Key Values" , Vector ) = (1.0,0.5,0.5,0.5)
        _GrassWidthRandomness("Grass Width Randomness", Range(0, 1)) = 0.25
        _GrassHeightRandomness("Grass Height Randomness", Range(0, 1)) = 0.5

        [Header(Grass Direction)][Space]
        _GrassUpDirectionRandom("Grass Up Direction Randomness", Range(0, 1)) = 0.25
        _GrassFaceDirectionRandom("Grass Face Direction Randomness", Range(0, 1)) = 0.25
        _GrassDroopIntensity("Grass Droop Intensity", Range(0, 1)) = 2.0
        // _GrassCurving("Grass Curving", Float) = 0.1

        [Space]
        _ExpandDistantGrassWidth("Expand Distant Grass Width", Float) = 1
        _ExpandDistantGrassRange("Expand Distant Grass Range", Vector) = (50, 200, 0, 0)
        
        [Header(Normal)][Space]
        _GrassNormalBlend("Grass Normal Blend", Range(-1, 1)) = 0.25

        [Header(Wind)][Space]
        // _WindTexture("Wind Texture", 2D) = "white" {}
        // _WindScroll("Wind Scroll", Vector) = (1, 1, 0, 0)
        _WindRandomness("Wind Randomness", Range(0, 1)) = 0.25
        _WindStrength("Wind Strength", Float) = 1

        // [Header(Lighting)][Space]
        // _RandomNormal("Random Normal", Range(0, 1)) = 0.1

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
            #pragma multi_compile _ _PROCEDURAL_MESH
            #pragma multi_compile _ _SHADER_TIP_SPECULAR _SHADER_PBR

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Lib/GrassLib.hlsl"
            #include "Lib/Simplex.hlsl"
            #include "Lib/WindLib.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half3 color        : COLOR;
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

                // Geometry 
                float _GrassWidth;
                float _GrassHeight;
                float4 _GrassCurveKeys;
                float _GrassWidthRandomness;
                float _GrassHeightRandomness;

                float _GrassUpDirectionRandom;
                float _GrassFaceDirectionRandom;

                float _GrassDroopIntensity;
                float _GrassCurving;

                float _ExpandDistantGrassWidth;
                float2 _ExpandDistantGrassRange;
                float4 _CascadeRange; // ( innerRange , outterRange , 1.0f / innerFade , 1.0f / outterFade )

                // Normal 
                float _GrassNormalBlend;
 
                float4 _WindTexture_ST;
                float _WindStrength;
                float2 _WindScroll;
                float4 _WindPositionParams;
                float _WindRandomness;

                half _RandomNormal;

                float4  _MapData;

                // Debug 
                float3 _DebugColor;

                StructuredBuffer<GrassData> _GrassData;

            CBUFFER_END

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 pivot = _GrassData[instanceID].position;
                uint rand = _GrassData[instanceID].rand0;

                // float2 uv = (pivot.xz - _CenterPos) / (_DrawDistance + _SnapDistance);
                float2 uv = (pivot.xz - _MapData.xy) * _MapData.w;
                uv = uv * 0.5 + 0.5;

                float3 upDirection = GetDefaultUpDirection(pivot, rand, _GrassUpDirectionRandom, _WorldSpaceCameraPos);
                float3 faceDirection = GetDefaultFaceDirection(pivot, rand, _GrassFaceDirectionRandom, _WorldSpaceCameraPos);

                float3 positionOS = IN.positionOS;
                #ifdef _PROCEDURAL_MESH
                float3 positionModel = grass_CreateProceduralPosition(positionOS, _GrassWidth, _GrassHeight, _GrassCurveKeys, _GrassWidthRandomness * grass_random(pivot.x * 842 + pivot.z * 12) );
                positionModel = grass_AdjustWithRandomness(pivot, positionModel, 1.0, _GrassWidthRandomness, 1.0, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                #else

                float3 positionModel = grass_AdjustWithRandomness(pivot, positionOS, _GrassWidth, _GrassWidthRandomness, _GrassHeight, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                #endif


                // TODO : add wind 
                float2 wind = GetWind(pivot.xz, _WindPositionParams);
                upDirection = grass_ApplyWindToUp(upDirection, wind, _WindStrength, rand , _WindRandomness, positionModel);
                
                float3 positionModelWithRot = grass_RecalculateByDirection(
                    upDirection, 
                    faceDirection, 
                    positionModel, 
                    _GrassDroopIntensity);
                 
                //posOS -> posWS
                float3 positionWS = positionModelWithRot + pivot;

                float3 modelNormal = grass_ModelNormal(positionModel, _GrassNormalBlend);
                float3 normalWS = grass_GetNormal(
                    positionModel,
                    upDirection,
                    faceDirection,
                    modelNormal,
                    _GrassDroopIntensity);

                float albedoRand = snoise(positionWS / _CascadeRange.y * 3.41 * _ColorNoiseFactor);
                float3 albedo = grass_Albedo(_ColorA, _ColorB, albedoRand, _AOColor, positionOS, _AOFactor);
                float3 viewWS = normalize(_WorldSpaceCameraPos - positionWS);

                //posWS -> posCS
                OUT.positionCS = TransformWorldToHClip(positionWS);

                // shading 
                float specularMask = 0.0; // TODO : pass by map
                OUT.color = grass_Shading(albedo, _Smoothness, _Metallic, positionWS, normalWS, viewWS, specularMask, positionModel);
                OUT.color += _DebugColor;


                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(IN.color.rgb,1);
            }
            ENDHLSL
        }
    }
}