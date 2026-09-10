Shader "GameJamOcean/Boat Foam"
{
    Properties
    {
        _FoamTex ("Foam Pattern", 2D) = "white" {}
        _PatternStrength ("Pattern Strength", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
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
                half _PatternStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float edge = length(p) + 0.08 * sin(p.x * 14.0) * sin(p.y * 11.0);
                float softness = 1.0 - smoothstep(0.3, 0.95, edge);
                float flecks = 0.72 + 0.28 * sin(p.x * 19.0 + p.y * 7.0) * sin(p.y * 17.0);
                half pattern = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, input.uv).r;
                half texturedFoam = lerp(1.0h, smoothstep(0.18h, 0.82h, pattern), _PatternStrength);
                half3 foamColor = lerp(input.color.rgb, half3(1.0h, 1.0h, 1.0h), pattern * 0.58h);
                return half4(foamColor, input.color.a * softness * flecks * texturedFoam);
            }
            ENDHLSL
        }
    }
}
