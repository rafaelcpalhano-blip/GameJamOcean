Shader "GameJamOcean/Soft Interaction Circle"
{
    Properties
    {
        _BaseColor ("Glow Color", Color) = (0.5, 0.95, 1, 0.45)
        _PulseStrength ("Pulse Strength", Range(0, 0.4)) = 0.08
        _PulseSpeed ("Pulse Cycles Per Second", Range(0, 2)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+110" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _PulseStrength;
                float _PulseSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float alpha = 1.0 - smoothstep(0.05, 1.0, radius);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed * 6.283185) * _PulseStrength;
                return half4(_BaseColor.rgb, saturate(_BaseColor.a * alpha * pulse));
            }
            ENDHLSL
        }
    }
}
