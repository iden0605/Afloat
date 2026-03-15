// Custom/SpriteEffects
// Combined URP-compatible shader for 2D sprites supporting:
//   - Coloured outline drawn 1 pixel outside the sprite silhouette
//   - Noise-based dissolve-out (progress 0 → 1 burns the sprite away)
//
// Works like Sprites/Default for tint: sprite vertex colour * _Color * texture.
// All effects off by default (zero-cost when unused via UNITY_BRANCH).
//
// Add SpriteEffectsController.cs to the same GO — it creates a private material
// instance from this shader so other objects sharing the atlas are unaffected.

Shader "Custom/SpriteEffects"
{
    Properties
    {
        _MainTex           ("Sprite Texture", 2D) = "white" {}
        [PerRendererData]
        _Color             ("Tint", Color) = (1,1,1,1)

        // ── Outline ──────────────────────────────────────────────────────────
        _OutlineColor      ("Outline Color", Color) = (1,1,1,1)
        _OutlineThickness  ("Outline Thickness (texels)", Float) = 1.5
        _OutlineEnabled    ("Outline Enabled", Float) = 0

        // ── Dissolve ─────────────────────────────────────────────────────────
        _DissolveProgress  ("Dissolve Progress", Range(0,1)) = 0
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1,0.4,0,1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0,0.2)) = 0.05
        _DissolveScale     ("Dissolve Noise Scale", Float) = 8.0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "SpriteEffects"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;  // (1/w, 1/h, w, h) — auto-set by Unity
                float4 _Color;
                float4 _OutlineColor;
                float  _OutlineThickness;
                float  _OutlineEnabled;
                float  _DissolveProgress;
                float4 _DissolveEdgeColor;
                float  _DissolveEdgeWidth;
                float  _DissolveScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color;
                return OUT;
            }

            // ── Value noise (no texture lookup required) ──────────────────────
            float Hash(float2 p)
            {
                p  = frac(p * float2(443.897, 441.423));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(i),               Hash(i + float2(1,0)), u.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x),
                    u.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _Color;

                // ── Outline ───────────────────────────────────────────────────
                UNITY_BRANCH
                if (_OutlineEnabled > 0.5)
                {
                    float2 t = _MainTex_TexelSize.xy * _OutlineThickness;

                    // 8-directional neighbour alpha sum
                    float n =
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t.x,    0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t.x,    0)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(   0,  t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(   0, -t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t.x,  t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t.x,  t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2( t.x, -t.y)).a +
                        SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(-t.x, -t.y)).a;

                    // Transparent pixel with at least one opaque neighbour → outline pixel
                    if (col.a < 0.01 && n > 0.0)
                    {
                        col   = _OutlineColor;
                        col.a = saturate(n);
                    }
                }

                // ── Dissolve ──────────────────────────────────────────────────
                UNITY_BRANCH
                if (_DissolveProgress > 0.0 && col.a > 0.01)
                {
                    // Fractional Brownian Motion noise for organic edge
                    float2 uv = IN.uv * _DissolveScale;
                    float  n  = SmoothNoise(uv)               * 0.50
                              + SmoothNoise(uv * 2.1 + 7.30)  * 0.30
                              + SmoothNoise(uv * 4.2 + 3.70)  * 0.20;

                    // Discard pixels below the threshold (they have dissolved)
                    clip(n - _DissolveProgress);

                    // Glow at the burn edge
                    float edge = saturate((n - _DissolveProgress) / max(_DissolveEdgeWidth, 0.001));
                    col.rgb    = lerp(_DissolveEdgeColor.rgb, col.rgb, edge);
                    col.a     *= saturate(edge * 3.0); // sharp fade for crisp edge
                }

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
