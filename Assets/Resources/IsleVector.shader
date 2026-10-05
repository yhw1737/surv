// SYS-CHAR-02 §Rendering: resolution-independent vector primitives. uv.xy is a position in the primitive's own
// [-1, 1] space, uv.z the mode (0 stroke: edge at |y| = 1; 1 disk: edge at length = 1; 2 solid). fwidth turns the
// edge into ~1.5 screen pixels of anti-aliasing at any zoom.
// Lit by URP 2D lights (day/night, torches) like the sprites around it; the forward pass is the unlit fallback.
Shader "Isle/Vector"
{
    Properties { }

    HLSLINCLUDE
    // Coverage of a primitive at this fragment, 0..1.
    float VectorCoverage(float4 uv)
    {
        float mode = uv.z;
        if (mode > 1.5) return 1.0;
        float d = mode < 0.5 ? abs(uv.y) : length(uv.xy);
        float aa = max(fwidth(d) * 1.5, 1e-4);
        return saturate((1.0 - d) / aa);
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                // Mesh vertex colours are authored in sRGB; Unity only converts material/sprite colours for us.
                output.color = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb = SRGBToLinear(input.color.rgb);
                #endif
                return output;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = input.color.a * VectorCoverage(input.uv);
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(input.color.rgb, alpha, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
                InitializeInputData(input.uv.xy, input.lightingUV, inputData);
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                output.color.rgb = SRGBToLinear(input.color.rgb);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a * VectorCoverage(input.uv));
            }
            ENDHLSL
        }
    }
}
