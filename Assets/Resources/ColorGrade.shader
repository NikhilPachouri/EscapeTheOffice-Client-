// Camera post effect for the 3D view (EscapeOffice.ColorGrading): tilt-shift blur toward the
// top and bottom (the miniature-diorama look), bloom so emissive things glow, then saturation,
// contrast, warm highlights / cool shadows and a vignette.
// Lives in Resources so builds include it and Shader.Find sees it.
Shader "EscapeOffice/ColorGrade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    sampler2D _BlurTex;
    float4 _Tilt;        // x: strength, y: half-height of the sharp band (0..1)

    float4 _Threshold;   // x: threshold, y: soft knee, z: bloom intensity
    float4 _Grade;       // x: saturation, y: contrast, z: exposure, w: split-tone strength
    float4 _Warm;        // highlight tint
    float4 _Cool;        // shadow tint
    float4 _Vignette;    // x: strength, y: roundness start

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box4 (float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }

    // 0: keep only what is brighter than the threshold (soft knee), at half resolution.
    half4 fragPrefilter (v2f i) : SV_Target
    {
        half3 c = Box4(i.uv, 1);
        half b = max(c.r, max(c.g, c.b));
        half soft = clamp(b - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
        soft = soft * soft / (4 * _Threshold.y + 1e-4);
        half w = max(soft, b - _Threshold.x) / max(b, 1e-4);
        return half4(c * w, 1);
    }

    // 1: downsample. 2: upsample (blended additively onto the next level up).
    half4 fragDown (v2f i) : SV_Target { return half4(Box4(i.uv, 1), 1); }
    half4 fragUp (v2f i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }

    // 3: composite and grade.
    half4 fragGrade (v2f i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        half band = abs(i.uv.y - 0.5) * 2;
        c = lerp(c, tex2D(_BlurTex, i.uv).rgb, _Tilt.x * smoothstep(_Tilt.y, 1.0, band));
        c += tex2D(_BloomTex, i.uv).rgb * _Threshold.z;
        c *= _Grade.z;

        half luma = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(luma.xxx, c, _Grade.x);                    // saturation
        c = (c - 0.5) * _Grade.y + 0.5;                     // contrast
        half t = saturate(luma);
        c *= lerp(_Cool.rgb, _Warm.rgb, t * t * (3 - 2 * t)) * _Grade.w + (1 - _Grade.w); // split tone

        float2 d = (i.uv - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1);
        half v = 1 - _Vignette.x * smoothstep(_Vignette.y, 1.1, length(d) * 1.2);
        return half4(saturate(c * v), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragPrefilter
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragDown
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragUp
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert
               #pragma fragment fragGrade
               ENDCG }
    }
}
