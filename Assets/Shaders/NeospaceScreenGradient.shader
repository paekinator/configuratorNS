// The studio backdrop: a wash behind everything in the 3D view.
//
// STATED AS FOUR CORNERS, not as an angle and a list of stops. The first
// attempt ported the mockup's CSS literally —
//
//     linear-gradient(135deg, #fff 0%, #F0EEE9 52%, #deddd9 100%)
//
// — and got it wrong in a way worth recording. CSS normalises a 135deg
// gradient by PIXELS: t = (x + y) / (W + H). The shader normalised by UV:
// t = (x/W + y/H) / 2, which weights a short axis as heavily as a long one.
// On a 16:9 screen the bottom-left corner then sat at t = 0.5 instead of
// t = 0.36, so the lower half of the view went grey far sooner than the
// mockup's and the whole thing read as flat and dull.
//
// Four corners cannot have that fault: each corner is the value it is told to
// be, at every aspect ratio, and the field between them is a plain bilinear
// blend. The numbers are brightness percentages of a neutral grey, which is
// how they were specified:
//
//     top-left 100%   top-right 97%
//     bottom-left 85%  bottom-right 80%
//
// BLENDED IN DISPLAY SPACE. "85% brightness" is a value in sRGB — it is what
// a colour picker shows. In a linear-colour project Unity hands this shader
// linearised colours, and blending those gives a field whose midpoints are
// measurably lighter than the four numbers imply. So the corners are taken
// back to sRGB, blended there, and returned to linear on the way out: the
// numbers then mean on screen exactly what they mean in the picker.
//
// FIXED TO THE SCREEN, not to the world. The vertex stage writes clip-space
// positions and ignores the object's transform entirely, so the quad fills the
// viewport whatever the camera is doing — perspective or orthographic,
// orbiting or zoomed — and the wash never slides while the model turns.
// Because the transform is ignored, the quad's position and scale exist only
// to keep it inside the frustum so it is not culled before it can paint.
Shader "NEOSPACE/Screen Gradient"
{
    Properties
    {
        // Defaults are a rough stand-in only: ConfiguratorEnvironmentStyler
        // writes all four from SceneBackdrop, which is where they are decided.
        [MainColor] _CornerTL ("Top left", Color) = (1, 1, 1, 1)
        _CornerTR ("Top right", Color) = (0.969, 0.969, 0.969, 1)
        _CornerBL ("Bottom left", Color) = (0.851, 0.851, 0.851, 1)
        _CornerBR ("Bottom right", Color) = (0.800, 0.800, 0.800, 1)

        // White to 80% grey spans about 50 steps of 8-bit grey across a whole
        // screen, which bands into visible stripes. A little noise below one
        // step breaks them up and is itself invisible.
        _Dither ("Dither (steps of 1/255)", Range(0, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Background"
        }

        Pass
        {
            Name "ScreenGradient"

            // Drawn before all geometry, paints every pixel, and takes no part
            // in depth: the model is never occluded by its own backdrop.
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CornerTL;
                float4 _CornerTR;
                float4 _CornerBL;
                float4 _CornerBR;
                float _Dither;
            CBUFFER_END

            // Written out rather than taken from the URP library: this shader
            // needs them in both directions on every platform, and a helper
            // whose name or signature shifts between package versions would
            // take the backdrop with it.
            float3 ToDisplay(float3 c)
            {
                c = max(c, 0.0);
                float3 lo = c * 12.92;
                float3 hi = 1.055 * pow(c + 1e-7, 1.0 / 2.4) - 0.055;
                // step(), not ?: — a componentwise ternary on a vector is not
                // portable across every shader compiler Unity uses.
                return lerp(lo, hi, step(0.0031308, c));
            }

            float3 ToRender(float3 c)
            {
                c = max(c, 0.0);
                float3 lo = c / 12.92;
                float3 hi = pow((c + 0.055) / 1.055 + 1e-7, 2.4);
                return lerp(lo, hi, step(0.04045, c));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;

                // Unity's Quad is +/-0.5 in object space; doubling gives the
                // -1..1 of normalised device coordinates, which is the whole
                // viewport. No view or projection matrix is consulted.
                float2 ndc = IN.positionOS.xy * 2.0;

                // _ProjectionParams.x is -1 when the frame is being rendered
                // upside down into an intermediate target. Ordinary shaders get
                // this for free from the projection matrix; this one bypasses
                // the matrix, so it has to say so itself or the wash would run
                // the wrong way in exactly those passes.
                OUT.positionCS = float4(ndc.x, ndc.y * _ProjectionParams.x,
                                        UNITY_NEAR_CLIP_VALUE, 1.0);

                // From object space, so it follows the vertex through that flip
                // and always means the same corner of the finished image.
                OUT.uv = IN.positionOS.xy + 0.5;
                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                float3 tl = _CornerTL.rgb;
                float3 tr = _CornerTR.rgb;
                float3 bl = _CornerBL.rgb;
                float3 br = _CornerBR.rgb;

                #ifndef UNITY_COLORSPACE_GAMMA
                    tl = ToDisplay(tl);
                    tr = ToDisplay(tr);
                    bl = ToDisplay(bl);
                    br = ToDisplay(br);
                #endif

                float3 top = lerp(tl, tr, IN.uv.x);
                float3 bottom = lerp(bl, br, IN.uv.x);
                float3 rgb = lerp(bottom, top, IN.uv.y);

                // Dithered here, in display space, because a "step" of banding
                // is one part in 255 of the FINISHED image.
                float noise = frac(sin(dot(IN.positionCS.xy, float2(12.9898, 78.233))) * 43758.5453);
                rgb += (noise - 0.5) * (_Dither / 255.0);

                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = ToRender(rgb);
                #endif

                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
