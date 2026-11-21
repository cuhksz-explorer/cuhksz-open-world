Shader "Custom/MinimapCircleMask" {
    Properties {
        _MainTex ("Base Texture", 2D) = "white" {}  // 小地图原始纹理
        _Radius ("Circle Radius (0-0.5)", Range(0.1, 0.5)) = 0.5  // 圆形半径（0.5为完整圆形）
        _Feather ("Edge Feather (0-0.1)", Range(0.001, 0.1)) = 0.02  // 边缘羽化程度
    }

    SubShader {
        Tags { 
            "Queue" = "Transparent"  // 透明队列，确保在不透明物体后渲染
            "RenderType" = "Transparent" 
            "IgnoreProjector" = "True"  // 忽略投影器影响
        }

        // 启用Alpha混合（透明效果）
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off  // 关闭背面剔除（UI可能双面显示）
        ZWrite Off  // 关闭深度写入（避免遮挡其他UI）

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"  // 引入UI相关函数

            // 输入顶点数据结构（Unity UI标准结构）
            struct appdata {
                float4 vertex : POSITION;  // 顶点位置
                float2 uv : TEXCOORD0;     // 纹理坐标
                float4 color : COLOR;      // 顶点颜色（UI可能用到）
            };

            // 顶点到片段的传递结构
            struct v2f {
                float4 pos : SV_POSITION;  // 裁剪空间位置
                float2 uv : TEXCOORD0;     // 纹理坐标
                float4 color : COLOR;      // 颜色传递
            };

            // 外部参数（与Properties对应）
            sampler2D _MainTex;
            float4 _MainTex_ST;  // 纹理缩放偏移
            float _Radius;
            float _Feather;
            float4 _ClipRect;  // UI裁剪矩形（来自UnityUI）

            // 顶点着色器
            v2f vert (appdata v) {
                v2f o;
                // 计算裁剪空间位置
                o.pos = UnityObjectToClipPos(v.vertex);
                // 计算纹理坐标（支持缩放偏移）
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // 传递颜色
                o.color = v.color;
                return o;
            }

            // 片段着色器（核心：圆形裁剪）
            fixed4 frag (v2f i) : SV_Target {
                // 先应用UI裁剪（避免超出UI范围的显示）
                clip(UnityGet2DClipping(i.pos.xy, _ClipRect));

                // 计算当前像素到纹理中心的距离（uv坐标中心为(0.5,0.5)）
                float2 centerUV = float2(0.5, 0.5);  // 纹理中心
                float2 uvOffset = i.uv - centerUV;   // 当前像素与中心的偏移
                float distanceToCenter = length(uvOffset);  // 距离（半径方向）

                // 计算圆形遮罩的透明度：
                // - 距离 < 半径 - 羽化 → 完全不透明（alpha=1）
                // - 距离 > 半径 + 羽化 → 完全透明（alpha=0）
                // - 中间区域 → 平滑过渡（抗锯齿）
                float alpha = smoothstep(
                    _Radius + _Feather,  // 结束透明的边界
                    _Radius - _Feather,  // 开始不透明的边界
                    distanceToCenter     // 当前像素距离
                );

                // 采样原始纹理并应用透明度和顶点颜色
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.a *= alpha;  // 只修改透明度通道

                return col;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"  // 降级Shader（避免编译失败）
}