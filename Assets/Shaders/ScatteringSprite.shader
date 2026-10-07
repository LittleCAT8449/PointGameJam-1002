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
        [HideInInspector] _RippleEnabled ("Ripple Enabled", Float) = 0
        [HideInInspector] _RippleOrigin ("Ripple Origin", Vector) = (0,0,0,0)
        [HideInInspector] _RippleStartTime ("Ripple Start Time", Float) = 0
        [HideInInspector] _RippleSpeed ("Ripple Speed", Float) = 15
        [HideInInspector] _RippleMaxRadius ("Ripple Max Radius", Float) = 0
        [HideInInspector] _RippleFrequency ("Ripple Frequency", Float) = 28
        [HideInInspector] _RippleDamping ("Ripple Damping", Float) = 5
        [HideInInspector] _RippleDotScale ("Ripple Dot Scale", Float) = 0.2
        [HideInInspector] _RippleWobble ("Ripple Wobble", Float) = 0.035
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
            float _RippleEnabled;
            float4 _RippleOrigin;
            float _RippleStartTime;
            float _RippleSpeed;
            float _RippleMaxRadius;
            float _RippleFrequency;
            float _RippleDamping;
            float _RippleDotScale;
            float _RippleWobble;
        CBUFFER_END

        float EvaluateRipple(float2 worldPosition)
        {
            float distanceFromOrigin = distance(worldPosition, _RippleOrigin.xy);
            float age = _Time.y - _RippleStartTime - distanceFromOrigin / max(_RippleSpeed, 0.01);
            float active = _RippleEnabled * step(0.0, age) * step(distanceFromOrigin, _RippleMaxRadius);
            return active * sin(max(age, 0.0) * _RippleFrequency) * exp(-max(age, 0.0) * _RippleDamping);
        }

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
            float2 positionWS : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionHCS = TransformWorldToHClip(positionWS);
            output.uv = input.uv;
            output.color = input.color * _Color;
            output.positionWS = positionWS.xy;
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
            float ripple = EvaluateRipple(input.positionWS);
            float2 pointCenter = 0.5 + randomDirection * _ScatterProgress * _ScatterDistance;
            pointCenter += randomDirection * (_RippleWobble * ripple);
            float pointDistance = length(cellPosition - pointCenter);
            float antialias = max(fwidth(pointDistance), 0.002);
            float pointRadius = min(0.5, _DotRadius * (1.0 + abs(ripple) * _RippleDotScale));
            half pointMask = 1.0h - smoothstep(pointRadius - antialias, pointRadius + antialias, pointDistance);

            float edgeWidth = max(_EdgeWidth, 0.001);
            float dissolveFront = _ScatterProgress * (1.0 + 2.0 * edgeWidth) - edgeWidth;
            float randomness = Hash11(cellIndex);
            half visibility = 1.0h - smoothstep(randomness - edgeWidth, randomness + edgeWidth, dissolveFront);
            float edgeDistance = abs(randomness - dissolveFront);
            half dissolveEdge = 1.0h - smoothstep(edgeWidth * 0.15, edgeWidth, edgeDistance);

            half3 baseColor = source.rgb * input.color.rgb;
            baseColor = saturate(baseColor * (1.0 + abs(ripple) * 0.35));
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
