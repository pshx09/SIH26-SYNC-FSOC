Shader "Hidden/FSOC/AtmosphereEffects"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            int _AtmosphereMode;
            float _Intensity;
            float _Contrast;
            float _Brightness;
            float _HazeAmount;
            float _FogAmount;
            float _RainAmount;
            float _TimeSeed;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                
                // 0. Clear: Exact Hardware Pass-Through
                if (_AtmosphereMode == 0) return col;

                float sourceLuma = col.r; // FSOC pipeline operates on monochrome
                float finalLuma = sourceLuma;

                if (_AtmosphereMode == 1) // Haze
                {
                    // Plausible Haze: Mild optical veil transmission model
                    float appliedHaze = saturate(_HazeAmount * _Intensity * 0.5);
                    float hazeColor = 0.5; // Neutral gray
                    
                    float transmission = 1.0 - appliedHaze;
                    finalLuma = (sourceLuma * transmission) + (hazeColor * appliedHaze);
                    
                    finalLuma = (finalLuma - 0.5) * _Contrast + 0.5;
                    finalLuma = finalLuma * _Brightness;
                }
                else if (_AtmosphereMode == 2) // Fog
                {
                    // Plausible Fog: Strong optical veil transmission model
                    float appliedFog = saturate(_FogAmount * _Intensity);
                    float fogColor = 0.65;
                    
                    // The fog itself occludes the signal and scatters ambient light (fogColor)
                    float transmission = 1.0 - appliedFog;
                    finalLuma = (sourceLuma * transmission) + (fogColor * appliedFog);
                    
                    // Contrast and Brightness are applied cleanly ONCE
                    finalLuma = (finalLuma - 0.5) * _Contrast + 0.5;
                    finalLuma = finalLuma * _Brightness;
                }
                else if (_AtmosphereMode == 3) // Rain
                {
                    // Plausible Rain: Continuous, sparse, procedural vertical streaks
                    float appliedRain = saturate(_RainAmount * _Intensity);
                    
                    // Create continuous vertical bands that distort and fall rapidly
                    float rainX = i.uv.x * 400.0;
                    float rainY = i.uv.y * 15.0 + _TimeSeed * 25.0;
                    
                    // Intersecting sines create continuous sparse streaks without grid blocks
                    float streak = sin(rainX + sin(rainY) * 2.0) * sin(rainX * 0.7 - rainY * 0.5);
                    streak = streak * 0.5 + 0.5; // map to 0..1
                    
                    // Sharpen aggressively so streaks are very thin and sparse
                    streak = pow(streak, 15.0);
                    
                    // Rain acts purely as an optical occlusion (attenuation) without any additive scattering
                    float rainOcclusion = 1.0 - (streak * appliedRain * 0.4);
                    
                    // Apply environmental flicker (simulating storm lighting changes)
                    float flicker = 1.0 - (sin(_TimeSeed * 4.0) * 0.05 * _Intensity);
                    
                    finalLuma = finalLuma * rainOcclusion;
                    finalLuma = finalLuma * flicker;
                    
                    // Standard user overrides
                    finalLuma = (finalLuma - 0.5) * _Contrast + 0.5;
                    finalLuma = finalLuma * _Brightness;
                }
                else if (_AtmosphereMode == 4) // LowLight
                {
                    // Plausible LowLight: Multiplicative attenuation with peak signal preservation
                    float appliedDarkness = saturate(_Intensity);
                    
                    // Determine how "peak-like" the original pixel was to preserve optical beacons
                    // smoothstep ensures a continuous transition, preventing hard edges
                    float peakPreservation = smoothstep(0.6, 1.0, sourceLuma);
                    
                    // To prevent contrast compression from artificially dragging bright peaks below 
                    // detector thresholds, peaks resist contrast compression in dark conditions
                    float effectiveContrast = lerp(_Contrast, 1.0, peakPreservation * appliedDarkness);
                    float baseLuma = (sourceLuma - 0.5) * effectiveContrast + 0.5;

                    // Backgrounds are crushed heavily by darkness
                    float backgroundAttenuation = 1.0 - (appliedDarkness * 0.90);
                    
                    // Bright peaks are preserved significantly better (retaining nearly all their brightness)
                    float peakAttenuation = 1.0 - (appliedDarkness * 0.02);
                    
                    // Blend the attenuation factor based on the source luminance
                    float attenuation = lerp(backgroundAttenuation, peakAttenuation, peakPreservation);
                    
                    // Purely multiplicative attenuation (no additive energy can create false beacons)
                    finalLuma = baseLuma * attenuation * _Brightness;
                }

                // Strictly clamp to maintain physically plausible monochrome 0-1 range
                finalLuma = saturate(finalLuma);
                return float4(finalLuma, finalLuma, finalLuma, 1.0);
            }
            ENDCG
        }
    }
}
