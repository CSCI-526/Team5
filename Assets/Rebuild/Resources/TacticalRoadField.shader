Shader "Team5/Rebuild/TacticalRoadField"
{
    Properties { _Field ("Signed distance field", 2D) = "black" {} }
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
            TEXTURE2D(_Field); SAMPLER(sampler_Field);
            struct Attributes { float4 positionOS : POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.world=TransformObjectToWorld(v.positionOS.xyz).xz; o.uv=v.uv; return o; }
            float hash2(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise2(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash2(i),hash2(i+float2(1,0)),f.x),lerp(hash2(i+float2(0,1)),hash2(i+1),f.x),f.y); }
            half4 Frag(Varyings v):SV_Target
            {
                float3 field=SAMPLE_TEXTURE2D(_Field,sampler_Field,v.uv).rgb;
                float dist=(field.r-.5)*6; float boundary=(field.g-.5)*6; float curve=field.b*4;
                // The subtle verge is still a visible boundary of the movement area.
                // No hard 1-tile rings; blend continuously from packed dirt to dry grass.
                float n=noise2(v.world*1.8)-.5;
                float inner=(1-smoothstep(1.15,2.18,curve+n*.12))*smoothstep(.04,.66,dist);
                float edge=smoothstep(-.10,.20,boundary)*lerp(.44,1,smoothstep(-.15,.30,dist));
                float3 shoulder=float3(.46,.52,.35), dirt=float3(.76,.64,.44);
                float3 color=lerp(shoulder,dirt,inner);
                float tonal=(noise2(v.world*.38)-.5)*.11+(noise2(v.world*4.1)-.5)*.045;
                color*=1+tonal;
                // A narrow organic dark edge provides a readable limit without a bright grid.
                color*=1-.11*(1-smoothstep(.02,.22,dist));
                #ifndef UNITY_COLORSPACE_GAMMA
                color=pow(max((color+.055)/1.055,0),2.4);
                #endif
                return half4(color,edge);
            }
            ENDHLSL
        }
    }
}
