// A soft glow around a sprite's silhouette, drawn on its own renderer behind the sprite.
// For each pixel it looks at the sprite's alpha on rings around it: the closer the sprite, the
// stronger the glow. Unlit, so it reads the same in every light.
Shader "Farhad/SpriteHalo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HaloColor ("Halo Color", Color) = (1, 1, 1, 0.85)
        _Radius ("Radius (texels)", Float) = 28
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _HaloColor;
                float _Radius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color; // the renderer's color: its alpha fades the whole halo
                return output;
            }

            half Coverage(float2 uv, float distance)
            {
                const int Taps = 16;
                half alpha = 0;
                for (int i = 0; i < Taps; i++)
                {
                    float angle = i * (6.2831853 / Taps);
                    float2 offset = float2(cos(angle), sin(angle)) * _MainTex_TexelSize.xy * distance;
                    alpha = max(alpha, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset).a);
                }
                return alpha;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Near rings count fully, far rings fade: a soft edge rather than a hard outline.
                half glow = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                glow = max(glow, Coverage(input.uv, _Radius * 0.33));
                glow = max(glow, Coverage(input.uv, _Radius * 0.66) * 0.7);
                glow = max(glow, Coverage(input.uv, _Radius) * 0.35);
                return half4(_HaloColor.rgb, _HaloColor.a * glow * input.color.a);
            }
            ENDHLSL
        }
    }
}
