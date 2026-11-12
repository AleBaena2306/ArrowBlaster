// Arrow Blaster · Shader de tinta
// Convierte cualquier forma simple (cuadrado, círculo, triángulo, línea o estela) en una pincelada:
//  - bordes irregulares (el contorno se desplaza con ruido),
//  - pincel seco: vetas a lo largo del trazo que se abren hacia el final y en los lados,
//  - variaciones de densidad de la tinta y borde algo más oscuro (la tinta se acumula en el filo).
// El trazo se orienta solo: va en la dirección más larga de la forma (una pared vertical tiene vetas verticales).
// Con _WorldSpace = 1 el ruido usa coordenadas del mundo (para el suelo, que sigue a la cámara sin «resbalar»).
Shader "ArrowBlaster/Ink"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Seed ("Semilla (por objeto)", Float) = 0
        _WorldSpace ("Ruido en coordenadas del mundo", Float) = 0
        _Wobble ("Irregularidad del borde (unidades)", Float) = 0.05
        _WobbleFreq ("Frecuencia del borde", Float) = 2.4
        _Dry ("Pincel seco", Range(0, 1)) = 0.7
        _Bristles ("Vetas por unidad", Float) = 18
        _Rim ("Borde oscuro", Range(0, 1)) = 0.35
        _Density ("Variación de densidad", Range(0, 1)) = 0.3
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
                float _WorldSpace;
                float _Wobble;
                float _WobbleFreq;
                float _Dry;
                float _Bristles;
                float _Rim;
                float _Density;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
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

            float Fbm(float2 p)
            {
                float s = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    s += amp * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    amp *= 0.5;
                }
                return s / 0.9375;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // Tamaño en el mundo del rango 0..1 de las UV (resolviendo la jacobiana con las derivadas de pantalla)
                float2 duvdx = ddx(uv);
                float2 duvdy = ddy(uv);
                float3 dpdx = ddx(IN.positionWS);
                float3 dpdy = ddy(IN.positionWS);
                float det = duvdx.x * duvdy.y - duvdx.y * duvdy.x;
                float sizeU = 1.0;
                float sizeV = 1.0;
                if (abs(det) > 1e-12)
                {
                    float3 dpdu = (dpdx * duvdy.y - dpdy * duvdx.y) / det;
                    float3 dpdv = (dpdy * duvdx.x - dpdx * duvdy.x) / det;
                    sizeU = max(length(dpdu), 1e-3);
                    sizeV = max(length(dpdv), 1e-3);
                }
                float px = max(max(length(dpdx), length(dpdy)), 1e-5); // tamaño de un píxel en el mundo

                // Espacio del trazo: a = a lo largo (0..L), b = a lo ancho (0..W)
                bool alongU = sizeU >= sizeV;
                float L = alongU ? sizeU : sizeV;
                float W = alongU ? sizeV : sizeU;
                float a = (alongU ? uv.x : uv.y) * L;
                float b = (alongU ? uv.y : uv.x) * W;
                float2 sp = float2(a, b) + _Seed * 37.13;
                if (_WorldSpace > 0.5) sp = IN.positionWS.xy + _Seed * 37.13;

                // Bordes irregulares
                float wobAmp = min(_Wobble, W * 0.22);
                float wa = (Fbm(sp * _WobbleFreq + 31.7) - 0.5) * 2.0;
                float wb = (Fbm(sp * _WobbleFreq) - 0.5) * 2.0;
                float a2 = a + wa * wobAmp;
                float b2 = b + wb * wobAmp;
                float2 uvd = alongU ? float2(a2 / L, b2 / W) : float2(b2 / W, a2 / L);

                // Máscara: el rectángulo de la forma (distorsionado) y el alfa del sprite (círculos, triángulos)
                float edgeAlong = min(a2, L - a2);
                float edgeAcross = min(b2, W - b2);
                float box = saturate(min(edgeAlong, edgeAcross) / px + 0.5);
                float texA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvd).a;
                float texMask = saturate((texA - 0.5) / max(fwidth(texA), 1e-4) + 0.5);
                float mask = box * texMask;

                // Pincel seco: vetas a lo largo del trazo, más abiertas hacia el final y en los lados.
                // Las formas casi redondas apenas se secan.
                float2 bp = _WorldSpace > 0.5 ? IN.positionWS.xy : float2(a, b);
                float bristle = Fbm(float2(bp.x * 1.3, bp.y * _Bristles) + _Seed * 5.1);
                float t = saturate(a2 / L);
                float sides = 1.0 - saturate(edgeAcross / max(W * 0.5, 1e-3));
                float elongation = saturate((L / W - 1.2) / 3.0);
                float zone = (saturate((t - 0.5) / 0.5) * 0.85 * (1.0 - _WorldSpace) + sides * 0.45) * _Dry * lerp(0.3, 1.0, elongation);
                float dry = smoothstep(zone - 0.07, zone + 0.07, bristle);
                mask *= dry;

                // Densidad de la tinta (aguadas más claras) y borde más oscuro
                float wash = Fbm(sp * 0.8 + 3.3);
                float density = 1.0 - _Density * (wash * 0.7 + t * 0.3 * (1.0 - _WorldSpace));
                float rim = 1.0 - saturate(min(edgeAlong, edgeAcross) / max(min(W, L) * 0.2, 1e-3));
                float3 col = IN.color.rgb * (1.0 - _Rim * rim * 0.45);
                float alpha = mask * IN.color.a * saturate(density + rim * 0.3);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
