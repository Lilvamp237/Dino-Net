Shader "DinoNet/PrehistoricSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.25, 0.45, 0.85, 1)
        _Mid ("Mid Sky", Color) = (0.55, 0.72, 0.95, 1)
        _Horizon ("Horizon", Color) = (0.95, 0.85, 0.7, 1)
        _Haze ("Horizon Haze (match fog)", Color) = (0.8, 0.85, 0.9, 1)
        _Ground ("Below Horizon", Color) = (0.3, 0.32, 0.28, 1)
        _SunDir ("Sun Direction (xyz)", Vector) = (0.4, 0.6, 0.6, 0)
        _SunColor ("Sun Colour", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Size", Range(0.002, 0.08)) = 0.02
        _CloudColor ("Cloud Colour", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade", Color) = (0.7, 0.75, 0.85, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.45
        _CloudSpeed ("Cloud Speed", Range(0, 0.1)) = 0.012
        _SmokeAmount ("Volcano Smoke", Range(0, 1)) = 0.8
        _SmokeAzimuth ("Volcano Direction (radians)", Range(-3.14159, 3.14159)) = 1.2
        _SmokeColor ("Smoke Colour", Color) = (0.28, 0.24, 0.24, 1)
        _BirdAmount ("Pterodactyls", Range(0, 1)) = 0.8
        _StarStrength ("Stars", Range(0, 4)) = 0
        _MoonDir ("Moon Direction (xyz)", Vector) = (-0.4, 0.55, -0.6, 0)
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

            fixed4 _Zenith, _Mid, _Horizon, _Haze, _Ground, _SunColor, _CloudColor, _CloudShade, _SmokeColor;
            float4 _SunDir, _MoonDir;
            float _SunSize, _CloudCover, _CloudSpeed, _SmokeAmount, _SmokeAzimuth, _BirdAmount, _StarStrength;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p = p * 2.03 + 11.7;
                    a *= 0.5;
                }
                return v;
            }

            float wrapAngle(float a)
            {
                return a - 6.2831853 * floor((a + 3.1415926) / 6.2831853);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float az = atan2(d.z, d.x);

                float3 col;
                if (h >= 0)
                {
                    float3 grad = lerp(_Horizon.rgb, _Mid.rgb, smoothstep(0.0, 0.25, h));
                    grad = lerp(grad, _Zenith.rgb, smoothstep(0.2, 0.9, h));
                    col = lerp(_Haze.rgb, grad, smoothstep(0.0, 0.06, h));
                }
                else
                {
                    col = lerp(_Haze.rgb, _Ground.rgb, smoothstep(0.0, 0.3, -h));
                }

                // Sun disc and warm glow around it.
                float3 sunDir = normalize(_SunDir.xyz);
                float s = saturate(dot(d, sunDir));
                col += _SunColor.rgb * (smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, s) * 3.0 + pow(s, 24.0) * 0.5 + pow(s, 6.0) * 0.18);

                // Stars and moon for the night levels.
                if (_StarStrength > 0.001)
                {
                    float3 p = d * 90.0;
                    float3 cell = floor(p);
                    float3 rnd = hash33(cell);
                    float3 f = frac(p) - 0.5;
                    float3 offset = (hash33(cell + 17.0) - 0.5) * 0.6;
                    float star = step(0.84, rnd.x) * smoothstep(lerp(0.05, 0.15, rnd.y), 0.0, length(f - offset));
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * 2.0 * (0.5 + rnd.y) + rnd.z * 6.28);
                    col += float3(0.85, 0.9, 1.0) * star * twinkle * _StarStrength * smoothstep(0.03, 0.28, h);

                    float3 moonDir = normalize(_MoonDir.xyz);
                    float m = dot(d, moonDir);
                    col += float3(1.0, 0.95, 0.8) * (smoothstep(0.9992, 0.9996, m) * 2.5 + pow(saturate(m), 200.0) * 0.5) * _StarStrength * 0.5;
                }

                if (h > 0.0)
                {
                    // Drifting clouds, projected onto a flat layer overhead.
                    float2 uv = d.xz / (h + 0.28) * 1.6 + float2(_Time.y * _CloudSpeed, _Time.y * _CloudSpeed * 0.4);
                    float c = fbm(uv);
                    float density = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.25, c) * smoothstep(0.0, 0.18, h);
                    float shade = saturate(fbm(uv + sunDir.xz * 0.35) - c + 0.55);
                    float3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, shade);
                    col = lerp(col, cloud, density * 0.85);

                    // Volcano smoke plume: a dark column that widens as it climbs.
                    float da = wrapAngle(az - _SmokeAzimuth);
                    float y = h;
                    float width = lerp(0.03, 0.2, saturate(y / 0.5)) + (fbm(float2(da * 9.0, y * 6.0 + _Time.y * 0.03)) - 0.5) * 0.06;
                    float column = smoothstep(width, width * 0.55, abs(da)) * smoothstep(0.62, 0.35, y);
                    float puffs = 0.6 + 0.4 * fbm(float2(da * 14.0, y * 10.0 - _Time.y * 0.05));
                    col = lerp(col, _SmokeColor.rgb * puffs, saturate(column) * _SmokeAmount);
                    float glow = smoothstep(0.05, 0.0, y) * smoothstep(0.05, 0.0, abs(da)) * _SmokeAmount;
                    col += float3(1.0, 0.4, 0.1) * glow * 0.9;

                    // A few pterodactyl silhouettes crossing the sky.
                    float birds = 0.0;
                    for (int b = 0; b < 5; b++)
                    {
                        float phase = frac(_Time.y * (0.004 + b * 0.0007) + b * 0.21) * 6.2831853;
                        float bAz = phase;
                        float bY = 0.16 + 0.07 * b + 0.015 * sin(_Time.y * 0.5 + b);
                        float2 q = float2(wrapAngle(az - bAz) * 40.0, (y - bY) * 40.0);
                        float flap = 0.35 + 0.35 * sin(_Time.y * 4.0 + b * 1.7);
                        float wing = abs(q.x) * (0.5 + flap) - q.y;
                        float shape = smoothstep(0.35, 0.0, abs(wing)) * smoothstep(1.1, 0.7, abs(q.x)) * smoothstep(0.5, 0.0, abs(q.y + 0.4 * abs(q.x)));
                        birds = max(birds, shape);
                    }
                    col = lerp(col, float3(0.08, 0.07, 0.09), saturate(birds) * _BirdAmount);
                }

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
