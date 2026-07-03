Shader "Regolith/TransparentDirtMask"
{
    Properties
    {
        _MainTex ("Dirt Texture", 2D) = "white" {}
        _MaskTex ("Clean Mask", 2D) = "white" {}
        _Color ("Tint", Color) = (0.62, 0.64, 0.64, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TransparentDirt"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Opacity;

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 dirt = tex2D(_MainTex, i.uv);
                fixed mask = tex2D(_MaskTex, i.uv).r;

                fixed4 color;
                color.rgb = dirt.rgb * _Color.rgb;
                color.a = dirt.a * mask * _Color.a * _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
