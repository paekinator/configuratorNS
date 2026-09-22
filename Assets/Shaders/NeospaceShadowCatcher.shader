// An invisible ground that still has a shadow on it.
//
// The floor draws nothing of its own — no colour, no grid, no edge. All it
// contributes is the darkening where the key light is blocked, so the build
// reads as standing on something without a slab being drawn under it. The
// backdrop gradient shows through everywhere else, and the plane's own
// boundary is invisible because a plane with nothing on it has no boundary to
// see.
//
// The grid is NOT here. It is a separate surface above this one
// (AdaptiveGridController), which lets the grid fade out on its own terms
// without taking the shadow with it.
//
// WHAT IT IS NOT. This is not a URP Lit material with a transparent tint: that
// would still take ambient light, still show a faint slab, and still need its
// edge hidden. It samples the main light's shadow map and outputs darkness
// alone.
Shader "NEOSPACE/Shadow Catcher"
{
    Properties
    {
        _ShadowColor ("Shadow colour", Color) = (0.125, 0.125, 0.125, 1)

        // How dark the shadow gets where it is fully blocked. NOT the only
        // factor: URP has already applied the light's own shadow strength by
        // the time this multiplies, so the darkest the ground goes is the two
        // multiplied together. Both are set from StageLighting.
        _Strength ("Ground opacity", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // After all opaque geometry, before the grid patch at 2900, so a
            // shadow lies under the grid lines rather than over them.
            "Queue" = "Transparent-200"
        }

        Pass
        {
            Name "ShadowCatcher"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            // Without these the shadow map is never bound and the ground stays
            // perfectly clear — which is a silent failure, not an error.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Assets/Shaders/NeospaceGroundClip.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _Strength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                // The same near-plane problem as the grid, and the same fix:
                // this is the ground too, and a shadow cut off in a straight
                // line across the screen is as wrong as a grid cut off there.
                OUT.positionCS = NeospaceKeepGroundInFront(positions.positionCS);
                OUT.positionWS = positions.positionWS;
                return OUT;
            }

            half4 Fragment(Varyings IN, out float depth : SV_Depth) : SV_Target
            {
                depth = NeospaceGroundDepth(IN.positionWS);

                // 1 where the light reaches, 0 where it is fully blocked. URP
                // has already folded the light's own shadowStrength into this,
                // so a light set to half strength never returns 0 here.
                half attenuation = MainLightRealtimeShadow(TransformWorldToShadowCoord(IN.positionWS));

                half alpha = saturate((1.0h - attenuation) * _Strength);
                return half4(_ShadowColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
