Shader "GameJamOcean/Boat Contact Foam"
{
    Properties
    {
        _FoamTex ("Foam Pattern", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 1
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

            TEXTURE2D(_FoamTex);
            SAMPLER(sampler_FoamTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _FoamTex_ST;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FoamTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half pattern = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, input.uv).r;
                half detail = lerp(0.72h, 1.0h, smoothstep(0.2h, 0.82h, pattern));
                half3 texturedColor = lerp(input.color.rgb, half3(1.0h, 1.0h, 1.0h), pattern * 0.32h);
                return half4(texturedColor, input.color.a * detail * _Opacity);
            }
            ENDHLSL
        }
    }
}
