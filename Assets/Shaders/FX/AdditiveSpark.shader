// 将火花纹理叠加到场景，供粒子与武器拖尾使用。
Shader "ARPG/FX/AdditiveSpark"
{
    Properties
    {
        _MainTex ("Spark", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Dark Crush", Range(0, 0.4)) = 0.1
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
        ZTest LEqual
        Lighting Off

        Pass
        {
            Name "Spark"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            // 压低遮罩暗部，避免加色混合时露出纹理边框。
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float lum = max(max(tex.r, tex.g), tex.b);
                float mask = max(lum, tex.a);
                mask = saturate((mask - _Cutoff) / max(1e-4, 1.0 - _Cutoff));
                float3 rgb = i.color.rgb * tex.rgb * mask * i.color.a;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
