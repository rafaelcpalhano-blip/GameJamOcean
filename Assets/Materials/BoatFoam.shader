Shader "GameJamOcean/Boat Foam"
{
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
                return half4(input.color.rgb, input.color.a * softness * flecks);
            }
            ENDHLSL
        }
    }
}
