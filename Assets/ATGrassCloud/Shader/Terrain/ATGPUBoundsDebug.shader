Shader "ATGrassCloud/ATBoundsDebug"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalForward"}
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Cull Off
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature ENABLE_MIP_DEBUG

 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/TerrainLib.hlsl"

            StructuredBuffer<BoundsDebug> _BoundsList;
            // StructuredBuffer<RenderPatch> _PatchList;

            struct appdata
            {
                float4 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 color: TEXCOORD1;
            };


            v2f vert (appdata v)
            {
                v2f o;
                float4 inVertex = v.vertex;
                BoundsDebug boundsDebug = _BoundsList[v.instanceID];
                Bounds bounds = boundsDebug.bounds;
                // RenderPatch patch = _PatchList[v.instanceID];

                float3 center = (bounds.minPosition + bounds.maxPosition) * 0.5;
                float meshSize = 0.5;

                float3 scale = (bounds.maxPosition - center) / meshSize;

                float3 positionWS = inVertex.xyz * scale + center;

                float4 vertex = TransformObjectToHClip(positionWS);
                o.vertex = vertex;
                o.color = boundsDebug.color.rgb;
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                half4 col = half4(i.color,1);
                return col;
            }
            ENDHLSL
        }
    }
}
