// PartnerHoodieSwap.shader
// The partner's sprite shader: SpriteBillboardDepthBias (so they stop sinking into walls they stand
// against — see that file for the depth trick, which is copied here verbatim) PLUS a three-key colour
// remap that recolours the hoodie without a single re-baked texture.
//
// The hoodie in partner_<who>{,_walk,_action}.png is exactly three tones — base / shadow / highlight —
// and nothing else in the art uses them (the nearest other colour is a comfortable distance away; the
// sheets import Uncompressed and Point-filtered, so a sampled texel is the authored value exactly).
// Remapping those three keys therefore repaints the garment and leaves skin, hair, trousers and the
// cream drawstrings alone. HoodieRecolor.cs drives the _From*/_To* pairs.
//
// COLOUR SPACE: the keys are declared as Vector, not Color, on purpose. Colour properties get a silent
// gamma->linear conversion in a linear project and Vectors do not, so HoodieRecolor converts once, in
// C#, into the same space the sampler returns and the comparison is exact. Defaults are negative so an
// undriven material matches nothing and draws the art untouched.
Shader "Sprites/PartnerHoodieSwap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DepthBias ("Camera Depth Bias (world units)", Float) = 0.8

        _FromBase   ("From: hoodie base",      Vector) = (-1,-1,-1,0)
        _FromShadow ("From: hoodie shadow",    Vector) = (-1,-1,-1,0)
        _FromHigh   ("From: hoodie highlight", Vector) = (-1,-1,-1,0)
        _ToBase     ("To: hoodie base",        Vector) = (-1,-1,-1,0)
        _ToShadow   ("To: hoodie shadow",      Vector) = (-1,-1,-1,0)
        _ToHigh     ("To: hoodie highlight",   Vector) = (-1,-1,-1,0)
        _KeyTolerance ("Key match tolerance",  Float)  = 0.004

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex SpriteVertBias
            #pragma fragment HoodieFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _DepthBias;
            float4 _FromBase, _FromShadow, _FromHigh;
            float4 _ToBase, _ToShadow, _ToHigh;
            float _KeyTolerance;

            // Bias ONLY the depth, never the screen position/size — identical to
            // SpriteBillboardDepthBias.shader, where the reasoning is written out in full.
            v2f SpriteVertBias(appdata_t IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID (IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 pos = UnityFlipSprite (IN.vertex, _Flip);

                float4 viewPos = mul(UNITY_MATRIX_MV, pos);
                float4 clip    = mul(UNITY_MATRIX_P, viewPos);
                float biasedVz = viewPos.z + _DepthBias;
                clip.z = UNITY_MATRIX_P._m20 * viewPos.x + UNITY_MATRIX_P._m21 * viewPos.y
                       + UNITY_MATRIX_P._m22 * biasedVz  + UNITY_MATRIX_P._m23 * viewPos.w;
                OUT.vertex = clip;

                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                return OUT;
            }

            bool KeyHit(float3 c, float3 key, float tol)
            {
                float3 d = c - key;
                return dot(d, d) < tol * tol;
            }

            fixed4 HoodieFrag(v2f IN) : SV_Target
            {
                // Remap on the RAW texel, before the vertex colour tint and before the premultiply —
                // that is the only place the authored colours still are what the palette table says.
                fixed4 tex = SampleSpriteTexture (IN.texcoord);
                float tol = _KeyTolerance;
                if      (KeyHit(tex.rgb, _FromBase.rgb,   tol)) tex.rgb = _ToBase.rgb;
                else if (KeyHit(tex.rgb, _FromShadow.rgb, tol)) tex.rgb = _ToShadow.rgb;
                else if (KeyHit(tex.rgb, _FromHigh.rgb,   tol)) tex.rgb = _ToHigh.rgb;

                fixed4 c = tex * IN.color;
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
