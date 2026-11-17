Shader "Team5/Tower Effect"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex : POSITION; float4 color : COLOR; };
            struct output { float4 vertex : SV_POSITION; float4 color : COLOR; };
            float4 _Color;
            output vert(input v) { output o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; return o; }
            half4 frag(output i) : SV_Target { return i.color; }
            ENDHLSL
        }
    }
}
