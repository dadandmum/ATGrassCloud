Shader "ATGrassCloud/ATTerrainBRDFShader"
{
    Properties
    {
        [Header(Splat Textures)]
        _SplatMap1("Splat Map 1 (RGBA)", 2D) = "white" {}
        _SplatMap2("Splat Map 2 (RGBA)", 2D) = "white" {}

        [Header(Layer Textures 8 Layers)]
        _LayerAlbedoMap("Albedo Maps", 2DArray) = "white" {}
        _LayerMetallicSmoothnessMap("Metallic & Smoothness Maps", 2DArray) = "white" {}
        _LayerNormalMap("Normal Maps", 2DArray) = "bump" {}

        [NoScaleOffset] _HeightMap("Height Map", 2D) = "white" {}

        [Header(Height Mapping)]
        _HeightScale("Height Scale", Range(0.0, 0.1)) = 0.02
        _HeightSteps("Parallax Steps", Int) = 10

        
        [Toggle(_SINGLE_MATERIAL)]_ShaderPBR("Single Material", Float) = 0
    }

    HLSLINCLUDE


    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


    // include terrain lib
    #include "Assets/ATGrassCloud/Shader/Lib/TerrainLib.hlsl"
    #include "Assets/ATGrassCloud/Shader/Lib/TerrainMaterialLib.hlsl"


    struct Attributes
    {
        float3 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float4 tangentOS : TANGENT;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float3 tangentWS : TEXCOORD2;
        float3 bitangentWS : TEXCOORD3;
        float2 uv : TEXCOORD4;
    };



    Varyings Vert(Attributes input)
    {
        Varyings output;
        VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
        VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

        output.positionCS = vertexInput.positionCS;
        output.positionWS = vertexInput.positionWS;
        output.normalWS = normalInput.normalWS;
        output.tangentWS = normalInput.tangentWS;
        output.bitangentWS = normalInput.bitangentWS;
        output.uv = input.uv;

        return output;
    }


    float4 Frag(Varyings input) : SV_TARGET
    {
        float2 uv = input.uv;

        // sample 2 Splat Maps, total 8 weights
        float4 splat1 = SAMPLE_TEXTURE2D(_SplatMap1, sampler_SplatMap1, uv);
        float4 splat2 = SAMPLE_TEXTURE2D(_SplatMap2, sampler_SplatMap1, uv); // example: replace with _SplatMap2

        float weights[8] = {
            splat1.r, splat1.g, splat1.b, splat1.a,
            splat2.r, splat2.g, splat2.b, splat2.a
        };

        // normalize weights
        float weightSum = 0;
        [unroll] for (int i = 0; i < 8; ++i)
            weightSum += weights[i];
        weightSum = max(weightSum, 1e-4);
        [unroll] for (int i = 0; i < 8; ++i)
            weights[i] /= weightSum;

        // blend results
        float3 blendedAlbedo = 0;
        float blendedMetallic = 0;
        float blendedSmoothness = 0;
        float3 blendedNormalTS = 0;
        float3 viewWS = _WorldSpaceCameraPos - input.positionWS;
        float3 normalWS = input.normalWS;
        float3 tangentWS = input.tangentWS;
        float3 bitangentWS = input.bitangentWS;

        [unroll]
        for (int i = 0; i < 8; ++i)
        {
            if (weights[i] <= 0) continue;

            TerrainMaterial layer = SAMPLE_TERRAIN_LAYER(
                _LayerAlbedoMap,
                _LayerMetallicSmoothnessMap,
                _LayerNormalMap,
                _HeightMap,
                sampler_LayerAlbedoMap,
                sampler_LayerMetallicSmoothnessMap,
                sampler_LayerNormalMap,
                sampler_HeightMap,
                uv,
                i
            );

            // blend Albedo
            blendedAlbedo += layer.albedo * weights[i];

            // blend Metallic & Smoothness
            blendedMetallic += layer.metallic * weights[i];
            blendedSmoothness += layer.smoothness * weights[i];

            // blend Normal (Tangent Space)
            if (any(layer.normalTS))
            {
                float3x3 tbn = float3x3(tangentWS, bitangentWS, normalWS);
                float3 worldNormal = mul(layer.normalTS, tbn);
                blendedNormalTS += worldNormal * weights[i];
            }
        }

        // normalize blended normal
        if (any(blendedNormalTS))
            normalWS = normalize(blendedNormalTS);

        viewWS = normalize(viewWS);

        // use PBR Shading
        float3 finalColor = terrain_Shading(
            blendedAlbedo,
            blendedSmoothness,
            blendedMetallic,
            input.positionWS,
            normalWS,
            viewWS
        );

        return float4(finalColor, 1.0);
    }


    float4 FragSingle(Varyings input) : SV_TARGET
    {
        float2 uv = input.uv;
        float weight = 1.0;

        float3 viewWS = GetCameraPositionWS() - input.positionWS;
        float3 blendedNormalTS = 0;
        float3 normalWS = input.normalWS;
        float3 tangentWS = input.tangentWS;
        float3 bitangentWS = input.bitangentWS;
        
        TerrainMaterial layer = SAMPLE_TERRAIN_LAYER(
            _LayerAlbedoMap,
            _LayerMetallicSmoothnessMap,
            _LayerNormalMap,
            _HeightMap,
            sampler_LayerAlbedoMap,
            sampler_LayerMetallicSmoothnessMap,
            sampler_LayerNormalMap,
            sampler_HeightMap,
            uv,
            0
        );


        // blend Albedo
        float3 blendedAlbedo = layer.albedo * weight;

        // blend Metallic & Smoothness
        float blendedMetallic = layer.metallic * weight;
        float blendedSmoothness = layer.smoothness * weight;

        // blend Normal (Tangent Space)
        if (any(layer.normalTS))
        {
            float3x3 tbn = float3x3(tangentWS, bitangentWS, normalWS);
            float3 worldNormal = mul(layer.normalTS, tbn);
            blendedNormalTS += worldNormal * weight;
        }

        // normalize blended normal
        if (any(blendedNormalTS))
            normalWS = normalize(blendedNormalTS);

        // use PBR Shading
        float3 finalColor = terrain_Shading(
            blendedAlbedo,
            blendedSmoothness,
            blendedMetallic,
            input.positionWS,
            normalWS,
            viewWS
        );

        return float4(finalColor, 1.0);

    }



    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Forward"

            HLSLPROGRAM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SINGLE_MATERIAL

            
            #pragma vertex Vert
            #ifdef _SINGLE_MATERIAL
            #pragma fragment FragSingle
            #else
            #pragma fragment Frag
            #endif  
            

            ENDHLSL
        }
    }

    Fallback "Hidden/Shader Graph/FallbackError"
}