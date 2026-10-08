// SceneColorHalo 用的半透明色彩光暈：固定釘在場景座標上的 3D mesh（目前是碗的形狀，
// Assets/Chinese Exhibits/635.obj 的 mmGroup0），支援 HoloLens 的 Single Pass Instanced
// （不然只會出現在一隻眼睛）。放在 Resources 底下才會被打包進 build。
Shader "Custom/ColorHalo"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.5, 0.5, 0.25)
        _EdgeSoftness ("Edge Softness (view-angle fade)", Range(0.01, 1)) = 0.4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
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
            float _EdgeSoftness;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // 碗這種凹凸起伏的 mesh，表面上每一點到物體中心的距離不固定，沒辦法像平面薄膜
            // 一樣單純用「離中心多遠」淡出邊緣；改用視線跟法線的夾角（Fresnel）：正對鏡頭那
            // 一面（法線幾乎平行視線，facing≈1）最濃，愈轉向側面、愈接近輪廓邊緣（法線愈
            // 垂直視線，facing≈0）愈淡，不管 mesh 形狀長怎樣都能有一致的柔邊發光效果。
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float facing = saturate(dot(normalize(i.worldNormal), viewDir));
                fixed4 c = _Color;
                c.a *= smoothstep(0.0, max(_EdgeSoftness, 0.0001), facing);
                return c;
            }
            ENDCG
        }
    }
}
