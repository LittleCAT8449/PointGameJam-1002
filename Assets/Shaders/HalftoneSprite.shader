Shader "PointGameJam/Halftone Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Renderer Tint", Color) = (1,1,1,1)
        _DotColor ("Dot Color", Color) = (0.2,1,0.35,1)
        _GridCount ("Grid Count", Vector) = (64,64,0,0)
        _SpriteBounds ("Sprite Bounds", Vector) = (-1,-1,2,2)
        _DotRatio ("Dot Radius Range", Vector) = (0.08,0.95,0,0)
        _JitterStrength ("Jitter Strength", Range(0, 0.5)) = 0.15
        _DarkPixelsLarger ("Dark Pixels Larger", Float) = 0
        [HideInInspector] _RevealRegularity ("Reveal Regularity", Range(0, 1)) = 0
        [HideInInspector] _RippleEnabled ("Ripple Enabled", Float) = 0
        [HideInInspector] _RippleOrigin ("Ripple Origin", Vector) = (0,0,0,0)
        [HideInInspector] _RippleStartTime ("Ripple Start Time", Float) = 0
        [HideInInspector] _RippleSpeed ("Ripple Speed", Float) = 15
        [HideInInspector] _RippleMaxRadius ("Ripple Max Radius", Float) = 0
        _RippleAmplitude ("Ripple Displacement", Range(0, 0.5)) = 0.08
        _RippleFrequency ("Ripple Frequency", Range(1, 80)) = 28
        _RippleDamping ("Ripple Damping", Range(0.1, 15)) = 5
        _RippleDotScale ("Ripple Dot Scale", Range(0, 1)) = 0.55
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

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _DotColor;
            float4 _GridCount;
            float4 _SpriteBounds;
            float4 _DotRatio;
            float _JitterStrength;
            float _DarkPixelsLarger;
            float _RevealRegularity;
            float _RippleEnabled;
            float4 _RippleOrigin;
            float _RippleStartTime;
            float _RippleSpeed;
            float _RippleMaxRadius;
            float _RippleAmplitude;
            float _RippleFrequency;
            float _RippleDamping;
            float _RippleDotScale;
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
            float2 localPosition : TEXCOORD1;
            float4 color : COLOR;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.localPosition = input.positionOS.xy;
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
            return float2(
                Hash11(p),
                Hash11(p + float2(19.19, 73.13)));
        }

        float EvaluateRipple(float2 worldPosition)
        {
            float distanceFromOrigin = distance(worldPosition, _RippleOrigin.xy);
            float age = _Time.y - _RippleStartTime - distanceFromOrigin / max(_RippleSpeed, 0.01);
            float active = _RippleEnabled * step(0.0, age) * step(distanceFromOrigin, _RippleMaxRadius);
            return active * sin(max(age, 0.0) * _RippleFrequency) * exp(-max(age, 0.0) * _RippleDamping);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float2 boundsSize = max(_SpriteBounds.zw, float2(0.00001, 0.00001));
            float2 gridCount = max(_GridCount.xy, float2(1.0, 1.0));
            float2 spriteCoord = saturate((input.localPosition - _SpriteBounds.xy) / boundsSize);
            float2 gridCoord = min(spriteCoord * gridCount, gridCount - 0.0001);
            float2 cellIndex = floor(gridCoord);

            float2 centerLocal = _SpriteBounds.xy + (cellIndex + 0.5) / gridCount * boundsSize;
            float2 cellSize = boundsSize / gridCount;
            float2 randomOffset = Hash22(cellIndex) * 2.0 - 1.0;
            float remainingJitter = _JitterStrength * (1.0 - saturate(_RevealRegularity));
            float2 dotCenterLocal = centerLocal + randomOffset * cellSize * remainingJitter;
            float2 localDx = ddx(input.localPosition);
            float2 localDy = ddy(input.localPosition);
            float2 uvDx = ddx(input.uv);
            float2 uvDy = ddy(input.uv);
            float determinant = localDx.x * localDy.y - localDx.y * localDy.x;
            float safeDeterminant = (determinant < 0.0 ? -1.0 : 1.0) * max(abs(determinant), 0.000001);
            float2 uvPerLocalX = (uvDx * localDy.y - uvDy * localDx.y) / safeDeterminant;
            float2 uvPerLocalY = (uvDy * localDx.x - uvDx * localDy.x) / safeDeterminant;
            float2 centerUV = input.uv
                + uvPerLocalX * (centerLocal.x - input.localPosition.x)
                + uvPerLocalY * (centerLocal.y - input.localPosition.y);
            half4 toneSample = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, centerUV);

            half luminance = dot(toneSample.rgb, half3(0.2126, 0.7152, 0.0722));
            half tone = lerp(luminance, 1.0h - luminance, saturate(_DarkPixelsLarger));
            half radiusRatio = lerp(_DotRatio.x, _DotRatio.y, tone);
            float minCellSize = max(min(cellSize.x, cellSize.y), 0.00001);

            float3 dotCenterWorld = TransformObjectToWorld(float3(dotCenterLocal, 0.0));
            float2 fromRippleOrigin = dotCenterWorld.xy - _RippleOrigin.xy;
            float distanceFromRippleOrigin = length(fromRippleOrigin);
            float ripple = EvaluateRipple(dotCenterWorld.xy);
            float2 outward = fromRippleOrigin / max(distanceFromRippleOrigin, 0.0001);
            float3 displacedCenterWorld = dotCenterWorld;
            displacedCenterWorld.xy += outward * (_RippleAmplitude * ripple);
            float2 displacedCenterLocal = TransformWorldToObject(displacedCenterWorld).xy;

            float2 localOffset = input.localPosition - displacedCenterLocal;
            float distanceInCells = length(localOffset / minCellSize);
            float radiusInCells = radiusRatio * (1.0 + abs(ripple) * _RippleDotScale) * 0.5;
            float antialias = max(fwidth(distanceInCells), 0.002);
            half circle = 1.0h - smoothstep(radiusInCells - antialias, radiusInCells + antialias, distanceInCells);

            half4 sourceAtPixel = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.uv);
            half alpha = circle * toneSample.a * sourceAtPixel.a * _DotColor.a * input.color.a;
            return half4(_DotColor.rgb * input.color.rgb, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "SpriteUnlit"
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
            Name "SpriteForward"
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


