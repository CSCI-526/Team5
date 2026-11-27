Shader "Team5/Rebuild/BattlefieldMeadow"
{
    Properties { _Color ("Meadow tint", Color) = (.31,.45,.31,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
              half4 _Color;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 world:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.world=TransformObjectToWorld(v.positionOS.xyz).xz; return o; }
            float hash2(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise2(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash2(i),hash2(i+float2(1,0)),f.x),lerp(hash2(i+float2(0,1)),hash2(i+1),f.x),f.y); }
            half4 Frag(Varyings v):SV_Target
            {
                float broad=noise2(v.world*.12);
                float grain=noise2(v.world*2.2)-.5;
                float3 c=_Color.rgb+float3(.075,.065,.022)*(broad-.5)+grain*.018;
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}
