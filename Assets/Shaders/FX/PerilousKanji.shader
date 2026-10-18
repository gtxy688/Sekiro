// 从白字黑底纹理提取“危”字，扩展笔画并叠加红色发光。
Shader "ARPG/FX/PerilousKanji"
{
    Properties
    {
        _MainTex ("Kanji", 2D) = "white" {}
        _EdgeColor ("Edge", Color) = (0.48, 0.01, 0.01, 1)
        _CoreColor ("Core", Color) = (0.78, 0.08, 0.05, 1)
        _Cutoff ("Dark Crush", Range(0, 0.4)) = 0.08
        _Thickness ("Stroke Thickness", Range(0, 1)) = 0.4
        _Intensity ("Intensity", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            Name "Kanji"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _EdgeColor;
            fixed4 _CoreColor;
            float _Cutoff;
            float _Thickness;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float MaskAt(float2 uv)
            {
                fixed4 tex = tex2D(_MainTex, uv);
                // 以亮度取遮罩，避免不透明黑底也参与发光。
                return max(max(tex.r, tex.g), tex.b);
            }

            // 邻域取最大值加粗笔画，再按阈值去除黑底。
            fixed4 frag(v2f i) : SV_Target
            {
                float mask = MaskAt(i.uv);
                float2 stepUv = _MainTex_TexelSize.xy * (_Thickness * 8.0);
                if (stepUv.x > 0)
                {
                    mask = max(mask, MaskAt(i.uv + float2(stepUv.x, 0)));
                    mask = max(mask, MaskAt(i.uv - float2(stepUv.x, 0)));
                    mask = max(mask, MaskAt(i.uv + float2(0, stepUv.y)));
                    mask = max(mask, MaskAt(i.uv - float2(0, stepUv.y)));
                    mask = max(mask, MaskAt(i.uv + stepUv));
                    mask = max(mask, MaskAt(i.uv - stepUv));
                }
                mask = saturate((mask - _Cutoff) / max(1e-4, 1.0 - _Cutoff));
                float3 rgb = lerp(_EdgeColor.rgb, _CoreColor.rgb, mask) * mask * _Intensity;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
