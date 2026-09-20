Shader "DinoNet/TwilightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.05, 0.05, 0.22, 1)
        _Mid ("Mid Sky", Color) = (0.30, 0.16, 0.42, 1)
        _Horizon ("Horizon Glow", Color) = (1.0, 0.45, 0.35, 1)
        _Ground ("Below Horizon", Color) = (0.10, 0.06, 0.16, 1)
        _Haze ("Horizon Haze (match fog)", Color) = (0.42, 0.24, 0.40, 1)
        _SunDir ("Sun Direction (xyz)", Vector) = (0.5, 0.1, 0.8, 0)
        _SunGlow ("Sun Glow Strength", Range(0, 2)) = 0.9
        _MoonDir ("Moon Direction (xyz)", Vector) = (-0.4, 0.55, -0.6, 0)
        _StarScale ("Star Grid Scale", Range(20, 200)) = 90
        _StarDensity ("Star Density", Range(0, 0.6)) = 0.16
        _StarBrightness ("Star Brightness", Range(0, 6)) = 2.5
        _TwinkleSpeed ("Twinkle Speed", Range(0, 6)) = 2
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Mid, _Horizon, _Ground, _Haze;
            float4 _SunDir, _MoonDir;
            float _SunGlow, _StarScale, _StarDensity, _StarBrightness, _TwinkleSpeed;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                float3 col;
                if (h >= 0)
                {
                    float3 grad = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.22, h));
                    grad = lerp(grad, _Zenith.rgb, smoothstep(0.18, 0.85, h));
                    // At the horizon line the sky fades into the same haze colour as the fog.
                    col = lerp(_Haze.rgb, grad, smoothstep(0.0, 0.07, h));
                }
                else
                {
                    col = lerp(_Haze.rgb, _Ground.rgb, smoothstep(0.0, 0.3, -h));
                }

                float3 sunDir = normalize(_SunDir.xyz);
                float s = saturate(dot(d, sunDir));
                col += _Horizon.rgb * _SunGlow * pow(s, 6.0) * (1.0 - saturate(h * 1.5));

                // Twinkling stars, faded out near the horizon.
                float3 p = d * _StarScale;
                float3 cell = floor(p);
                float3 rnd = hash33(cell);
                float3 f = frac(p) - 0.5;
                float3 offset = (hash33(cell + 17.0) - 0.5) * 0.6;
                float dist = length(f - offset);
                float exists = step(1.0 - _StarDensity, rnd.x);
                float radius = lerp(0.05, 0.16, rnd.y);
                float star = exists * smoothstep(radius, 0.0, dist);
                float twinkle = 0.55 + 0.45 * sin(_Time.y * _TwinkleSpeed * (0.5 + rnd.y) + rnd.z * 6.2831);
                float3 starCol = lerp(float3(0.75, 0.85, 1.0), float3(1.0, 0.92, 0.7), rnd.z);
                col += starCol * star * twinkle * _StarBrightness * smoothstep(0.03, 0.28, h);

                // Soft glowing moon
                float3 moonDir = normalize(_MoonDir.xyz);
                float m = dot(d, moonDir);
                float disc = smoothstep(0.9993, 0.9997, m);
                float halo = pow(saturate(m), 220.0) * 0.6;
                col += float3(1.0, 0.95, 0.8) * (disc * 3.0 + halo);

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
