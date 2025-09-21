Shader "ATGrassCloud/CubeCreator"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (0.5, 0.7, 1.0, 1) 
        _SkyIntensity ("Sky Intensity", Range(0.0, 10.0)) = 1.0
        _GroundColor ("Ground Color", Color) = (0.1, 0.3, 0.1, 1)
        _GroundIntensity ("Ground Intensity", Range(0.0, 10.0)) = 1.0
        _BlendPower ("Blend Power", Range(1.0, 10.0)) = 2.0 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _SkyColor;
            float _SkyIntensity;
            float4 _GroundColor;
            float _GroundIntensity;
            float _BlendPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0; // direction vector 
            };

            struct v2f
            {
                float3 dir : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                // Transform the vertex position to clip space
                o.pos = UnityObjectToClipPos(v.vertex);
                // // Convert the vertex position to a world space direction (for cubemap cameras, this is usually the direction)
                // o.dir = mul(unity_ObjectToWorld, v.vertex).xyz;// - _WorldSpaceCameraPos.xyz;

                o.dir = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize the direction vector
                float3 dir = normalize(i.dir);

                // Use the Y component as a blending factor
                // Map [-1, 1] to [0, 1]
                float t = dir.y * 0.5 + 0.5; // -1 -> 0, 0 -> 0.5, 1 -> 1

                // Apply a power function for "cubic" interpolation (actually power adjustment to simulate a more natural gradient)
                t = pow(saturate(t), _BlendPower);

                // Linear interpolation
                fixed4 color = lerp(_GroundColor * _GroundIntensity, _SkyColor * _SkyIntensity, t);

                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}