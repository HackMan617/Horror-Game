// SpriteBillboardDepthBias.shader
// A drop-in replacement for "Sprites/Default" that nudges the sprite TOWARD THE CAMERA in the depth
// buffer by _DepthBias world units (view-space), without moving it on screen. This lets a 2.5D
// billboard that geometrically intersects a nearby wall win the depth test against it (so it stops
// "sinking into" the wall), while anything clearly further in front of the sprite than the bias — the
// cabin seen from outside, a chimney block — still occludes it normally.
//
// Everything else matches Sprites/Default exactly (premultiplied blend, ZWrite Off, Cull Off, vertex
// colour, per-renderer tint/flip, pixel snap), so the look is unchanged. Renders under URP too, like
// the stock sprite shader this game already uses.
Shader "Sprites/BillboardDepthBias"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _DepthBias ("Camera Depth Bias (world units)", Float) = 0.8
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
            #pragma fragment SpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            float _DepthBias;

            v2f SpriteVertBias(appdata_t IN)
            {
                v2f OUT;

                UNITY_SETUP_INSTANCE_ID (IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4 pos = UnityFlipSprite (IN.vertex, _Flip);

                // Bias ONLY the depth, never the screen position/size. We project normally (correct
                // x, y, w — so the silhouette is pixel-identical to Sprites/Default), then recompute
                // just clip.z from a view depth pulled _DepthBias units toward the camera (view space
                // looks down -Z, so +Z is nearer). Using the real projection row keeps this correct on
                // reversed-Z platforms. Net effect: the sprite tests against walls as if it stood a bit
                // in front of where it is, so it stops sinking into a wall it's up against — while
                // anything more than _DepthBias in front of it still occludes it normally.
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
        ENDCG
        }
    }
}
