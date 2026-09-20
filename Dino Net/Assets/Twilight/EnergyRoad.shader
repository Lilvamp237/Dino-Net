Shader "DinoNet/EnergyRoad"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.3, 0.95, 1.0, 1)
        _Intensity ("Intensity", Float) = 0
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseCount ("Pulses Per Unit", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _PulseSpeed;
                float _PulseCount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float pulse = 0.65 + 0.35 * sin(i.uv.x * _PulseCount * 6.2831 - _Time.y * _PulseSpeed);
                float edge = saturate(1.0 - abs(i.uv.y * 2.0 - 1.0));
                edge = edge * edge;
                half3 col = _Color.rgb * _Intensity * pulse * edge;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
