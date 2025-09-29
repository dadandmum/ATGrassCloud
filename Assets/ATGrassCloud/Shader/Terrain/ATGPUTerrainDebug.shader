Shader "ATGrassCloud/ATGPUTerrainDebug"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
        _HeightMap ("Height Map", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "white" {}
        _NormalIntensity ("Normal Intensity", Range(0, 1)) = 0.5
        _SplatMap0 ("Splat Map 0 ", 2D) = "white" {}
        _SplatMap1 ("Splat Map 1", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}
         
        Pass
        {
            Cull Back
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature ENABLE_MIP_DEBUG
            #pragma shader_feature ENABLE_PATCH_DEBUG
            #pragma shader_feature ENABLE_LOD_SEAMLESS
            #pragma shader_feature ENABLE_NODE_DEBUG

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/TerrainLib.hlsl"

            StructuredBuffer<RenderPatch> _PatchList;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                half3 color: TEXCOORD1;
            };

            TEXTURE2D( _MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D( _HeightMap);
            SAMPLER(sampler_HeightMap);
            TEXTURE2D( _NormalMap);
            SAMPLER(sampler_NormalMap);
            uniform float _NormalIntensity;
            TEXTURE2D( _SplatMap0);
            SAMPLER(sampler_SplatMap0);
            TEXTURE2D( _SplatMap1);
            SAMPLER(sampler_SplatMap1);
            // uniform float3 _WorldSize;
            float4x4 _WorldToNormalMapMatrix;

            float3 ApplyTileDebug(RenderPatch patch,float3 vertex){
                uint nodeCount = (uint)(5 * pow(2,5 - patch.lod));
                float nodeSize = _TerrainWorldSize.x / nodeCount;
                uint2 nodeLoc = floor((patch.position + _TerrainWorldSize.xz * 0.5) / nodeSize);
                float2 nodeCenterPosition = - _TerrainWorldSize.xz * 0.5 + (nodeLoc + 0.5) * nodeSize ;
                vertex.xz = nodeCenterPosition + (vertex.xz - nodeCenterPosition) * 0.95;
                return vertex;
            }


            float3 TransformNormalToWorldSpace(float3 normal){
                return SafeNormalize(mul(normal,(float3x3)_WorldToNormalMapMatrix));
            }


            float3 SampleNormal(float2 uv){
                // float3 normal;
                // normal.xz = tex2Dlod(_NormalMap,float4(uv,0,0)).xy * 2 - 1;
                // normal.y = sqrt(max(0,1 - dot(normal.xz,normal.xz)));
                // normal = TransformNormalToWorldSpace(normal);
                // return normal;

                float3 normal;
                normal.xz = SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_NormalMap, uv, 0).xy * 2 - 1;
                normal.y = sqrt(max(0,1 - dot(normal.xz,normal.xz)));
                normal = TransformNormalToWorldSpace(normal) * _NormalIntensity;
                return normal;
            }

            v2f vert (appdata v)
            {
                v2f o;
                
                float4 positionOS = v.vertex;
                float2 uv = v.uv;

                RenderPatch patch = _PatchList[v.instanceID];

                uint lod = patch.lod;
                float scale = GetMeshScaleByLOD(lod);
                // uint4 lodTrans = patch.lodTrans;

                float3 positionWS = positionOS.xyz;
                positionWS.xz *= scale;
                #if ENABLE_PATCH_DEBUG
                positionWS.xz *= 0.9;
                #endif
                // positionWS.xz += patch.position.xy;
                
                float patchSize = GetTileSize(lod) / 8 ;
                // float2 worldOffsetXZ = GetTilePositionWS2(patch.lodTrans.zw,lod) + (patch.lodTrans.xy - int2(7,7) * 0.5) * patchSize;
                // float2 worldOffsetXZ = GetTilePositionWS2(patch.lodTrans.zw,lod);
                float2 worldOffsetXZ = patch.position.xy;

                positionWS.xz += worldOffsetXZ;
                #if ENABLE_TILE_DEBUG
                positionWS = ApplyTileDebug(patch,positionWS);
                #endif

                float2 heightUV = (positionWS.xz + (_TerrainWorldSize.xz * 0.5) + 0.5) / (_TerrainWorldSize.xz + 1);
                float height = SAMPLE_TEXTURE2D_LOD(_HeightMap, sampler_HeightMap, heightUV, 0).r;
                positionWS.y = height * _TerrainWorldSize.y;

                // float3 normal = SampleNormal(heightUV);
                // Light light = GetMainLight();
                // o.color = max(0.05,dot(light.direction,normal));

                float4 vertex = TransformObjectToHClip(positionWS.xyz);
                o.vertex = vertex;
                o.uv = uv * scale * 8;

                o.color = GetDebugColor(lod);
                
                return o;
            } 

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 col0 = SAMPLE_TEXTURE2D_LOD(_SplatMap0, sampler_SplatMap0, i.uv, 0);
                half4 col1 = SAMPLE_TEXTURE2D_LOD(_SplatMap1, sampler_SplatMap1, i.uv, 0);
                
                half4 col = max( col0 , col1) ;
                col.rgb *= i.color;

                return half4( i.color,1.0);
                return half4( col.rgb,1.0);
            }
            ENDHLSL
        }
    }
}
