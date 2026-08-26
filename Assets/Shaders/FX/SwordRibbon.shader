Shader "ARPG/FX/SwordRibbon"
{
    Properties
    {
        _MainTex ("Streak", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _SoftEdge ("Soft Edge", Range(0, 0.45)) = 0.16
        _Streak ("Streak Mix", Range(0, 1)) = 0.35
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
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Lighting Off

        Pass
        {
            Name "Ribbon"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _SoftEdge;
            float _Streak;

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
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 tex = tex2D(_MainTex, uv);
                float lum = max(max(tex.r, tex.g), tex.b);

                // uv.x = 圆弧进度，uv.y = 半径；纤维沿径向，内外沿是圆不是折线。
                float fiber = 0.62 + 0.38 * saturate(0.5 + 0.5 * sin(uv.y * 36.0));
                fiber *= 0.82 + 0.18 * saturate(0.5 + 0.5 * sin(uv.y * 88.0 + uv.x * 7.0));
                fiber = lerp(fiber, fiber * lerp(1.0, lum, 0.65), _Streak);

                float edge = _SoftEdge;
                float vFade = smoothstep(0.0, edge, uv.y) * smoothstep(1.0, 1.0 - edge, uv.y);
                float uFade = smoothstep(0.0, 0.18, uv.x);

                float3 rgb = _Color.rgb * i.color.rgb;
                float a = _Color.a * i.color.a * vFade * uFade * fiber * 0.88;
                return float4(rgb, a);
            }
            ENDCG
        }
    }
}
