Shader "ARPG/FX/HealSprite"
{
    Properties
    {
        _MainTex ("Kanji", 2D) = "white" {}
        _Color ("Tint", Color) = (0.18, 0.92, 0.32, 1)
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
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            Name "Sprite"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

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

            // 白字黑底 PNG：亮度当透明。RGB 乘上遮罩，避免透明像素仍带高亮绿被 Bloom 糊开。
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float mask = max(max(tex.r, tex.g), tex.b) * tex.a;
                float a = _Color.a * mask;
                return fixed4(_Color.rgb * a, a);
            }
            ENDCG
        }
    }
}
