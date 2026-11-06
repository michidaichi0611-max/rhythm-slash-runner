// Solid boss shards (owner round 10: the pieces looked paper-thin). Front and back faces show the boss picture; the
// side walls (uv2.x = 1) are filled with the boss's inside colour (vertex colour) but only where the picture is solid
// along that edge. Alpha-tested and depth-written, so the pieces sort correctly as real 3D chunks.
Shader "RhythmRunner/Shard"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 side : TEXCOORD1; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float side : TEXCOORD1; fixed4 color : COLOR; float light : TEXCOORD3; UNITY_FOG_COORDS(2) };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.side = v.side.x; o.color = v.color;
                // simple two-sided key light from the upper front: a tumbling piece catches and loses the light
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                o.light = 0.72 + 0.45 * abs(dot(n, normalize(float3(0.35, 0.8, -0.5))));
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                clip(t.a - 0.85); // only the solid body breaks into chunks; mist, glow and aura (semi-transparent) vanish
                fixed3 rgb = (i.side > 0.5 ? i.color.rgb : t.rgb * i.color.rgb) * i.light;
                fixed4 c = fixed4(rgb, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
