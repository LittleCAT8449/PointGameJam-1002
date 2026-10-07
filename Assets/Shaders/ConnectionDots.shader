Shader "PointGameJam/Connection Dots"
{
    Properties
    {
        _Color ("Bridge Color", Color) = (0.55,0.9,1,1)
        _DotCount ("Dot Count", Float) = 32
        _DotRadius ("Dot Radius", Range(0.1,0.5)) = 0.46
        [HideInInspector] _RippleEnabled ("Ripple Enabled", Float) = 0
        [HideInInspector] _RippleOrigin ("Ripple Origin", Vector) = (0,0,0,0)
        [HideInInspector] _RippleStartTime ("Ripple Start Time", Float) = 0
        [HideInInspector] _RippleSpeed ("Ripple Speed", Float) = 15
        [HideInInspector] _RippleMaxRadius ("Ripple Max Radius", Float) = 0
        [HideInInspector] _RippleAmplitude ("Ripple Amplitude", Float) = 0.055
        [HideInInspector] _RippleFrequency ("Ripple Frequency", Float) = 28
        [HideInInspector] _RippleDamping ("Ripple Damping", Float) = 5
        [HideInInspector] _RippleDotScale ("Ripple Dot Scale", Float) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _DotCount;
            float _DotRadius;
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
            float ripple = EvaluateRipple(positionWS.xy);
            float2 outward = positionWS.xy - _RippleOrigin.xy;
            outward /= max(length(outward), 0.0001);
            positionWS.xy += outward * (_RippleAmplitude * ripple);
            output.positionHCS = TransformWorldToHClip(positionWS);
            output.uv = input.uv;
            output.color = input.color * _Color;
            output.positionWS = positionWS.xy;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float2 dotPosition = float2((frac(input.uv.x * max(_DotCount, 1.0)) - 0.5) * 2.0,
                (input.uv.y - 0.5) * 2.0);
            float distanceFromCenter = length(dotPosition);
            float antialias = max(fwidth(distanceFromCenter), 0.002);
            float ripple = EvaluateRipple(input.positionWS);
            float dotRadius = min(0.5, _DotRadius * (1.0 + abs(ripple) * _RippleDotScale));
            half alpha = 1.0h - smoothstep(dotRadius - antialias, dotRadius + antialias, distanceFromCenter);
            half3 color = saturate(input.color.rgb * (1.0 + abs(ripple) * 0.25));
            return half4(color, input.color.a * alpha);
        }
        ENDHLSL

        Pass
        {
            Name "ConnectionDots2D"
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
            Name "ConnectionDotsForward"
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
