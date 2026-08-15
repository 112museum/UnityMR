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
    

        Tags { "Queue"="Transparent" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 texcoord : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Factor;
            float _RedMultiplier;
            float _GreenMultiplier;
            float _BlueMultiplier;
            float4 _Rect1;
            float4 _Rect2;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            half4 frag(v2f i) : COLOR
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 color = tex2D(_MainTex, i.texcoord) * _Color;

                //texcoord.x ����0~1�����A��*�ù����e
                if ((i.texcoord.x * 640) > _Rect1.x && (i.texcoord.x * 640) < _Rect1.z &&
                    (i.texcoord.y * 480) > _Rect1.y && (i.texcoord.y * 480) < _Rect1.w)
                {
                    color.r *= _RedMultiplier;
                    color.g *= _GreenMultiplier;
                    color.b *= _BlueMultiplier;
                }

                if ((i.texcoord.x * 640) > _Rect2.x && (i.texcoord.x * 640) < _Rect2.z &&
                    (i.texcoord.y * 480) > _Rect2.y && (i.texcoord.y * 480) < _Rect2.w)
                {
                    color.r *= _RedMultiplier;
                    color.g *= _GreenMultiplier;
                    color.b *= _BlueMultiplier;
                }
                return color;
            }
            ENDCG
         }
    }
    //FallBack "Diffuse"
}
