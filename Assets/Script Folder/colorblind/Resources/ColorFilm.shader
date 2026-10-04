// HeadLockedColorFilm 用的半透明色彩薄膜：不寫深度、永遠畫在最上層，支援 HoloLens 的
// Single Pass Instanced（不然只會出現在一隻眼睛）。放在 Resources 底下才會被打包進 build。
Shader "Custom/ColorFilm"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.5, 0.5, 0.25)
        _EdgeFade ("Edge Fade (fraction of view)", Range(0, 0.5)) = 0.1
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _EdgeFade;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            // HoloLens 能顯示虛擬畫面的範圍只有視野中間一塊長方形，薄膜到那裡會整齊斷掉、像一塊
            // 板子。中間整片濃淡一致，只有最外圈（_EdgeFade 那麼寬）往外淡出，讓邊界不那麼生硬。
            // （之前用圓形從中心淡出，戴起來像光圈，所以改成長方形邊緣。）
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)); // 到最近邊緣的距離
                fixed4 c = _Color;
                c.a *= smoothstep(0.0, max(_EdgeFade, 0.0001), edge);
                return c;
            }
            ENDCG
        }
    }
}
