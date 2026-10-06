Shader "PointGameJam/Connection Dots"
{
    Properties
    {
        _Color ("Bridge Color", Color) = (0.55,0.9,1,1)
        _DotCount ("Dot Count", Float) = 32
        _DotRadius ("Dot Radius", Range(0.1,0.5)) = 0.46
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

        half4 Frag(Varyings input) : SV_Target
        {
            float2 dotPosition = float2((frac(input.uv.x * max(_DotCount, 1.0)) - 0.5) * 2.0,
                (input.uv.y - 0.5) * 2.0);
            float distanceFromCenter = length(dotPosition);
            float antialias = max(fwidth(distanceFromCenter), 0.002);
            half alpha = 1.0h - smoothstep(_DotRadius - antialias, _DotRadius + antialias, distanceFromCenter);
            return half4(input.color.rgb, input.color.a * alpha);
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
