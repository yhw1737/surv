// SYS-CHAR-02 §Rendering: resolution-independent vector primitives. uv.xy is a position in the primitive's own
// [-1, 1] space, uv.z the mode (0 stroke: edge at |y| = 1; 1 disk: edge at length = 1; 2 solid). fwidth turns the
// edge into ~1.5 screen pixels of anti-aliasing at any zoom.
// Lit by URP 2D lights (day/night, torches) like the sprites around it; the forward pass is the unlit fallback.
// Drawn in pencil (2026-10-09): paper grain, hatching in dark tones, rough graphite ink, and a gentle line boil.
Shader "Isle/Vector"
{
    Properties
    {
        // A solid colour instead of the drawing (alpha > 0): the orange outline copies behind an interactable.
        _Silhouette ("Silhouette", Color) = (0, 0, 0, 0)
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _Silhouette;
    CBUFFER_END

    // Pencil look (Core/Util/PencilLook.cs mirrors this; keep the two in step). Values [invented look].
    #define PENCIL_CELLS_PER_UNIT 45.0
    #define PENCIL_HATCH_CELLS 3.2
    #define PENCIL_INK_LUMINANCE 0.14
    #define PENCIL_BOIL_FPS 6.0
    #define PENCIL_BOIL_UNITS 0.012

    float PencilHash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float PencilNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(PencilHash(i), PencilHash(i + float2(1, 0)), f.x),
                    lerp(PencilHash(i + float2(0, 1)), PencilHash(i + float2(1, 1)), f.x), f.y);
    }

    // The drawing "boils": it is redrawn a few times a second, each time a little differently.
    float PencilBoil() { return floor(_Time.y * PENCIL_BOIL_FPS); }

    // Lines wobble by a pixel or so between redraws.
    float3 PencilJitter(float3 positionOS)
    {
        float b = PencilBoil();
        float2 n = float2(PencilNoise(positionOS.xy * 7.0 + b * 3.17), PencilNoise(positionOS.yx * 7.0 + b * 5.91 + 13.0));
        return positionOS + float3((n - 0.5) * 2.0 * PENCIL_BOIL_UNITS, 0);
    }

    // sRGB colour in, sRGB colour out (alpha may drop for patchy graphite).
    half4 PencilShade(half4 c, float2 p)
    {
        float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
        float grain = 0.6 * PencilNoise(p) + 0.4 * PencilNoise(p * 2.3 + float2(17.1, 5.3));
        if (lum < PENCIL_INK_LUMINANCE)
        {
            half4 g = c;
            g.rgb = lerp(c.rgb, half3(0.2, 0.2, 0.22), 0.4);
            g.a = c.a * (0.85 + 0.15 * saturate(grain * 1.6));
            return g;
        }
        float3 rgb = c.rgb;
        float tooth = saturate((grain - 0.55) * 3.0) * 0.42 * (0.35 + 0.65 * lum);
        rgb = lerp(rgb, float3(0.96, 0.94, 0.88), tooth);
        float dark = 1.0 - lum;
        float wobble = PencilNoise(p * 0.13 + float2(3.7, 0)) * 0.9;
        float h1 = abs(frac((p.x + p.y) / PENCIL_HATCH_CELLS + wobble) - 0.5) * 2.0;
        float line1 = saturate((0.32 - h1) * 5.0) * saturate((dark - 0.3) * 2.5);
        float h2 = abs(frac((p.x - p.y) / PENCIL_HATCH_CELLS + wobble * 0.7 + 0.37) - 0.5) * 2.0;
        float line2 = saturate((0.26 - h2) * 5.0) * saturate((dark - 0.62) * 3.0);
        float hatch = max(line1, line2) * (0.55 + 0.45 * grain);
        rgb *= 1.0 - 0.24 * hatch;
        rgb *= 1.0 - 0.1 * (grain - 0.5);
        return half4(saturate(rgb), c.a);
    }

    // Coverage of a primitive at this fragment, 0..1. Ink strokes get a rough, hand-drawn edge.
    float VectorCoverage(float4 uv, float2 paper, bool ink)
    {
        float mode = uv.z;
        if (mode > 1.5) return 1.0;
        float d = mode < 0.5 ? abs(uv.y) : length(uv.xy);
        if (ink) d += (PencilNoise(paper * 0.22 + PencilBoil() * 7.3) - 0.5) * 0.2;
        float aa = max(fwidth(d) * 1.5, 1e-4);
        return saturate((1.0 - d) / aa);
    }

    // Vertex colour (sRGB) through the pencil, then to the colour space the pass blends in.
    half4 PencilFragment(float4 colourSRGB, float4 uv, float2 positionOS)
    {
        float2 paper = positionOS * PENCIL_CELLS_PER_UNIT;
        bool ink = dot(colourSRGB.rgb, float3(0.299, 0.587, 0.114)) < PENCIL_INK_LUMINANCE;
        half4 c = PencilShade(half4(colourSRGB), paper);
        c.a *= VectorCoverage(uv, paper, ink);
        #if !defined(UNITY_COLORSPACE_GAMMA)
        c.rgb = SRGBToLinear(c.rgb);
        #endif
        // Material colours arrive already in the blending colour space.
        // Only the solid drawing makes the outline — not the faint foot shadow or other see-through parts.
        if (_Silhouette.a > 0.0) c = half4(_Silhouette.rgb, colourSRGB.a < 0.5 ? 0.0 : c.a * _Silhouette.a);
        return c;
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
                float2 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(PencilJitter(input.positionOS));
                output.uv = input.uv;
                output.lightingUV = half2(ComputeScreenPos(output.positionCS / output.positionCS.w).xy);
                // Mesh vertex colours are authored in sRGB (Unity only converts material/sprite colours for us); the
                // pencil pass works in sRGB and converts in the fragment.
                output.color = input.color;
                output.positionOS = input.positionOS.xy;
                return output;
            }

            // Brings LightingUtility, which declares the shape-light textures (URP 17.3; declared here before).
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                half4 pencil = PencilFragment(input.color, input.uv, input.positionOS);
                if (_Silhouette.a > 0.0) return pencil; // outlines glow the same by day and night
                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(pencil.rgb, pencil.a, half4(1, 1, 1, 1), half3(0, 0, 1), surfaceData);
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
                float2 positionOS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(PencilJitter(input.positionOS));
                output.uv = input.uv;
                output.color = input.color;
                output.positionOS = input.positionOS.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return PencilFragment(input.color, input.uv, input.positionOS);
            }
            ENDHLSL
        }
    }
}
