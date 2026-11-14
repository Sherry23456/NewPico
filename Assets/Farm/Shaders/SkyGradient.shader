Shader "Farm/SkyGradient"
{
    Properties
    {
        _Top("天顶色", Color) = (0.30, 0.52, 0.90, 1)
        _Horizon("地平线色", Color) = (0.78, 0.88, 0.98, 1)
        _NightBlend("夜混合", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Sphere" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Horizon;
            float _NightBlend;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(mul((float3x3)unity_ObjectToWorld, v.vertex.xyz).xyz);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = saturate(i.dir.y);
                fixed4 topN = fixed4(0.015, 0.030, 0.090, 1);
                fixed4 horN = fixed4(0.050, 0.070, 0.150, 1);
                fixed4 top = lerp(_Top, topN, _NightBlend);
                fixed4 hor = lerp(_Horizon, horN, _NightBlend);
                fixed4 col = lerp(hor, top, pow(h, 0.55));

                // 程序化星星（仅夜空高处）
                float3 d = normalize(i.dir);
                float3 cell = floor(d * 260.0);
                float rnd = frac(sin(dot(cell, float3(12.9898, 78.233, 45.164))) * 43758.5453);
                float star = step(0.9986, rnd) * saturate(h * 2.5);
                float twinkle = 0.65 + 0.35 * sin(_Time.y * 3.0 + rnd * 40.0);
                col += star * _NightBlend * twinkle * 0.9;

                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
