using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Evo.UI
{
    /// <summary>
    /// Renders anti-aliased rounded rectangles via a SDF shader.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
    [HelpURL(Constants.HelpUrl + "procedural-rect")]
    [AddComponentMenu("Evo/UI/Graphics/Procedural Rect (Preview)")]
    public class ProceduralRect : MaskableGraphic, ICanvasRaycastFilter
    {
        // Graphic
        [Tooltip("The source sprite image.")]
        public Sprite sprite;
        [Tooltip("Image scale mode.\n\nCover: fills the rect.\nFit: preserves aspect ratio with gaps.\nStretch: ignores aspect ratio.")]
        public ScaleMode scaleMode = ScaleMode.Cover;
        [Tooltip("Raycast detection method.\n\nStandard: use the rect bounds.\nShape: use the precise rounded/clipped geometry.")]
        public RaycastMode raycastMode = RaycastMode.Standard;

        // Clipping
        [Tooltip("Clipping method.")]
        public ClipMethod clipMethod = ClipMethod.None;
        [Tooltip("Clip origin direction.")]
        [Range(0, 3)] public int clipOrigin = 0;
        [Tooltip("How much of the shape is visible.")]
        [Range(0f, 1f)] public float clipAmount = 1f;
        [Tooltip("Clockwise clipping direction.")]
        public bool clipClockwise = true;

        // Fill
        [Tooltip("Fill the interior. Disable to render only the outline.")]
        public bool fillCenter = true;
        [Tooltip("Extra edge softness in pixels beyond the default 1-px AA band.")]
        [Range(0f, 32f)] public float softness = 0;
        [Tooltip("Fill color mode.")]
        public ColorMode fillColorMode = ColorMode.Base;
        [Tooltip("Fill solid color.")]
        public Color fillColor = Color.white;
        [Tooltip("Fill gradient.")]
        public Gradient fillGradient = DefaultGradient();
        [Tooltip("Fill gradient angle in degrees.")]
        [Range(0f, 360f)] public float fillGradientAngle = 0f;
        [Tooltip("Fill gradient zoom. > 1 compresses, < 1 stretches.")]
        [Range(0.1f, 5f)] public float fillGradientZoom = 1f;
        [Tooltip("Swap the fill gradient start and end.")]
        public bool fillGradientReverse = false;

        // Outline
        [Tooltip("Outline width in pixels, drawn inward from the shape edge.")]
        [Min(0f)] public float outlineWidth = 0f;
        [Tooltip("Outline color mode.")]
        public ColorMode outlineColorMode = ColorMode.Custom;
        [Tooltip("Outline solid color.")]
        public Color outlineColor = Color.gray;
        [Tooltip("Outline gradient.")]
        public Gradient outlineGradient = DefaultGradient();
        [Tooltip("Outline gradient angle in degrees.")]
        [Range(0f, 360f)] public float outlineGradientAngle = 0f;
        [Tooltip("Outline gradient zoom.")]
        [Range(0.1f, 4)] public float outlineGradientZoom = 1f;
        [Tooltip("Swap the outline gradient start and end.")]
        public bool outlineGradientReverse = false;

        // Corners
        [Tooltip("Corner radius unit.")]
        public RadiusMode radiusMode = RadiusMode.Pixels;
        [Tooltip("Give each corner a different radius.")]
        public bool independentCorners = false;
        [Tooltip("Enable squircle (smooth) corners instead of standard rounded corners.")]
        public bool squircleCorners = false;
        public Vector4 cornerRadius = Vector4.zero;
        [Tooltip("Syncs this radius to fit perfectly inside another ProceduralRect based on padding.")]
        public RadiusSyncMode radiusSyncMode = RadiusSyncMode.None;
        public ProceduralRect radiusSyncTarget;

        // Inner Shadow
        public Vector2 innerShadowOffset = Vector2.zero;
        [Tooltip("Expansion or contraction size of the inner shadow.")]
        [Min(0f)] public float innerShadowSize = 0f;
        [Tooltip("Blur radius.")]
        [Min(0f)] public float innerShadowSoftness = 0f;
        [Tooltip("Inner Shadow color mode.")]
        public ColorMode innerShadowColorMode = ColorMode.Custom;
        [Tooltip("Inner Shadow solid color.")]
        public Color innerShadowColor = new(0f, 0f, 0f, 0.5f);
        [Tooltip("Inner Shadow gradient.")]
        public Gradient innerShadowGradient = DefaultGradient();
        [Tooltip("Inner Shadow gradient angle in degrees.")]
        [Range(0f, 360f)] public float innerShadowGradientAngle = 0f;
        [Tooltip("Inner Shadow gradient zoom.")]
        [Range(0.1f, 4f)] public float innerShadowGradientZoom = 1f;
        [Tooltip("Swap the inner shadow gradient start and end.")]
        public bool innerShadowGradientReverse = false;

        // Outer Shadow
        public Vector2 outerShadowOffset = Vector2.zero;
        [Tooltip("Expansion or contraction size of the outer shadow.")]
        [Min(0f)] public float outerShadowSize = 0f;
        [Tooltip("Blur radius.")]
        [Min(0f)] public float outerShadowSoftness = 0f;
        [Tooltip("Outer Shadow color mode.")]
        public ColorMode outerShadowColorMode = ColorMode.Custom;
        [Tooltip("Outer Shadow solid color.")]
        public Color outerShadowColor = new(0f, 0f, 0f, 0.5f);
        [Tooltip("Outer Shadow gradient.")]
        public Gradient outerShadowGradient = DefaultGradient();
        [Tooltip("Outer Shadow gradient angle in degrees.")]
        [Range(0f, 360f)] public float outerShadowGradientAngle = 0f;
        [Tooltip("Outer Shadow gradient zoom.")]
        [Range(0.1f, 4f)] public float outerShadowGradientZoom = 1f;
        [Tooltip("Swap the outer shadow gradient start and end.")]
        public bool outerShadowGradientReverse = false;

        // Enums
        public enum ColorMode { Base = 0, Custom = 1, Gradient = 2 }
        public enum ClipMethod { None, Horizontal, Vertical, Radial360 }
        public enum RadiusMode { Pixels = 0, Percentage = 1 }
        public enum ScaleMode { Stretch = 0, Cover = 1, Fit = 2 }
        public enum RaycastMode { None = 0, Standard = 1, Shape = 2 }
        public enum RadiusSyncMode { None = 0, MatchParent = 1, MatchTarget = 2 }

        // Constants
        const string ShaderName = "Evo/UI/Procedural Rect";
        const int GradientRes = 256;

        // Shader
        [SerializeField, HideInInspector] Shader embeddedShader;

        // Property IDs
        static readonly int ID_FillColor = Shader.PropertyToID("_FillColor");
        static readonly int ID_FillColorMode = Shader.PropertyToID("_FillColorMode");
        static readonly int ID_FillGradientTex = Shader.PropertyToID("_FillGradientTex");
        static readonly int ID_FillGradientAngle = Shader.PropertyToID("_FillGradientAngle");
        static readonly int ID_FillGradientZoom = Shader.PropertyToID("_FillGradientZoom");
        static readonly int ID_FillGradientReverse = Shader.PropertyToID("_FillGradientReverse");

        static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int ID_OutlineColorMode = Shader.PropertyToID("_OutlineColorMode");
        static readonly int ID_OutlineGradientTex = Shader.PropertyToID("_OutlineGradientTex");
        static readonly int ID_OutlineGradientAngle = Shader.PropertyToID("_OutlineGradientAngle");
        static readonly int ID_OutlineGradientZoom = Shader.PropertyToID("_OutlineGradientZoom");
        static readonly int ID_OutlineGradientReverse = Shader.PropertyToID("_OutlineGradientReverse");

        static readonly int ID_InnerShadowColor = Shader.PropertyToID("_InnerShadowColor");
        static readonly int ID_InnerShadowColorMode = Shader.PropertyToID("_InnerShadowColorMode");
        static readonly int ID_InnerShadowGradientTex = Shader.PropertyToID("_InnerShadowGradientTex");
        static readonly int ID_InnerShadowGradientAngle = Shader.PropertyToID("_InnerShadowGradientAngle");
        static readonly int ID_InnerShadowGradientZoom = Shader.PropertyToID("_InnerShadowGradientZoom");
        static readonly int ID_InnerShadowGradientReverse = Shader.PropertyToID("_InnerShadowGradientReverse");

        static readonly int ID_OuterShadowColor = Shader.PropertyToID("_OuterShadowColor");
        static readonly int ID_OuterShadowColorMode = Shader.PropertyToID("_OuterShadowColorMode");
        static readonly int ID_OuterShadowGradientTex = Shader.PropertyToID("_OuterShadowGradientTex");
        static readonly int ID_OuterShadowGradientAngle = Shader.PropertyToID("_OuterShadowGradientAngle");
        static readonly int ID_OuterShadowGradientZoom = Shader.PropertyToID("_OuterShadowGradientZoom");
        static readonly int ID_OuterShadowGradientReverse = Shader.PropertyToID("_OuterShadowGradientReverse");

        // Cache
        ProceduralRect cachedParentRect;
        Material pooledMaterial;
        MaterialKey currentMatKey;
        int fillTexKey, outlineTexKey, innerShadowTexKey, outerShadowTexKey;
        Canvas.WillRenderCanvases syncColorSpaceDelegate;

        // Helpers
        bool lastIsLinear;
        static Texture2D whiteTex;

        // Single static array prevents gc per-gradient refresh
        static readonly Color32[] gradientPixels32 = new Color32[GradientRes];

        bool ActiveSquircleCorners
        {
            get
            {
                if (radiusSyncMode != RadiusSyncMode.None)
                {
                    ProceduralRect target = radiusSyncMode == RadiusSyncMode.MatchParent ? cachedParentRect : radiusSyncTarget;
                    if (target != null && target != this)
                        return target.squircleCorners;
                }

                return squircleCorners;
            }
        }

        public override bool raycastTarget
        {
            get => base.raycastTarget;
            set
            {
                base.raycastTarget = value;

                if (!value) { raycastMode = RaycastMode.None; }
                else if (raycastMode == RaycastMode.None) raycastMode = RaycastMode.Standard;
            }
        }

        public override Texture mainTexture
        {
            get
            {
                if (sprite != null && sprite.texture != null)
                    return sprite.texture;

                return whiteTex ? whiteTex : (whiteTex = Texture2D.whiteTexture);
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                if (pooledMaterial == null)
                    ResolveMaterialAndTextures();

                return pooledMaterial != null ? pooledMaterial : base.defaultMaterial;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            lastIsLinear = IsCanvasLinear();
            syncColorSpaceDelegate ??= SyncColorSpaceIfChanged;
            Canvas.willRenderCanvases += syncColorSpaceDelegate;

            EnsureCanvasChannels();
            UpdateCachedParent();
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (syncColorSpaceDelegate != null)
                Canvas.willRenderCanvases -= syncColorSpaceDelegate;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            ReleasePooledMaterial(currentMatKey);
            ReleasePooledTexture(fillTexKey);
            ReleasePooledTexture(outlineTexKey);
            ReleasePooledTexture(innerShadowTexKey);
            ReleasePooledTexture(outerShadowTexKey);

            currentMatKey = default;
            pooledMaterial = null;
            fillTexKey = outlineTexKey = innerShadowTexKey = outerShadowTexKey = 0;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();

            EnsureCanvasChannels();
            UpdateCachedParent();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            SetVerticesDirty();
        }

        protected override void UpdateMaterial()
        {
            if (!IsActive())
                return;

            EnsureCanvasChannels();
            ResolveMaterialAndTextures();

            base.UpdateMaterial();
        }

        void UpdateCachedParent()
        {
            if (radiusSyncMode == RadiusSyncMode.MatchParent)
                cachedParentRect = transform.parent != null ? transform.parent.GetComponent<ProceduralRect>() : null;
            else
                cachedParentRect = null;
        }

        struct VertexGenerator
        {
            public Rect rect;
            public float uvMinX, uvMaxX, uvMinY, uvMaxY;
            public Vector3 normal;
            public Vector4 tangent;
            public Color32 color;
            public Vector4 uv1, uv2, uv3;

            public UIVertex Generate(float x, float y, float sx, float sy)
            {
                // Calculate correct 0-1 percentage mapping relative to the rect boundaries (not the expanded bounds)
                float tx = rect.width > 0 ? (x - rect.xMin) / rect.width : 0f;
                float ty = rect.height > 0 ? (y - rect.yMin) / rect.height : 0f;

                // Extrapolate the mapped Sprite UV out to the expanded edge of the vertex
                float uvX = Mathf.LerpUnclamped(uvMinX, uvMaxX, tx);
                float uvY = Mathf.LerpUnclamped(uvMinY, uvMaxY, ty);

                return new UIVertex
                {
                    position = new Vector3(x, y),
                    normal = normal,
                    tangent = tangent,
                    color = color,
                    uv0 = new Vector4(sx, sy, uvX, uvY), // Pass SDF coords (XY) and Sprite UVs (ZW) together
                    uv1 = uv1,
                    uv2 = uv2,
                    uv3 = uv3
                };
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width < 0.001f || rect.height < 0.001f) { return; }

            // Handle Preserve Aspect Fit
            if (sprite != null && scaleMode == ScaleMode.Fit) { rect = CalculateFitRect(rect, sprite); }

            float maxOffset = Mathf.Max(Mathf.Abs(outerShadowOffset.x), Mathf.Abs(outerShadowOffset.y));
            float shadowExpand = Mathf.Max(0, outerShadowSize) + outerShadowSoftness + maxOffset;
            float expand = shadowExpand + softness + 2f;

            float xMin = rect.xMin - expand;
            float xMax = rect.xMax + expand;
            float yMin = rect.yMin - expand;
            float yMax = rect.yMax + expand;
            float cx = rect.center.x;
            float cy = rect.center.y;

            // Pack Squircle + Fill boolean
            float fillAndSq = (fillCenter ? 1f : 0f) + (ActiveSquircleCorners ? 2f : 0f);

            Vector4 uv1 = new(rect.width, rect.height, softness, fillAndSq);
            Vector4 uv2 = new(outlineWidth, innerShadowSoftness, clipAmount, PackFillConfig());
            Vector4 uv3 = GetRadiiPixels(rect);
            Vector4 tangent = new(outerShadowOffset.x, outerShadowOffset.y, outerShadowSize, outerShadowSoftness);
            Vector3 normal = new(innerShadowOffset.x, innerShadowOffset.y, innerShadowSize);

            Color32 c32 = color;
            Vector4 spriteUV = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);

            float uvMinX = spriteUV.x;
            float uvMaxX = spriteUV.z;
            float uvMinY = spriteUV.y;
            float uvMaxY = spriteUV.w;

            // Handle Preserve Aspect Cover (Crop UV mapping to fit bounds)
            if (sprite != null && scaleMode == ScaleMode.Cover && rect.width > 0.001f && rect.height > 0.001f && sprite.rect.height > 0.001f)
            {
                float rectAspect = rect.width / rect.height;
                float spriteAspect = sprite.rect.width / sprite.rect.height;

                if (spriteAspect > rectAspect)
                {
                    // Sprite is relatively wider than the rect. Crop left and right.
                    float scale = rectAspect / spriteAspect;
                    float uvWidth = (spriteUV.z - spriteUV.x) * scale;
                    float uvCenter = (spriteUV.x + spriteUV.z) * 0.5f;
                    uvMinX = uvCenter - uvWidth * 0.5f;
                    uvMaxX = uvCenter + uvWidth * 0.5f;
                }
                else
                {
                    // Sprite is relatively taller than the rect. Crop top and bottom.
                    float scale = spriteAspect / rectAspect;
                    float uvHeight = (spriteUV.w - spriteUV.y) * scale;
                    float uvCenter = (spriteUV.y + spriteUV.w) * 0.5f;
                    uvMinY = uvCenter - uvHeight * 0.5f;
                    uvMaxY = uvCenter + uvHeight * 0.5f;
                }
            }

            var vGen = new VertexGenerator
            {
                rect = rect,
                uvMinX = uvMinX,
                uvMaxX = uvMaxX,
                uvMinY = uvMinY,
                uvMaxY = uvMaxY,
                normal = normal,
                tangent = tangent,
                color = c32,
                uv1 = uv1,
                uv2 = uv2,
                uv3 = uv3
            };

            vh.AddVert(vGen.Generate(xMin, yMin, xMin - cx, yMin - cy));
            vh.AddVert(vGen.Generate(xMin, yMax, xMin - cx, yMax - cy));
            vh.AddVert(vGen.Generate(xMax, yMax, xMax - cx, yMax - cy));
            vh.AddVert(vGen.Generate(xMax, yMin, xMax - cx, yMin - cy));

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        float GetSDF(Vector2 p, Vector2 b, Vector4 r, bool squircle)
        {
            float radius;

            if (p.x > 0 && p.y > 0) { radius = r.x; } // TopRight
            else if (p.x > 0 && p.y <= 0) { radius = r.y; } // BottomRight
            else if (p.x <= 0 && p.y <= 0) { radius = r.w; } // BottomLeft
            else { radius = r.z; } // TopLeft

            Vector2 q = new(Mathf.Abs(p.x) - b.x + radius, Mathf.Abs(p.y) - b.y + radius);

            float maxQx = Mathf.Max(q.x, 0f);
            float maxQy = Mathf.Max(q.y, 0f);
            float length;

            if (!squircle) { length = Mathf.Sqrt(maxQx * maxQx + maxQy * maxQy); }
            else
            {
                float sqX = maxQx * maxQx;
                float sqY = maxQy * maxQy;
                length = Mathf.Sqrt(Mathf.Sqrt(sqX * sqX + sqY * sqY));
            }

            return Mathf.Min(Mathf.Max(q.x, q.y), 0.0f) + length - radius;
        }

        Rect CalculateFitRect(Rect rect, Sprite sprite)
        {
            // Safety guard: Prevents Divide-by-Zero / NaN geometry if an invalid/empty sprite is passed
            if (sprite == null || sprite.rect.height <= 0.001f || sprite.rect.width <= 0.001f || rect.height <= 0.001f)
                return rect;

            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect = rect.width / rect.height;

            if (spriteAspect > rectAspect)
            {
                float newHeight = rect.width / spriteAspect;
                return new Rect(rect.x, rect.center.y - newHeight * 0.5f, rect.width, newHeight);
            }
            else
            {
                float newWidth = rect.height * spriteAspect;
                return new Rect(rect.center.x - newWidth * 0.5f, rect.y, newWidth, rect.height);
            }
        }

        void SyncColorSpaceIfChanged()
        {
            if (this == null || !IsActive())
                return;

            bool isLinear = IsCanvasLinear();
            if (isLinear != lastIsLinear)
            {
                lastIsLinear = isLinear;
                SetMaterialDirty();
            }
        }

        void EnsureCanvasChannels()
        {
            if (canvas != null)
            {
                var required = AdditionalCanvasShaderChannels.TexCoord1 |
                               AdditionalCanvasShaderChannels.TexCoord2 |
                               AdditionalCanvasShaderChannels.TexCoord3 |
                               AdditionalCanvasShaderChannels.Tangent |
                               AdditionalCanvasShaderChannels.Normal;

                if ((canvas.additionalShaderChannels & required) != required)
                    canvas.additionalShaderChannels |= required;
            }
        }

        public Vector4 GetRadiiPixels(Rect rect)
        {
            // Intercept manual radii if we are syncing
            if (radiusSyncMode != RadiusSyncMode.None)
            {
                ProceduralRect target = radiusSyncMode == RadiusSyncMode.MatchParent ? cachedParentRect : radiusSyncTarget;

                if (target != null && target != this)
                    return CalculateSyncedRadii(target);
            }

            float maxR = Mathf.Min(rect.width, rect.height) * 0.5f;
            float P(float val) => radiusMode == RadiusMode.Percentage ? val * 0.01f * maxR : val;

            if (!independentCorners)
            {
                float uniformRadius = P(cornerRadius.x);
                return new Vector4(uniformRadius, uniformRadius, uniformRadius, uniformRadius);
            }

            // TopRight, BottomRight, BottomLeft, TopLeft mapping
            return new Vector4(P(cornerRadius.y), P(cornerRadius.z), P(cornerRadius.x), P(cornerRadius.w));
        }

        Vector4 CalculateSyncedRadii(ProceduralRect target)
        {
            // Get the target's final resolved radii
            Vector4 pRadii = target.GetRadiiPixels(target.rectTransform.rect);

            // Calculate local bounds using matrices
            Rect cRect = rectTransform.rect;
            Vector2 min = cRect.min;
            Vector2 max = cRect.max;

            Matrix4x4 childToTarget = target.transform.worldToLocalMatrix * transform.localToWorldMatrix;

            Vector3 bl = childToTarget.MultiplyPoint3x4(new Vector3(min.x, min.y, 0));
            Vector3 tl = childToTarget.MultiplyPoint3x4(new Vector3(min.x, max.y, 0));
            Vector3 tr = childToTarget.MultiplyPoint3x4(new Vector3(max.x, max.y, 0));
            Vector3 br = childToTarget.MultiplyPoint3x4(new Vector3(max.x, min.y, 0));

            float minX = Mathf.Min(bl.x, tl.x, tr.x, br.x);
            float maxX = Mathf.Max(bl.x, tl.x, tr.x, br.x);
            float minY = Mathf.Min(bl.y, tl.y, tr.y, br.y);
            float maxY = Mathf.Max(bl.y, tl.y, tr.y, br.y);

            // Determine precise padding on all 4 sides
            Rect pRect = target.rectTransform.rect;
            float padLeft = minX - pRect.xMin;
            float padRight = pRect.xMax - maxX;
            float padBottom = minY - pRect.yMin;
            float padTop = pRect.yMax - maxY;

            // Calculate final radii: R_inner = max(0, R_outer - min(adjacentPaddingX, adjacentPaddingY))
            float outTR = Mathf.Max(0, pRadii.x - Mathf.Min(padTop, padRight));
            float outBR = Mathf.Max(0, pRadii.y - Mathf.Min(padBottom, padRight));
            float outTL = Mathf.Max(0, pRadii.z - Mathf.Min(padTop, padLeft));
            float outBL = Mathf.Max(0, pRadii.w - Mathf.Min(padBottom, padLeft));

            return new Vector4(outTR, outBR, outTL, outBL);
        }

        static int ComputeGradientHash(Gradient g)
        {
            if (g == null)
                return 0;

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + g.mode.GetHashCode();
                hash = hash * 31 + g.Evaluate(0.0f).GetHashCode();
                hash = hash * 31 + g.Evaluate(0.25f).GetHashCode();
                hash = hash * 31 + g.Evaluate(0.5f).GetHashCode();
                hash = hash * 31 + g.Evaluate(0.75f).GetHashCode();
                hash = hash * 31 + g.Evaluate(1.0f).GetHashCode();

                return hash;
            }
        }

        int GetGradientKey(Gradient g, bool isLinear)
        {
            if (g == null)
                return 0;

            return unchecked(ComputeGradientHash(g) * 31 + isLinear.GetHashCode());
        }

        bool IsCanvasLinear()
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                return false;

            if (canvas != null)
                return !canvas.vertexColorAlwaysGammaSpace;

            return true;
        }

        Color SyncColorPrecision(Color col, bool isLinear)
        {
            // Snaps precision to exactly 8-bits before color space logic.
            // This guarantees the Material vectors will perfectly match the CanvasRenderer's
            // vertex color conversion quantization limits, eliminating sub-pixel color mismatches.
            Color32 c32 = col;
            return isLinear ? ((Color)c32).linear : (Color)c32;
        }

        void ResolveMaterialAndTextures()
        {
            bool isLinear = IsCanvasLinear();
            Shader shader = embeddedShader ? embeddedShader : Shader.Find(ShaderName);

            int newFillTexKey = fillColorMode == ColorMode.Gradient ? GetGradientKey(fillGradient, isLinear) : 0;
            int newOutlineTexKey = outlineColorMode == ColorMode.Gradient && outlineWidth > 0 ? GetGradientKey(outlineGradient, isLinear) : 0;
            int newInnerShadowTexKey = innerShadowColorMode == ColorMode.Gradient && (innerShadowSize != 0 || innerShadowSoftness > 0 || innerShadowOffset != Vector2.zero) ? GetGradientKey(innerShadowGradient, isLinear) : 0;
            int newOuterShadowTexKey = outerShadowColorMode == ColorMode.Gradient && (outerShadowSize != 0 || outerShadowSoftness > 0 || outerShadowOffset != Vector2.zero) ? GetGradientKey(outerShadowGradient, isLinear) : 0;

            if (fillTexKey != newFillTexKey || (newFillTexKey != 0 && !texPool.ContainsKey(newFillTexKey)))
            {
                ReleasePooledTexture(fillTexKey);
                fillTexKey = newFillTexKey;
                AcquirePooledTexture(newFillTexKey, fillGradient, isLinear);
            }

            if (outlineTexKey != newOutlineTexKey || (newOutlineTexKey != 0 && !texPool.ContainsKey(newOutlineTexKey)))
            {
                ReleasePooledTexture(outlineTexKey);
                outlineTexKey = newOutlineTexKey;
                AcquirePooledTexture(newOutlineTexKey, outlineGradient, isLinear);
            }

            if (innerShadowTexKey != newInnerShadowTexKey || (newInnerShadowTexKey != 0 && !texPool.ContainsKey(newInnerShadowTexKey)))
            {
                ReleasePooledTexture(innerShadowTexKey);
                innerShadowTexKey = newInnerShadowTexKey;
                AcquirePooledTexture(newInnerShadowTexKey, innerShadowGradient, isLinear);
            }

            if (outerShadowTexKey != newOuterShadowTexKey || (newOuterShadowTexKey != 0 && !texPool.ContainsKey(newOuterShadowTexKey)))
            {
                ReleasePooledTexture(outerShadowTexKey);
                outerShadowTexKey = newOuterShadowTexKey;
                AcquirePooledTexture(newOuterShadowTexKey, outerShadowGradient, isLinear);
            }

            var matKey = new MaterialKey(
                (int)fillColorMode, fillTexKey, fillGradientAngle, fillGradientZoom, fillGradientReverse, fillColor,
                (int)outlineColorMode, outlineTexKey, outlineGradientAngle, outlineGradientZoom, outlineGradientReverse, outlineColor,
                (int)innerShadowColorMode, innerShadowTexKey, innerShadowGradientAngle, innerShadowGradientZoom, innerShadowGradientReverse, innerShadowColor,
                (int)outerShadowColorMode, outerShadowTexKey, outerShadowGradientAngle, outerShadowGradientZoom, outerShadowGradientReverse, outerShadowColor,
                isLinear, shader
            );

            if (!matKey.Equals(currentMatKey) || pooledMaterial == null || !matPool.ContainsKey(matKey))
            {
                ReleasePooledMaterial(currentMatKey);
                currentMatKey = matKey;

                Texture2D fTex = fillTexKey != 0 && texPool.TryGetValue(fillTexKey, out var fEntry) ? fEntry.tex : null;
                Texture2D oTex = outlineTexKey != 0 && texPool.TryGetValue(outlineTexKey, out var oEntry) ? oEntry.tex : null;
                Texture2D iSTex = innerShadowTexKey != 0 && texPool.TryGetValue(innerShadowTexKey, out var iSEntry) ? iSEntry.tex : null;
                Texture2D oSTex = outerShadowTexKey != 0 && texPool.TryGetValue(outerShadowTexKey, out var oSEntry) ? oSEntry.tex : null;

                pooledMaterial = AcquirePooledMaterial(matKey, fTex, oTex, iSTex, oSTex);
            }
        }

        void AcquirePooledTexture(int key, Gradient g, bool isLinear)
        {
            if (key == 0 || g == null)
                return;

            if (texPool.TryGetValue(key, out var entry))
            {
                entry.refCount++;
                return;
            }

            Texture2D tex = new(GradientRes, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                name = "ProceduralRect_SharedGradient",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int i = 0; i < GradientRes; i++)
            {
                Color c = g.Evaluate((float)i / (GradientRes - 1));
                gradientPixels32[i] = SyncColorPrecision(c, isLinear);
            }

            tex.SetPixels32(gradientPixels32);
            tex.Apply(updateMipmaps: false);

            texPool[key] = new TexEntry { tex = tex, refCount = 1 };
        }

        void ReleasePooledTexture(int key)
        {
            if (key == 0)
                return;

            if (texPool.TryGetValue(key, out var entry))
            {
                entry.refCount--;
                if (entry.refCount <= 0)
                {
                    SafeDestroy(entry.tex);
                    texPool.Remove(key);
                }
            }
        }

        Material AcquirePooledMaterial(MaterialKey key, Texture2D fillTex, Texture2D outlineTex, Texture2D innerShadowTex, Texture2D outerShadowTex)
        {
            if (matPool.TryGetValue(key, out var entry))
            {
                entry.refCount++;
                return entry.mat;
            }

            Shader fallbackShader = Shader.Find(ShaderName);
            Material mat = new(key.shader ? key.shader : fallbackShader)
            {
                name = "ProceduralRect_SharedMat",
                hideFlags = HideFlags.HideAndDontSave,
            };

            mat.SetFloat(ID_FillColorMode, key.fillMode);
            mat.SetVector(ID_FillColor, SyncColorPrecision(key.fillColor, key.isLinear));
            mat.SetFloat(ID_FillGradientAngle, key.fillAngle);
            mat.SetFloat(ID_FillGradientZoom, key.fillZoom);
            mat.SetFloat(ID_FillGradientReverse, key.fillRev ? 1f : 0f);
            if (fillTex) { mat.SetTexture(ID_FillGradientTex, fillTex); }

            mat.SetFloat(ID_OutlineColorMode, key.outlineMode);
            mat.SetVector(ID_OutlineColor, SyncColorPrecision(key.outlineColor, key.isLinear));
            mat.SetFloat(ID_OutlineGradientAngle, key.outlineAngle);
            mat.SetFloat(ID_OutlineGradientZoom, key.outlineZoom);
            mat.SetFloat(ID_OutlineGradientReverse, key.outlineRev ? 1f : 0f);
            if (outlineTex) { mat.SetTexture(ID_OutlineGradientTex, outlineTex); }

            mat.SetFloat(ID_InnerShadowColorMode, key.innerShadowMode);
            mat.SetVector(ID_InnerShadowColor, SyncColorPrecision(key.innerShadowColor, key.isLinear));
            mat.SetFloat(ID_InnerShadowGradientAngle, key.innerShadowAngle);
            mat.SetFloat(ID_InnerShadowGradientZoom, key.innerShadowZoom);
            mat.SetFloat(ID_InnerShadowGradientReverse, key.innerShadowRev ? 1f : 0f);
            if (innerShadowTex) { mat.SetTexture(ID_InnerShadowGradientTex, innerShadowTex); }

            mat.SetFloat(ID_OuterShadowColorMode, key.outerShadowMode);
            mat.SetVector(ID_OuterShadowColor, SyncColorPrecision(key.outerShadowColor, key.isLinear));
            mat.SetFloat(ID_OuterShadowGradientAngle, key.outerShadowAngle);
            mat.SetFloat(ID_OuterShadowGradientZoom, key.outerShadowZoom);
            mat.SetFloat(ID_OuterShadowGradientReverse, key.outerShadowRev ? 1f : 0f);
            if (outerShadowTex) { mat.SetTexture(ID_OuterShadowGradientTex, outerShadowTex); }

            matPool[key] = new MatEntry { mat = mat, refCount = 1 };
            return mat;
        }

        void ReleasePooledMaterial(MaterialKey key)
        {
            if (!key.IsValid)
                return;

            if (matPool.TryGetValue(key, out var entry))
            {
                entry.refCount--;
                if (entry.refCount <= 0)
                {
                    SafeDestroy(entry.mat);
                    matPool.Remove(key);
                }
            }
        }

        static void SafeDestroy(Object obj)
        {
            if (!obj)
                return;

            if (Application.isPlaying) { Destroy(obj); }
            else { DestroyImmediate(obj); }
        }

        float PackFillConfig()
        {
            int method = (int)clipMethod;
            int origin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());
            int cw = clipClockwise ? 1 : 0;
            return method + origin * 8 + cw * 64;
        }

        int MaxOriginForMethod() => clipMethod switch
        {
            ClipMethod.Horizontal => 1,
            ClipMethod.Vertical => 1,
            ClipMethod.Radial360 => 3,
            _ => 0,
        };

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return g;
        }

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (raycastMode == RaycastMode.None)
                return false;

            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, sp, eventCamera))
                return false;

            if (raycastMode == RaycastMode.Standard)
                return true;

            // Custom shape test (Alpha Hit without memory/texture sampling)
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 localPoint))
            {
                Rect r = rectTransform.rect;
                if (sprite != null && scaleMode == ScaleMode.Fit) { r = CalculateFitRect(r, sprite); }

                Vector2 halfSize = new(r.width * 0.5f, r.height * 0.5f);
                Vector2 center = r.center;
                Vector2 p = localPoint - center;

                float dist = GetSDF(p, halfSize, GetRadiiPixels(r), ActiveSquircleCorners);
                return dist <= 0f;
            }

            return false;
        }

        #region Public Methods
        /// <summary>
        /// Call this after modifying any public variables via code to update the visual mesh.
        /// </summary>
        public void UpdateRect()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void SetSprite(Sprite newSprite)
        {
            if (sprite != newSprite)
            {
                sprite = newSprite;
                SetAllDirty();
            }
        }

        /// <summary>
        /// Sets the corner radius for all corners simultaneously.
        /// </summary>
        public void SetCornerRadius(Vector4 radius)
        {
            if (cornerRadius != radius)
            {
                cornerRadius = radius;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Sets a uniform corner radius for all four corners.
        /// </summary>
        public void SetCornerRadius(float radius)
        {
            float r = Mathf.Max(0, radius);
            Vector4 uniformRadius = new(r, r, r, r);
            if (cornerRadius != uniformRadius)
            {
                cornerRadius = uniformRadius;
                SetVerticesDirty();
            }
        }
        #endregion

        #region Batch Pooling
        static readonly Dictionary<MaterialKey, MatEntry> matPool = new();
        static readonly Dictionary<int, TexEntry> texPool = new();

        class MatEntry
        {
            public Material mat;
            public int refCount;
        }

        class TexEntry
        {
            public Texture2D tex;
            public int refCount;
        }

        readonly struct MaterialKey : System.IEquatable<MaterialKey>
        {
            public readonly int fillMode, fillTexKey;
            public readonly float fillAngle, fillZoom;
            public readonly bool fillRev;
            public readonly Color fillColor;

            public readonly int outlineMode, outlineTexKey;
            public readonly float outlineAngle, outlineZoom;
            public readonly bool outlineRev;
            public readonly Color outlineColor;

            public readonly int innerShadowMode, innerShadowTexKey;
            public readonly float innerShadowAngle, innerShadowZoom;
            public readonly bool innerShadowRev;
            public readonly Color innerShadowColor;

            public readonly int outerShadowMode, outerShadowTexKey;
            public readonly float outerShadowAngle, outerShadowZoom;
            public readonly bool outerShadowRev;
            public readonly Color outerShadowColor;

            public readonly bool isLinear;
            public readonly Shader shader;

            public bool IsValid => shader != null;

            public MaterialKey(
                int fMode, int fTex, float fAng, float fZoom, bool fRev, Color fCol,
                int oMode, int oTex, float oAng, float oZoom, bool oRev, Color oCol,
                int inSMode, int inSTex, float inSAng, float inSZoom, bool inSRev, Color inSCol,
                int outSMode, int outSTex, float outSAng, float outSZoom, bool outSRev, Color outSCol,
                bool lin, Shader sh)
            {
                fillMode = fMode; fillTexKey = fTex; fillAngle = fAng; fillZoom = fZoom; fillRev = fRev; fillColor = fCol;
                outlineMode = oMode; outlineTexKey = oTex; outlineAngle = oAng; outlineZoom = oZoom; outlineRev = oRev; outlineColor = oCol;
                innerShadowMode = inSMode; innerShadowTexKey = inSTex; innerShadowAngle = inSAng; innerShadowZoom = inSZoom; innerShadowRev = inSRev; innerShadowColor = inSCol;
                outerShadowMode = outSMode; outerShadowTexKey = outSTex; outerShadowAngle = outSAng; outerShadowZoom = outSZoom; outerShadowRev = outSRev; outerShadowColor = outSCol;
                isLinear = lin; shader = sh;
            }

            public bool Equals(MaterialKey o)
            {
                return fillMode == o.fillMode && fillTexKey == o.fillTexKey &&
                       fillAngle == o.fillAngle && fillZoom == o.fillZoom && fillRev == o.fillRev && fillColor == o.fillColor &&
                       outlineMode == o.outlineMode && outlineTexKey == o.outlineTexKey &&
                       outlineAngle == o.outlineAngle && outlineZoom == o.outlineZoom && outlineRev == o.outlineRev &&
                       outlineColor == o.outlineColor &&
                       innerShadowMode == o.innerShadowMode && innerShadowTexKey == o.innerShadowTexKey &&
                       innerShadowAngle == o.innerShadowAngle && innerShadowZoom == o.innerShadowZoom && innerShadowRev == o.innerShadowRev &&
                       innerShadowColor == o.innerShadowColor &&
                       outerShadowMode == o.outerShadowMode && outerShadowTexKey == o.outerShadowTexKey &&
                       outerShadowAngle == o.outerShadowAngle && outerShadowZoom == o.outerShadowZoom && outerShadowRev == o.outerShadowRev &&
                       outerShadowColor == o.outerShadowColor &&
                       isLinear == o.isLinear && shader == o.shader;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + fillMode;
                    hash = hash * 31 + fillTexKey;
                    hash = hash * 31 + fillAngle.GetHashCode();
                    hash = hash * 31 + fillZoom.GetHashCode();
                    hash = hash * 31 + fillRev.GetHashCode();
                    hash = hash * 31 + fillColor.GetHashCode();

                    hash = hash * 31 + outlineMode;
                    hash = hash * 31 + outlineTexKey;
                    hash = hash * 31 + outlineAngle.GetHashCode();
                    hash = hash * 31 + outlineZoom.GetHashCode();
                    hash = hash * 31 + outlineRev.GetHashCode();
                    hash = hash * 31 + outlineColor.GetHashCode();

                    hash = hash * 31 + innerShadowMode;
                    hash = hash * 31 + innerShadowTexKey;
                    hash = hash * 31 + innerShadowAngle.GetHashCode();
                    hash = hash * 31 + innerShadowZoom.GetHashCode();
                    hash = hash * 31 + innerShadowRev.GetHashCode();
                    hash = hash * 31 + innerShadowColor.GetHashCode();

                    hash = hash * 31 + outerShadowMode;
                    hash = hash * 31 + outerShadowTexKey;
                    hash = hash * 31 + outerShadowAngle.GetHashCode();
                    hash = hash * 31 + outerShadowZoom.GetHashCode();
                    hash = hash * 31 + outerShadowRev.GetHashCode();
                    hash = hash * 31 + outerShadowColor.GetHashCode();

                    hash = hash * 31 + isLinear.GetHashCode();
                    hash = hash * 31 + (shader ? shader.GetHashCode() : 0);
                    return hash;
                }
            }
        }
        #endregion

#if UNITY_EDITOR
        [HideInInspector] public bool graphicFoldout = true;
        [HideInInspector] public bool fillFoldout = true;
        [HideInInspector] public bool outlineFoldout = false;
        [HideInInspector] public bool cornerRadiusFoldout = false;
        [HideInInspector] public bool innerShadowFoldout = false;
        [HideInInspector] public bool outerShadowFoldout = false;

        protected override void OnValidate()
        {
            // Sync native culling
            base.raycastTarget = raycastMode != RaycastMode.None;
            base.OnValidate();

            clipOrigin = Mathf.Clamp(clipOrigin, 0, MaxOriginForMethod());

            EnsureCanvasChannels();
            UpdateCachedParent();
            SetVerticesDirty();
            SetMaterialDirty();

            // Forces an immediate visual update in the Editor when modifying the parent
            if (!Application.isPlaying)
            {
                foreach (Transform child in transform)
                {
                    var childRect = child.GetComponent<ProceduralRect>();        
                    if (childRect != null && childRect.radiusSyncMode == RadiusSyncMode.MatchParent)
                        childRect.UpdateRect();
                }
            }
        }

        protected override void Reset()
        {
            base.Reset();

            embeddedShader = Shader.Find(ShaderName);
            fillGradient = DefaultGradient();
            outlineGradient = DefaultGradient();
            innerShadowGradient = DefaultGradient();
            outerShadowGradient = DefaultGradient();
        }
#endif
    }
}