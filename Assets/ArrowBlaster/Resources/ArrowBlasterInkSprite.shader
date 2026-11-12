// Arrow Blaster · Tinta para sprites con textura (Kenney)
// Convierte un sprite de colores en tinta japonesa:
//  - las líneas oscuras pasan a tinta negra,
//  - los rellenos pasan a aguada gris con pincel seco (o conservan su color si _KeepColor = 1 y son muy saturados),
//  - los blancos desaparecen (se ve el papel),
//  - el contorno tiembla un poco, como hecho a mano.
Shader "ArrowBlaster/InkSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Seed ("Semilla (por objeto)", Float) = 0
        _KeepColor ("Conservar color saturado", Range(0, 1)) = 0
        _Wobble ("Temblor del contorno (UV)", Float) = 0.006
        _Wash ("Intensidad de la aguada", Range(0, 1)) = 0.45
        _Dry ("Pincel seco en la aguada", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Seed;
                float _KeepColor;
                float _Wobble;
                float _Wash;
                float _Dry;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 seed = float2(_Seed * 13.7, _Seed * 7.3);
                // Contorno tembloroso
                float2 w = float2(ValueNoise(IN.uv * 9.0 + seed), ValueNoise(IN.uv * 9.0 + seed + 21.0)) - 0.5;
                float2 uv = IN.uv + w * _Wobble;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float maxc = max(c.r, max(c.g, c.b));
                float minc = min(c.r, min(c.g, c.b));
                float sat = maxc > 0.001 ? (maxc - minc) / maxc : 0.0;
                float ink = 1.0 - lum;

                // Líneas: tinta plena. Rellenos: aguada con vetas de pincel seco. Blancos: papel.
                float lineInk = smoothstep(0.62, 0.72, ink);
                float fill = smoothstep(0.1, 0.2, ink) * (1.0 - lineInk);
                float streak = ValueNoise(float2(IN.uv.x * 3.0, IN.uv.y * 40.0) + seed);
                float dry = lerp(1.0, smoothstep(0.25, 0.45, streak), _Dry);
                float washAlpha = fill * _Wash * (0.75 + 0.25 * ValueNoise(IN.uv * 4.0 + seed)) * dry;

                float3 inkColor = IN.color.rgb;
                float alpha = saturate(lineInk + washAlpha);
                float3 col = inkColor;

                // Color conservado (enemigos, monedas, dianas): los rellenos saturados mantienen su color, algo más vivo
                float keep = _KeepColor * smoothstep(0.25, 0.45, sat) * (1.0 - lineInk);
                float3 vivid = saturate((c.rgb - 0.5) * 1.25 + 0.5);
                col = lerp(col, vivid, keep);
                alpha = lerp(alpha, 1.0, keep);

                return half4(col, alpha * c.a * IN.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
