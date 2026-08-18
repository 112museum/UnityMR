Shader "Custom/NewSurfaceShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Factor("Color Factor", Range(0, 2)) = 2
        _RedMultiplier("Red Multiplier", Range(0, 3)) = 2.0
        _GreenMultiplier("Green Multiplier", Range(0, 3)) = 2.0
        _BlueMultiplier("Blue Multiplier", Range(0, 3)) = 2.0
        _Rect1("_Rect1", Vector) = (0, 0, 0, 0)
        _Rect2("_Rect2", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Opaque" }

        CGPROGRAM
        // 改用 surface shader + Lambert，讓這個材質跟場景其他吃光照的物件一樣會有明暗變化，
        // 不再是原本 unlit 的 Pass（那個版本不管場景燈光都固定同一個亮度，才會顯得特別亮）。
        // Single Pass Instanced 立體渲染（HoloLens）、GPU instancing 都是 surface shader
        // compiler 自動處理，不需要像原本手動 Pass 那樣自己寫 UNITY_VERTEX_OUTPUT_STEREO 等巨集。
        #pragma surface surf Lambert
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        fixed4 _Color;
        float _RedMultiplier;
        float _GreenMultiplier;
        float _BlueMultiplier;
        float4 _Rect1;
        float4 _Rect2;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 color = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // texcoord 0~1，乘上假想的 640x480 畫面尺寸後跟 _Rect 比對，邏輯跟原本 unlit 版本一致
            if ((IN.uv_MainTex.x * 640) > _Rect1.x && (IN.uv_MainTex.x * 640) < _Rect1.z &&
                (IN.uv_MainTex.y * 480) > _Rect1.y && (IN.uv_MainTex.y * 480) < _Rect1.w)
            {
                color.r *= _RedMultiplier;
                color.g *= _GreenMultiplier;
                color.b *= _BlueMultiplier;
            }

            if ((IN.uv_MainTex.x * 640) > _Rect2.x && (IN.uv_MainTex.x * 640) < _Rect2.z &&
                (IN.uv_MainTex.y * 480) > _Rect2.y && (IN.uv_MainTex.y * 480) < _Rect2.w)
            {
                color.r *= _RedMultiplier;
                color.g *= _GreenMultiplier;
                color.b *= _BlueMultiplier;
            }

            // 套完濾鏡倍率的顏色交給 Albedo，Lambert 燈光模型才會拿它去乘 NdotL、光源顏色、
            // ambient/light probe，物件才會有明暗，而不是原本固定輸出同一個亮度。
            o.Albedo = color.rgb;
            o.Alpha = color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
