// The ground plane's grid, all of it: three layers in one pass.
//
//   1. THE H7 LATTICE (704 mm), everywhere, quiet. Scale and orientation.
//   2. THE MODULE GRID (88 mm), only near what has been built, greyscale,
//      dissolving into layer 1 at a soft edge.
//   3. THE HOVER, in the highlight colour, around wherever the thing being
//      placed will land — plus the outline of the cell (or block of cells) it
//      will occupy.
//
// ONE PASS, ONE FUNCTION, because the 704 mm lines ARE every eighth 88 mm
// line. Drawn from two sources they would land a fraction of a pixel apart and
// double at some zooms and not others. Here they cannot disagree: same world
// position, same arithmetic, different spacing.
//
// THE 44 mm OFFSET IS ONLY HERE. Lines sit ON the module coordinates, which is
// where parts snap — the drawn lattice IS the anchor lattice. The hover
// outline is the one thing offset by half a module, because it bounds the
// SPACE around an anchor rather than the anchor itself: an 88 mm box a part
// sits inside. That offset exists in this file and nowhere else. Nothing that
// snaps, measures or places has ever heard of it.
//
// EDGES FADE AT A CONSTANT WIDTH, measured from the footprint rather than from
// its centre. Around one module that is a circle; along a wall it is that same
// circle dragged from end to end. A radius from the centre would balloon
// around anything long, and an ellipse would stretch the fade thin at the ends
// and pile it up at the sides — the same soft edge everywhere is what reads as
// deliberate.
Shader "NEOSPACE/Ground Grid"
{
    Properties
    {
        [Header(Lattice)]
        _LineColor ("Line colour", Color) = (0.42, 0.42, 0.42, 1)
        _Opacity ("H7 opacity", Range(0, 1)) = 0.3
        _Spacing ("H7 spacing (world units)", Float) = 7.04
        _LineWidthPx ("Line width (pixels)", Range(0.5, 4)) = 1.1

        [Header(Module grid)]
        _ModuleColor ("Module line colour", Color) = (0.42, 0.42, 0.42, 1)
        _ModuleSpacing ("Module spacing (world units)", Float) = 0.88
        _ModuleOpacity ("Module opacity", Range(0, 1)) = 0.3
        _ModuleLineWidthPx ("Module line width (pixels)", Range(0.5, 4)) = 1.0
        _IslandCenter ("Island centre (world XZ)", Vector) = (0, 0, 0, 0)
        _IslandHalf ("Island half extents (world XZ)", Vector) = (8, 8, 0, 0)
        _IslandFadeStart ("Island fade start (world units)", Float) = 1
        _IslandFadeEnd ("Island fade end (world units)", Float) = 7

        [Header(World centre)]
        _OriginColor ("Origin colour", Color) = (0.25, 0.25, 0.25, 1)
        _OriginOpacity ("Origin opacity", Range(0, 1)) = 0.5
        _OriginLineWidthPx ("Origin line width (pixels)", Range(0.5, 4)) = 1.4
        _OriginFadeStart ("Origin fade start (world units)", Float) = 7.04
        _OriginFadeEnd ("Origin fade end (world units)", Float) = 21.12

        [Header(Built zone)]
        _ZoneColor ("Zone colour", Color) = (0.3, 0.3, 0.3, 1)
        _ZoneOpacity ("Zone opacity", Range(0, 1)) = 0.45
        _ZoneLineWidthPx ("Zone line width (pixels)", Range(0.5, 4)) = 1.4
        _ZoneCenter ("Zone centre (world XZ)", Vector) = (0, 0, 0, 0)
        _ZoneHalf ("Zone half extents (world XZ)", Vector) = (7.04, 7.04, 0, 0)
        _ZoneStrength ("Zone strength", Range(0, 1)) = 0

        [Header(Hover)]
        _HighlightColor ("Highlight", Color) = (0.153, 0.463, 0.918, 1)
        _HoverOpacity ("Highlight opacity", Range(0, 1)) = 0.75
        _HoverFillOpacity ("Footprint fill opacity", Range(0, 1)) = 0.18
        _HoverCenter ("Hover centre (world XZ)", Vector) = (0, 0, 0, 0)
        _HoverHalf ("Hover half extents (world XZ)", Vector) = (0.44, 0.44, 0, 0)
        _HoverFadeStart ("Hover fade start (world units)", Float) = 0.5
        _HoverFadeEnd ("Hover fade end (world units)", Float) = 5
        _HoverStrength ("Hover strength", Range(0, 1)) = 0
        _OutlineWidthPx ("Hitbox outline width (pixels)", Range(0.5, 6)) = 2
        _OutlineDash ("Hitbox dash cycle (world units)", Float) = 0.44

        [Header(Fog of vision)]
        _FadeCenter ("Fade centre (world XZ)", Vector) = (0, 0, 0, 0)
        _FadeStart ("Fade start (world units)", Float) = 14
        _FadeEnd ("Fade end (world units)", Float) = 26

        [Header(The real ground)]
        _BoundsCenter ("Ground centre (world XZ)", Vector) = (0, 0, 0, 0)
        _BoundsHalf ("Ground half extents (world XZ)", Vector) = (500, 500, 0, 0)
        _BoundsFade ("Ground edge fade (world units)", Float) = 7.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            // After the shadow catcher at 2800, so a shadow lies under the
            // grid rather than over it.
            "Queue" = "Transparent-150"
        }

        Pass
        {
            Name "GroundGrid"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/NeospaceGroundClip.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LineColor;
                float _Opacity;
                float _Spacing;
                float _LineWidthPx;

                float4 _ModuleColor;
                float _ModuleSpacing;
                float _ModuleOpacity;
                float _ModuleLineWidthPx;
                float4 _IslandCenter;
                float4 _IslandHalf;
                float _IslandFadeStart;
                float _IslandFadeEnd;

                float4 _OriginColor;
                float _OriginOpacity;
                float _OriginLineWidthPx;
                float _OriginFadeStart;
                float _OriginFadeEnd;

                float4 _ZoneColor;
                float _ZoneOpacity;
                float _ZoneLineWidthPx;
                float4 _ZoneCenter;
                float4 _ZoneHalf;
                float _ZoneStrength;

                float4 _HighlightColor;
                float _HoverOpacity;
                float _HoverFillOpacity;
                float4 _HoverCenter;
                float4 _HoverHalf;
                float _HoverFadeStart;
                float _HoverFadeEnd;
                float _HoverStrength;
                float _OutlineWidthPx;
                float _OutlineDash;

                float4 _FadeCenter;
                float _FadeStart;
                float _FadeEnd;

                float4 _BoundsCenter;
                float4 _BoundsHalf;
                float _BoundsFade;
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

            /// <summary>
            /// Line coverage for one lattice, antialiased from screen-space
            /// derivatives, and faded out once it can no longer be resolved.
            ///
            /// The width is measured in PIXELS, so a line is the same
            /// thickness at every zoom — a textured grid cannot do that, and
            /// it is why the old one thinned away as the camera pulled back.
            /// </summary>
            float Lattice(float2 ground, float spacing, float widthPx)
            {
                float2 cells = ground / max(spacing, 1e-4);

                // How much of a cell one pixel covers. Everything below is a
                // comparison against this.
                float2 perPixel = max(fwidth(cells), 1e-6);

                // Distance to the nearest line, in cells.
                float2 toLine = abs(frac(cells + 0.5) - 0.5);

                float2 halfWidth = widthPx * 0.5 * perPixel;
                float2 covered = 1.0 - smoothstep(halfWidth - perPixel * 0.5,
                                                  halfWidth + perPixel * 0.5,
                                                  toLine);

                // Either direction lights the pixel: a crossing is not twice
                // as bright as a line.
                float lines = max(covered.x, covered.y);

                // Where the lattice can no longer be resolved, stop drawing
                // it. Without this both masks saturate and the distance fills
                // in as a solid sheet of line colour — a failure that looks
                // like fog and is not.
                float merged = max(perPixel.x, perPixel.y) * widthPx;
                return lines * (1.0 - smoothstep(0.35, 1.0, merged));
            }

            /// <summary>
            /// Distance from a point to a rectangle: negative inside, zero on
            /// the edge, positive outside — and outside it is the true
            /// distance, including round corners. This is what makes every
            /// fade the same width all the way round a footprint of any shape.
            /// </summary>
            float BoxDistance(float2 p, float2 center, float2 halfSize)
            {
                float2 d = abs(p - center) - halfSize;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            /// <summary>
            /// Soft falloff outward from a rectangle's edge: full strength out
            /// to <paramref name="start"/> past the edge, gone by
            /// <paramref name="end"/>. Two numbers rather than one, so the
            /// solid part and the gradient can be set apart from each other.
            /// </summary>
            float BoxFalloff(float2 p, float2 center, float2 halfSize, float start, float end)
            {
                return 1.0 - smoothstep(start, max(end, start + 1e-4),
                                        BoxDistance(p, center, halfSize));
            }

            /// <summary>
            /// World units per screen pixel ALONG ONE COORDINATE — measured in
            /// the direction that coordinate changes fastest on screen.
            ///
            /// A line x = c is crossed by moving in x, so its width in pixels
            /// is set by how fast x changes across the screen and by nothing
            /// else. The previous version took ONE number for the whole pixel,
            /// the larger of its two footprints. That is right looking straight
            /// down and badly wrong at a low angle, where a pixel covers a long
            /// way INTO the scene and very little ACROSS it: every line running
            /// into the scene was drawn as wide as that long footprint, which
            /// is the thick, smeared edge the built zone showed at shallow
            /// angles.
            ///
            /// Still taken from the position and never from a distance field,
            /// so a rectangle's corner cannot make it jump.
            /// </summary>
            float AxisPerPixel(float coordinate)
            {
                return max(length(float2(ddx(coordinate), ddy(coordinate))), 1e-6);
            }

            /// <summary>Coverage of a line, given the distance to it in PIXELS.</summary>
            float StrokeCoverage(float pixels, float widthPx)
            {
                float halfWidth = widthPx * 0.5;
                return 1.0 - smoothstep(halfWidth - 0.5, halfWidth + 0.5, pixels);
            }

            /// <summary>
            /// 1 inside a segment's span, fading out over one pixel past half a
            /// stroke beyond its end — so two sides meeting at a corner overlap
            /// exactly enough to close it.
            /// </summary>
            float SpanCoverage(float pixelsBeyond, float widthPx)
            {
                float halfWidth = widthPx * 0.5;
                return 1.0 - smoothstep(halfWidth, halfWidth + 1.0, pixelsBeyond);
            }

            /// <summary>
            /// A dash pattern along one coordinate, on a cycle measured in
            /// WORLD units so the dashes stay put on the ground as the camera
            /// orbits. Goes solid once a dash shrinks below a pixel rather than
            /// shimmering.
            /// </summary>
            float DashAlong(float coordinate, float dashLength)
            {
                float cycle = max(dashLength, 1e-3);
                float soft = max(AxisPerPixel(coordinate) / cycle, 1e-5);
                float phase = frac(coordinate / cycle);
                // 55% on, 45% off, one pixel of softness at each end.
                float dash = smoothstep(0.0, soft, phase) *
                             (1.0 - smoothstep(0.55 - soft, 0.55, phase));
                float resolved = 1.0 - smoothstep(0.15, 0.5, soft);
                return lerp(1.0, dash, resolved);
            }

            /// <summary>
            /// A rectangle's four sides, each measured along its OWN axis, so
            /// every side stays the width it was asked for at any angle.
            ///
            /// The sides at x = centre ± half.x are lines of constant x: their
            /// distance is in x and their dashes run along z. The other pair is
            /// the other way round. Each pair is kept to the rectangle's span.
            ///
            /// dashLength of 0 draws solid. The dash is always computed and
            /// blended in, never branched on, because screen-space derivatives
            /// inside a branch are undefined on some of the GPUs WebGL runs on.
            /// </summary>
            float RectStroke(float2 p, float2 center, float2 halfSize, float widthPx, float dashLength)
            {
                // "stroke", never "line": line is a reserved word in HLSL — it
                // names a geometry-shader primitive type — and using it for a
                // local once turned the whole shader magenta.
                float2 q = abs(p - center) - halfSize;
                float perX = AxisPerPixel(p.x);
                float perY = AxisPerPixel(p.y);

                float sidesX = StrokeCoverage(abs(q.x) / perX, widthPx)
                               * SpanCoverage(max(q.y, 0.0) / perY, widthPx);
                float sidesY = StrokeCoverage(abs(q.y) / perY, widthPx)
                               * SpanCoverage(max(q.x, 0.0) / perX, widthPx);

                float dashed = step(1e-5, dashLength);
                sidesX *= lerp(1.0, DashAlong(p.y, dashLength), dashed);
                sidesY *= lerp(1.0, DashAlong(p.x, dashLength), dashed);

                return max(sidesX, sidesY);
            }

            /// <summary>The built zone: a solid rectangle.</summary>
            float BoxStroke(float2 p, float2 center, float2 halfSize, float widthPx)
            {
                return RectStroke(p, center, halfSize, widthPx, 0.0);
            }

            /// <summary>The hitbox outline: the same rectangle, dashed.</summary>
            float BoxOutline(float2 p, float2 center, float2 halfSize, float widthPx, float dashLength)
            {
                return RectStroke(p, center, halfSize, widthPx, dashLength);
            }

            /// <summary>
            /// A straight line where <paramref name="coordinate"/> is zero,
            /// measured along that coordinate like every other line here.
            /// </summary>
            float LineStroke(float coordinate, float widthPx)
            {
                return StrokeCoverage(abs(coordinate) / AxisPerPixel(coordinate), widthPx);
            }

            /// <summary>
            /// The world centre: the two 704 mm lattice lines that pass
            /// through the origin, drawn stronger near it and fading back into
            /// the lattice along their length. No dot, no arrows, no colour per
            /// axis — the origin is where the two emphasised lines cross.
            ///
            /// Each line fades by distance ALONG itself, measured from the
            /// origin, so all four arms are the same length.
            /// </summary>
            float OriginCross(float2 p, float widthPx, float fadeStart, float fadeEnd)
            {
                float end = max(fadeEnd, fadeStart + 1e-4);
                // Runs along X: sits at z = 0, fades by |x|.
                float alongX = LineStroke(p.y, widthPx)
                               * (1.0 - smoothstep(fadeStart, end, abs(p.x)));
                // Runs along Z: sits at x = 0, fades by |z|.
                float alongZ = LineStroke(p.x, widthPx)
                               * (1.0 - smoothstep(fadeStart, end, abs(p.y)));
                return max(alongX, alongZ);
            }

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                // Held in front of the near plane in the isometric view, so the
                // grid is never cut off at the bottom of the screen when zoomed
                // out. See NeospaceGroundClip.hlsl for why that is exact.
                OUT.positionCS = NeospaceKeepGroundInFront(positions.positionCS);
                OUT.positionWS = positions.positionWS;
                return OUT;
            }

            half4 Fragment(Varyings IN, out float depth : SV_Depth) : SV_Target
            {
                // The true depth of this point on the ground, written before
                // anything can discard: the vertex stage may have bent the
                // interpolated depth to keep the ground in front of the near
                // plane, and without this the grid would draw over parts.
                depth = NeospaceGroundDepth(IN.positionWS);

                float2 ground = IN.positionWS.xz;

                // --- what the viewer can see -----------------------------
                float radius = length(ground - _FadeCenter.xy);
                float vision = 1.0 - smoothstep(_FadeStart, _FadeEnd, radius);

                float2 toBoundary = _BoundsHalf.xy - abs(ground - _BoundsCenter.xy);
                float onGround = saturate(min(toBoundary.x, toBoundary.y) / max(_BoundsFade, 1e-4));

                float visible = vision * onGround;
                if (visible <= 0.002)
                    discard;

                // --- the two lattices, each with its own thickness --------
                float h7 = Lattice(ground, _Spacing, _LineWidthPx);
                float modules = Lattice(ground, _ModuleSpacing, _ModuleLineWidthPx);

                // --- the three layers ------------------------------------
                float island = BoxFalloff(ground, _IslandCenter.xy, _IslandHalf.xy,
                                         _IslandFadeStart, _IslandFadeEnd);

                float hover = BoxFalloff(ground, _HoverCenter.xy, _HoverHalf.xy,
                                         _HoverFadeStart, _HoverFadeEnd) * _HoverStrength;
                float outline = BoxOutline(ground, _HoverCenter.xy, _HoverHalf.xy,
                                           _OutlineWidthPx, _OutlineDash) * _HoverStrength;
                // Uniform colour inside the exact footprint, antialiased only
                // across its edge. Unlike the highlighted module lines around
                // it, this does not fade across the interior.
                float footprintDistance = BoxDistance(
                    ground, _HoverCenter.xy, _HoverHalf.xy);
                float footprintEdge = max(fwidth(footprintDistance), 1e-5);
                float fill = (1.0 - smoothstep(-footprintEdge, footprintEdge,
                                               footprintDistance)) * _HoverStrength;

                // The built zone. Its edges sit ON 704 mm lattice lines — the
                // controller snaps them there — so this does not draw a new
                // line so much as pick four existing ones and let them speak
                // up. Solid, where the hitbox is dashed: the dashes mean "about
                // to be", and this is what already is.
                float zone = BoxStroke(ground, _ZoneCenter.xy, _ZoneHalf.xy, _ZoneLineWidthPx)
                             * _ZoneStrength;

                float origin = OriginCross(ground, _OriginLineWidthPx,
                                           _OriginFadeStart, _OriginFadeEnd);

                float aLattice = h7 * _Opacity;
                float aModule = modules * island * _ModuleOpacity;
                float aZone = zone * _ZoneOpacity;
                float aOrigin = origin * _OriginOpacity;
                // The outline is the answer to "exactly where will this go",
                // so it is added at full strength rather than washed into the
                // gradient around it.
                float aHover = saturate(fill * _HoverFillOpacity
                                        + modules * hover * _HoverOpacity
                                        + outline);

                aLattice *= visible;
                aModule *= visible;
                aZone *= visible;
                aOrigin *= visible;
                aHover *= visible;

                float total = aLattice + aModule + aZone + aOrigin + aHover;
                if (total <= 0.002)
                    discard;

                // A weighted average of the layers, by how much of this pixel
                // each one claimed — so the highlight takes over smoothly
                // instead of switching, and each layer keeps its own colour.
                float3 rgb = (_LineColor.rgb * aLattice
                              + _ModuleColor.rgb * aModule
                              + _ZoneColor.rgb * aZone
                              + _OriginColor.rgb * aOrigin
                              + _HighlightColor.rgb * aHover) / total;

                return half4(rgb, saturate(total));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
