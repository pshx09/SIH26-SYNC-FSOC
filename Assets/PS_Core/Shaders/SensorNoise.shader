Shader "Hidden/FSOC/SensorNoise"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NoiseType ("Noise Type", Int) = 0 // 0=None, 1=Gaussian, 2=SaltPepper, 3=Poisson
        _NoiseStrength ("Noise Strength", Float) = 0
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
            int _NoiseType;
            float _NoiseStrength;
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

            // Standard Normal Gaussian noise N(0,1)
            float getStandardGaussian(float2 uv)
            {
                float u1 = max(rand(uv + _TimeSeed), 0.0001);
                float u2 = rand(uv - _TimeSeed);
                float r = sqrt(-2.0 * log(u1));
                float theta = 2.0 * 3.14159265 * u2;
                return r * cos(theta);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                if (_NoiseType == 1) // Gaussian
                {
                    float noise = getStandardGaussian(i.uv) * (_NoiseStrength / 255.0);
                    col.rgb += float3(noise, noise, noise);
                    col.rgb = saturate(col.rgb);
                }
                else if (_NoiseType == 2) // Salt & Pepper
                {
                    float r = rand(i.uv + _TimeSeed + 0.123);
                    // _NoiseStrength is a probability 0 to 1
                    if (r < _NoiseStrength * 0.5)
                    {
                        col.rgb = float3(0, 0, 0); // Pepper
                    }
                    else if (r < _NoiseStrength)
                    {
                        col.rgb = float3(1, 1, 1); // Salt
                    }
                }
                else if (_NoiseType == 3) // Poisson (Approximated)
                {
                    // Poisson variance mathematically scales with the square root of the photon count.
                    // For a normalized intensity [0,1], we approximate shot noise standard deviation
                    // using a Gaussian distribution, heavily scaled down so the parameter is usable.
                    float n = getStandardGaussian(i.uv);
                    
                    // Clamp to a tiny value so pure black (0) doesn't produce NaN/Zero noise,
                    // but remains extremely close to 0 to prevent background blowout.
                    float intensity = max(col.r, 0.0001);
                    
                    // Base Poisson scaling:
                    // We map _NoiseStrength (0-10) to a manageable normalized variance.
                    // Strength 1 creates ~0.02 standard deviation at max intensity.
                    // A 0.1 background intensity only receives sqrt(0.1) * 0.02 = 0.006 standard deviation!
                    float effectiveSigma = _NoiseStrength * 0.02; 
                    
                    float poissonNoise = sqrt(intensity) * effectiveSigma * n;
                    
                    col.rgb += float3(poissonNoise, poissonNoise, poissonNoise);
                    col.rgb = saturate(col.rgb);
                }
                
                return col;
            }
            ENDCG
        }
    }
}
