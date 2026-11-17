Shader "Team5/FW Sprite World Z"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _FWFootZ ("Unit Foot World Z", Float) = 0
        [HideInInspector] _FWHairColor ("Role Hair Color", Color) = (1,1,1,1)
        [HideInInspector] _FWHairMode ("Hair Mask Mode", Float) = 0
        [HideInInspector] _FWHairMask ("Front Hair Mask", 2D) = "black" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        // The fragment shader retains foreground depth occlusion, but ignores surfaces behind the feet in Z.
        ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            TEXTURE2D(_FWHairMask);
            SAMPLER(sampler_FWHairMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FWFootZ;
                half4 _FWHairColor;
                float _FWHairMode;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool foreground = sceneDepth > input.positionCS.z + 0.0000001;
                #else
                    bool foreground = sceneDepth < input.positionCS.z - 0.0000001;
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, sceneDepth);
                #endif
                if (foreground)
                {
                    float3 surface = ComputeWorldSpacePosition(uv, sceneDepth, UNITY_MATRIX_I_VP);
                    if (surface.z <= _FWFootZ + .001) discard;
                }
                half4 color = CommonUnlitFragment(input, input.color);
                if (_FWHairMode > .5)
                {
                    half mask = _FWHairMode > 1.5 ? SAMPLE_TEXTURE2D(_FWHairMask, sampler_FWHairMask, input.uv).r : 1;
                    // Replace the original brown hue while keeping painted highlights/shadows.
                    half luminance = dot(color.rgb, half3(.2126, .7152, .0722));
                    #if defined(UNITY_COLORSPACE_GAMMA)
                        half shade = clamp(luminance / .48, .18, 1.4);
                    #else
                        half shade = clamp(luminance / .20, .18, 1.4);
                    #endif
                    color.rgb = lerp(color.rgb, _FWHairColor.rgb * shade, mask);
                }
                return color;
            }
            ENDHLSL
        }
    }
}
