Shader "PointGameJam/Scattering Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Renderer Tint", Color) = (1,1,1,1)
        _GridCount ("Point Grid", Vector) = (28,36,0,0)
        _DotRadius ("Point Radius", Range(0.1, 0.5)) = 0.38
        _ScatterProgress ("Scatter Progress", Range(0, 1)) = 0
        _ScatterDistance ("Scatter Distance", Range(0, 0.5)) = 0.18
        _EdgeWidth ("Dissolve Edge Width", Range(0.001, 0.2)) = 0.04
        _EdgeColor ("Dissolve Edge Color", Color) = (1,0.55,1,0.8)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _GridCount;
            float4 _EdgeColor;
            float _DotRadius;
            float _ScatterProgress;
            float _ScatterDistance;
            float _EdgeWidth;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.color = input.color * _Color;
            return output;
        }

        float Hash11(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float2 Hash22(float2 p)
        {
            return float2(Hash11(p), Hash11(p + float2(19.19, 73.13)));
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float2 gridCount = max(_GridCount.xy, float2(1.0, 1.0));
            float2 gridCoordinate = saturate(input.uv) * gridCount;
            float2 cellIndex = min(floor(gridCoordinate), gridCount - 0.0001);
            float2 cellPosition = frac(gridCoordinate);
            float2 centerUV = (cellIndex + 0.5) / gridCount;
            half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, centerUV);

            float2 randomDirection = Hash22(cellIndex) * 2.0 - 1.0;
            randomDirection /= max(length(randomDirection), 0.0001);
            float2 pointCenter = 0.5 + randomDirection * _ScatterProgress * _ScatterDistance;
            float pointDistance = length(cellPosition - pointCenter);
            float antialias = max(fwidth(pointDistance), 0.002);
            half pointMask = 1.0h - smoothstep(_DotRadius - antialias, _DotRadius + antialias, pointDistance);

            float edgeWidth = max(_EdgeWidth, 0.001);
            float dissolveFront = _ScatterProgress * (1.0 + 2.0 * edgeWidth) - edgeWidth;
            float randomness = Hash11(cellIndex);
            half visibility = 1.0h - smoothstep(randomness - edgeWidth, randomness + edgeWidth, dissolveFront);
            float edgeDistance = abs(randomness - dissolveFront);
            half dissolveEdge = 1.0h - smoothstep(edgeWidth * 0.15, edgeWidth, edgeDistance);

            half3 baseColor = source.rgb * input.color.rgb;
            half3 finalColor = lerp(baseColor, _EdgeColor.rgb, dissolveEdge * _EdgeColor.a);
            half alpha = source.a * input.color.a * pointMask * visibility;
            return half4(finalColor, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "ScatteringSprite2D"
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            ENDHLSL
        }

        Pass
        {
            Name "ScatteringSpriteForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            ENDHLSL
        }
    }
}
