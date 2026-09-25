// Darkens everything outside the player's vision radius (and outside fire light).
// Lives in Resources so it is always included in builds and found by Shader.Find.
// With the 3D camera the mask is a quad hovering over the level; each pixel is projected
// along the view ray onto the floor (z = 0) so the radius is measured on the ground.
Shader "EscapeOffice/VisionMask"
{
    Properties
    {
        _Color ("Darkness", Color) = (0, 0, 0, 0.97)
        [HideInInspector] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Center;      // xy: player, z: radius, w: soft edge
            float4 _Lights[16];  // xy: position, z: radius, w: soft edge
            int _LightCount;
            float _Project;      // 1: perspective camera, project onto the floor plane

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hole (float2 p, float4 c)
            {
                return 1.0 - smoothstep(c.z - c.w, c.z, distance(p, c.xy));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.world.xy;
                if (_Project > 0.5)
                {
                    float3 ray = i.world - _WorldSpaceCameraPos;
                    p = _WorldSpaceCameraPos.xy + ray.xy * (-_WorldSpaceCameraPos.z / ray.z);
                }
                float vis = hole(p, _Center);
                for (int k = 0; k < 16; k++)
                {
                    if (k >= _LightCount) break;
                    vis = max(vis, hole(p, _Lights[k]) * 0.75);
                }
                return fixed4(_Color.rgb, _Color.a * (1.0 - vis));
            }
            ENDCG
        }
    }
}
