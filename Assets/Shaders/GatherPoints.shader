Shader "PointGameJam/Gather Points"
{
    Properties
    {
        _DotColor ("Dot Color", Color) = (0.32, 1, 0.58, 1)
        _GatherProgress ("Gather Progress", Range(0, 1)) = 0
        _DotSize ("Dot Size", Float) = 0.085
        _ScatteredAlpha ("Scattered Alpha", Range(0, 1)) = 0.45
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
        CBUFFER_END

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
            float size = _DotSize * lerp(0.65, 1.0, pointProgress);
            float2 positionOS = center + input.animationData.xy * size * 0.5;

            output.positionHCS = TransformObjectToHClip(float3(positionOS, input.positionOS.z));
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
