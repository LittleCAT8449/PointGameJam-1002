Shader "PointGameJam/Gather Points"
{
    Properties
    {
        _DotColor ("Dot Color", Color) = (0.32, 1, 0.58, 1)
        _GatherProgress ("Gather Progress", Range(0, 1)) = 0
        _DotSize ("Dot Size", Float) = 0.085
        _ScatteredAlpha ("Scattered Alpha", Range(0, 1)) = 0.45
        [HideInInspector] _RippleEnabled ("Ripple Enabled", Float) = 0
        [HideInInspector] _RippleOrigin ("Ripple Origin", Vector) = (0,0,0,0)
        [HideInInspector] _RippleStartTime ("Ripple Start Time", Float) = 0
        [HideInInspector] _RippleSpeed ("Ripple Speed", Float) = 15
        [HideInInspector] _RippleMaxRadius ("Ripple Max Radius", Float) = 0
        [HideInInspector] _RippleAmplitude ("Ripple Amplitude", Float) = 0.08
        [HideInInspector] _RippleFrequency ("Ripple Frequency", Float) = 28
        [HideInInspector] _RippleDamping ("Ripple Damping", Float) = 5
        [HideInInspector] _RippleDotScale ("Ripple Dot Scale", Float) = 0.55
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
            float4 _DotColor;
            float _GatherProgress;
            float _DotSize;
            float _ScatteredAlpha;
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
            float2 targetCenter : TEXCOORD0;
            float4 animationData : TEXCOORD1;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 dotCoordinate : TEXCOORD0;
            half alpha : TEXCOORD1;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float delay = saturate(input.animationData.z);
            float pointProgress = saturate((_GatherProgress - delay) / max(1.0 - delay, 0.0001));
            float2 center = lerp(input.positionOS.xy, input.targetCenter, pointProgress);
            float3 centerWS = TransformObjectToWorld(float3(center, input.positionOS.z));
            float ripple = EvaluateRipple(centerWS.xy);
            float2 outward = centerWS.xy - _RippleOrigin.xy;
            outward /= max(length(outward), 0.0001);
            centerWS.xy += outward * (_RippleAmplitude * ripple);

            float size = _DotSize * lerp(0.65, 1.0, pointProgress);
            size *= 1.0 + abs(ripple) * _RippleDotScale;
            float2 cornerOS = center + input.animationData.xy * size * 0.5;
            float3 positionWS = TransformObjectToWorld(float3(cornerOS, input.positionOS.z));
            positionWS.xy += outward * (_RippleAmplitude * ripple);

            output.positionHCS = TransformWorldToHClip(positionWS);
            output.dotCoordinate = input.animationData.xy;
            output.alpha = lerp(_ScatteredAlpha, 1.0, pointProgress) * input.color.a;
            return output;
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float distanceFromCenter = length(input.dotCoordinate);
            float antialias = max(fwidth(distanceFromCenter), 0.002);
            half circle = 1.0h - smoothstep(1.0 - antialias, 1.0 + antialias, distanceFromCenter);
            half alpha = circle * input.alpha * _DotColor.a;
            return half4(_DotColor.rgb, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "GatherPoints2D"
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
            Name "GatherPointsForward"
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
