Shader "Custom/SimpleMotionBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurAmount ("Blur Amount", Range(0, 0.5)) = 0.0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BlurAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 offsets[4] = {
                    float2(-1, -1), float2(1, -1),
                    float2(-1,  1), float2(1,  1)
                };
                fixed4 col = tex2D(_MainTex, i.uv);
                float amount = _BlurAmount * 0.5;
                for (int k = 0; k < 4; k++)
                {
                    float2 uv = i.uv + offsets[k] * _MainTex_TexelSize.xy * amount;
                    col += tex2D(_MainTex, uv);
                }
                col /= 5.0;
                return col;
            }
            ENDCG
        }
    }
}