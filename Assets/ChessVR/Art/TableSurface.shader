Shader "Gambito/Table Surface"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _Glossiness ("Brilho", Range(0,1)) = 0.3
        _Metallic ("Metal", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emissão", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half4 _EmissionColor;
        struct Input { float3 worldPos; };
        void surf(Input input, inout SurfaceOutputStandard surface)
        {
            surface.Albedo = _Color.rgb;
            surface.Smoothness = _Glossiness;
            surface.Metallic = _Metallic;
            surface.Emission = _EmissionColor.rgb;
            surface.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
