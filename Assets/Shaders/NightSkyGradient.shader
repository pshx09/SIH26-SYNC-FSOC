Shader "Skybox/NightSkyGradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.01, 0.03, 0.07, 1) // #020713
        _MidColor ("Middle Color", Color) = (0.02, 0.04, 0.09, 1) // #040B18
        _BottomColor ("Horizon Color", Color) = (0.04, 0.10, 0.20, 1) // #0A1A32
        _GradientExponent ("Gradient Exponent", Float) = 2.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _MidColor;
            float4 _BottomColor;
            float _GradientExponent;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize view direction
                float3 d = normalize(i.texcoord);
                // Get vertical coordinate (-1 to 1)
                float y = d.y;
                
                // We only care about the upper hemisphere (y >= 0)
                // If y < 0, it's the ground (lower hemisphere)
                if (y < 0) {
                    return _BottomColor;
                }
                
                // Map y from [0, 1]
                float factor = pow(y, _GradientExponent);
                
                // Simple 3-color lerp
                if (factor > 0.5) {
                    float t = (factor - 0.5) * 2.0;
                    return lerp(_MidColor, _TopColor, t);
                } else {
                    float t = factor * 2.0;
                    return lerp(_BottomColor, _MidColor, t);
                }
            }
            ENDCG
        }
    }
}
