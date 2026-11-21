Shader "Custom/CelestialBody"
{
    Properties
    {
        [Toggle] _IsMoon ("Moon Mode", Float) = 0
        _MainTex ("Moon Surface", 2D) = "white" {}
        _CoreColor ("Core Color", Color) = (1, 0.95, 0.82, 1)
        _EdgeColor ("Edge / Limb Color", Color) = (1, 0.55, 0.12, 1)
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 3
        _CoronaSize ("Corona Size", Range(0.5, 3)) = 1.35
        _CoronaPower ("Corona Falloff", Range(0.5, 8)) = 2.2
        _Granulation ("Solar Granulation", Range(0, 0.35)) = 0.12
        _MoonTint ("Moon Tint", Color) = (0.82, 0.84, 0.9, 1)
        _MoonShadowTint ("Moon Shadow Tint", Color) = (0.08, 0.1, 0.16, 1)
        _MoonNormalStrength ("Moon Relief", Range(0, 2)) = 0.85
        _RimPower ("Rim Power", Range(0.5, 6)) = 2.5
    }

    SubShader
    {
        Tags { "Queue"="Background+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _CoreColor;
            fixed4 _EdgeColor;
            fixed4 _MoonTint;
            fixed4 _MoonShadowTint;
            float _EmissionStrength;
            float _CoronaSize;
            float _CoronaPower;
            float _Granulation;
            float _MoonNormalStrength;
            float _RimPower;
            float _IsMoon;

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise2(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    v += a * noise2(p);
                    p *= 2.1;
                    a *= 0.5;
                }
                return v;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.worldPos);
                return o;
            }

            fixed4 fragSun(v2f i)
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float ndv = saturate(dot(n, v));

                float2 uv = i.uv * 6.0;
                float gran = fbm(uv * 3.0) * _Granulation;
                float limb = pow(1.0 - ndv, 1.35);
                float core = pow(ndv, 0.35);

                fixed3 body = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
                body += gran * _CoreColor.rgb;
                body *= lerp(0.75, 1.0, core);

                float corona = pow(1.0 - ndv, _CoronaPower) * _CoronaSize;
                float halo = pow(1.0 - ndv, 1.1) * 0.35;
                fixed3 col = body * (_EmissionStrength * 0.35);
                col += _CoreColor.rgb * corona * _EmissionStrength * 0.55;
                col += _EdgeColor.rgb * halo * _EmissionStrength * 0.25;

                float alpha = saturate(corona * 0.65 + core * 0.95 + halo);
                return fixed4(col, alpha);
            }

            fixed4 fragMoon(v2f i)
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float ndv = saturate(dot(n, v));

                float2 uv = i.uv;
                fixed3 albedo = tex2D(_MainTex, uv).rgb;
                float proc = fbm(uv * 5.0);
                float craters = 1.0 - smoothstep(0.02, 0.0, abs(fbm(uv * 14.0 + proc) - 0.62));
                float mare = 0.5 + 0.12 * sin(uv.x * 9.0 + uv.y * 6.0);
                fixed3 procedural = fixed3(mare, mare, mare * 1.02) * (0.85 - craters * 0.35);
                if (dot(albedo, albedo) < 0.01 || distance(albedo, fixed3(1,1,1)) < 0.05)
                    albedo = procedural;
                else
                    albedo = lerp(procedural, albedo, 0.85);

                float2 dUv = 1.0 / 512.0;
                float hC = tex2D(_MainTex, uv).r;
                float hR = tex2D(_MainTex, uv + float2(dUv.x, 0)).r;
                float hU = tex2D(_MainTex, uv + float2(0, dUv.y)).r;
                float3 bumpN = normalize(float3((hC - hR) * _MoonNormalStrength, (hC - hU) * _MoonNormalStrength, 0.35));
                float3 litN = normalize(n + bumpN * 0.35);

                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float diff = saturate(dot(litN, lightDir));
                float wrap = saturate((dot(litN, lightDir) + 0.25) / 1.25);

                fixed3 moonCol = albedo * _MoonTint.rgb;
                fixed3 lit = moonCol * (0.25 + diff * 0.95 + wrap * 0.15);
                fixed3 shadow = _MoonShadowTint.rgb * (0.35 + albedo * 0.25);
                fixed3 body = lerp(shadow, lit, diff * 0.92 + wrap * 0.08);

                float rim = pow(1.0 - ndv, _RimPower) * 0.22;
                body += _MoonTint.rgb * rim;

                float corona = pow(1.0 - ndv, 3.5) * 0.18;
                body += _MoonTint.rgb * corona;

                float alpha = saturate(ndv * 0.85 + rim + corona);
                return fixed4(body * _EmissionStrength * 0.45, alpha);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_IsMoon > 0.5)
                    return fragMoon(i);
                return fragSun(i);
            }

            ENDCG
        }
    }

    FallBack Off
}
