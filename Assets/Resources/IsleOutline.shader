// The orange ring around whatever E would act on: drawn over a copy of its sprite, this paints only the band just
// outside the sprite's opaque shape (a texel is in the band when it is clear but a neighbour _OutlineUV away is
// not), in the renderer's colour. No fill, so it stays clean over a see-through (occluding) sprite. Unlit, so it
// reads the same by day and night.
Shader "Isle/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _OutlineUV ("Width (uv)", Float) = 0.01
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        float _OutlineUV;
    CBUFFER_END

    struct Attributes
    {
        float3 positionOS : POSITION;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        half4 color : COLOR;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        // Unity 6 hands a SpriteRenderer's colour and flip to the shader, not the mesh (as URP's sprite shaders do).
        SetUpSpriteInstanceProperties();
        output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
        output.color = input.color * unity_SpriteColor;
        output.uv = input.uv;
        return output;
    }

    half Alpha(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a; }

    half4 frag(Varyings input) : SV_Target
    {
        float2 uv = input.uv;
        float w = _OutlineUV;
        float d = w * 0.7071;
        half near = max(max(max(Alpha(uv + float2(w, 0)), Alpha(uv - float2(w, 0))), max(Alpha(uv + float2(0, w)), Alpha(uv - float2(0, w)))),
                        max(max(Alpha(uv + float2(d, d)), Alpha(uv - float2(d, d))), max(Alpha(uv + float2(d, -d)), Alpha(uv + float2(-d, d)))));
        half ring = saturate((near - Alpha(uv) - 0.3) * 2.5); // the pencil ink is a little see-through inside: only a real edge counts
        return half4(input.color.rgb, input.color.a * ring);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
