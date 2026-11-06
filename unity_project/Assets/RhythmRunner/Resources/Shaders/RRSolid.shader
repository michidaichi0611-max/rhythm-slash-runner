// Solid lit objects in the 3D scene (owner round 12: the long objects looked like paper craft "pasted" on the road).
// Texture × vertex colour, a soft key light from the upper front, a small highlight and a rim, tinted by the stage's
// light, and the same linear fog as the road and walls — so they sit in the world instead of on top of it.
Shader "RhythmRunner/Solid"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _LightTint ("Stage light", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            Cull Off ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _LightTint;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wpos : TEXCOORD2; fixed4 color : COLOR; UNITY_FOG_COORDS(3) };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed3 albedo = tex2D(_MainTex, i.uv).rgb * i.color.rgb;
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                float3 l = normalize(float3(0.3, 0.85, -0.45));
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float diff = saturate(dot(n, l));
                float spec = pow(saturate(dot(n, normalize(l + v))), 28) * 0.22;
                float rim = pow(1 - saturate(dot(n, v)), 3) * 0.16;
                // stronger key light, and the underside falls into shade (ambient occlusion from the road): round things read round
                float occl = lerp(0.86, 1.0, saturate(n.y * 0.5 + 0.5));
                // round 17 (owner: "暗くて見にくい"): brighter ambient, so the shaded side keeps its colour
                fixed3 c = albedo * (0.8 + 0.55 * diff) * occl * _LightTint.rgb + spec + rim * _LightTint.rgb;
                fixed4 col = fixed4(c, 1);
                // half-strength haze: they sit in the same air as the road, but keep their colours readable
                fixed4 fogged = col;
                UNITY_APPLY_FOG(i.fogCoord, fogged);
                col.rgb = lerp(col.rgb, fogged.rgb, 0.18);
                return col;
            }
            ENDCG
        }
    }
}
