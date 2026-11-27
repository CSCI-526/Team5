Shader "Team5/Rebuild/TacticalRoadPolish"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 world : TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.world=TransformObjectToWorld(v.positionOS.xyz).xz; return o; }
            float hash2(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise2(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash2(i),hash2(i+float2(1,0)),f.x),lerp(hash2(i+float2(0,1)),hash2(i+1),f.x),f.y); }
            half4 Frag(Varyings v) : SV_Target
            {
                float shade = (noise2(v.world*.9)-.5)*.09 + (noise2(v.world*5)-.5)*.035;
                return half4(_Color.rgb*(1+shade),_Color.a);
            }
            ENDHLSL
        }
    }
}
