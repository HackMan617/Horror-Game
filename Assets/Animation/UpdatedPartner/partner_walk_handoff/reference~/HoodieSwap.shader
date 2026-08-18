// HoodieSwap.shader — Sprites/Default + a 3-key colour remap (fragment). Attach the material to the
// partner's SpriteRenderer; HoodieRecolor drives the _To* properties.
Shader "Sprites/HoodieSwap" {
Properties {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _FromBase("",Color)=(1,0,0,1) _FromShadow("",Color)=(1,0,0,1) _FromHigh("",Color)=(1,0,0,1)
  _ToBase("",Color)=(1,0,0,1)   _ToShadow("",Color)=(1,0,0,1)   _ToHigh("",Color)=(1,0,0,1)
}
SubShader {
  Tags{"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass { CGPROGRAM
    #pragma vertex vert #pragma fragment frag
    #include "UnityCG.cginc"
    struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
    sampler2D _MainTex; float4 _MainTex_ST;
    fixed4 _FromBase,_FromShadow,_FromHigh,_ToBase,_ToShadow,_ToHigh;
    v2f vert(appdata_base v){ v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.texcoord,_MainTex); return o; }
    bool key(fixed3 a, fixed3 b){ return distance(a,b) < 0.02; }
    fixed4 frag(v2f i):SV_Target {
      fixed4 c = tex2D(_MainTex, i.uv);
      if (key(c.rgb,_FromBase.rgb))   c.rgb=_ToBase.rgb;
      else if (key(c.rgb,_FromShadow.rgb)) c.rgb=_ToShadow.rgb;
      else if (key(c.rgb,_FromHigh.rgb))   c.rgb=_ToHigh.rgb;
      return c;
    } ENDCG }
}}
