// Cracks over the boss sprite (owner round 8: its HP shows on the boss itself). Drawn by a SpriteRenderer that shares the
// boss's sprite: only where the sprite is solid, only the crack lines whose order is below _Amount. Dark cores with a
// hot rim, so it looks about to burst near the end.
Shader "RhythmRunner/BossCracks"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        _CrackTex ("Cracks (R line, G order)", 2D) = "black" {}
        _Amount ("Amount", Range(0, 1)) = 0
        _CrackColor ("Core", Color) = (0.12, 0.03, 0.06, 1)
        _RimColor ("Rim", Color) = (1, 0.88, 0.62, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _CrackTex; float _Amount; fixed4 _CrackColor, _RimColor;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed solid = step(0.6, tex2D(_MainTex, i.uv).a);
                fixed2 c = tex2D(_CrackTex, i.uv).rg;
                fixed shown = saturate((_Amount - c.g) * 25.0);
                fixed crack = smoothstep(0.05, 0.45, c.r) * shown * solid;
                fixed3 col = lerp(_RimColor.rgb, _CrackColor.rgb, smoothstep(0.45, 0.85, c.r));
                return fixed4(col, crack * i.color.a);
            }
            ENDCG
        }
    }
}
