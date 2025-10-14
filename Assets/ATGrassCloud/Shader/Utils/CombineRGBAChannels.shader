Shader "Hidden/CombineRGBAChannels"
{
    Properties
    {
        _AOMap ("AO Map (Red)", 2D) = "white" {}
        _RoughnessMap ("Roughness Map (Green)", 2D) = "white" {}
        _MetalMap ("Metal Map (Blue)", 2D) = "black" {}
        _EmissionMap ("Emission Map (Alpha)", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _AOMap;
            sampler2D _RoughnessMap;
            sampler2D _MetalMap;
            sampler2D _EmissionMap;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed ao      = tex2D(_AOMap,         i.uv).r;
                fixed rough   = tex2D(_RoughnessMap,  i.uv).r;
                fixed metal   = tex2D(_MetalMap,      i.uv).r;
                fixed emission = tex2D(_EmissionMap,  i.uv).r;

                return fixed4(ao, rough, metal, emission);
            }
            ENDCG
        }
    }
    Fallback Off
}