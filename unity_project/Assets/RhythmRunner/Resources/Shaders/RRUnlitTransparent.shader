// Unlit textured, alpha-blended, optional fog (backdrop layers, shadows, glows on the 3D road).
Shader "RhythmRunner/UnlitTransparent"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); UNITY_TRANSFER_FOG(o, o.pos); return o; }
            fixed4 frag (v2f i) : SV_Target { fixed4 c = tex2D(_MainTex, i.uv) * _Color; UNITY_APPLY_FOG_COLOR(i.fogCoord, c.rgb, unity_FogColor); return c; }
            ENDCG
        }
    }
}
