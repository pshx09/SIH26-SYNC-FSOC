Shader "Hidden/FSOC/GaussianNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseSigma ("Gaussian Sigma", Float) = 0
        _TimeSeed ("Time Seed", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _NoiseSigma;
            float _TimeSeed;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Pseudo-random number generator
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            // Box-Muller transform for Gaussian noise
            float gaussianNoise(float2 uv)
            {
                float u1 = rand(uv + _TimeSeed);
                float u2 = rand(uv - _TimeSeed);
                
                // Avoid log(0)
                u1 = max(u1, 0.0001);
                
                float r = sqrt(-2.0 * log(u1));
                float theta = 2.0 * 3.14159265 * u2;
                
                // standard normal distribution (mean 0, variance 1)
                return r * cos(theta);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                if (_NoiseSigma > 0)
                {
                    // Calculate noise and scale by sigma (converted from 0-255 scale to 0-1)
                    float noise = gaussianNoise(i.uv) * (_NoiseSigma / 255.0);
                    
                    // Apply noise to RGB (sensor is monochrome, so applying to all equally)
                    col.rgb += float3(noise, noise, noise);
                    
                    // Clamp to valid pixel range
                    col.rgb = saturate(col.rgb);
                }
                
                return col;
            }
            ENDCG
        }
    }
}
